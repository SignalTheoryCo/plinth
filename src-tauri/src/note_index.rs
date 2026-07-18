//! SQLite index over the vault: notes, links, tags.
//!
//! The database is a rebuildable cache stored inside the vault at
//! `.plinth/index.db`. The markdown files are always the source of truth —
//! the whole index is rebuilt from them every time a vault is opened, so the
//! vault folder can be moved freely.

use crate::link_parser;
use rusqlite::{params, Connection};
use serde::Serialize;
use std::collections::{BTreeMap, HashMap, HashSet};
use std::fs;
use std::path::{Path, PathBuf};
use walkdir::WalkDir;

#[derive(Serialize, Clone)]
#[serde(rename_all = "PascalCase")]
pub struct NoteMeta {
    pub name: String,
    pub path: String,
}

#[derive(Serialize)]
#[serde(rename_all = "PascalCase")]
pub struct SearchHit {
    pub name: String,
    pub snippet: String,
}

#[derive(Serialize)]
#[serde(rename_all = "PascalCase")]
pub struct TagCount {
    pub tag: String,
    pub count: i64,
}

#[derive(Serialize)]
#[serde(rename_all = "PascalCase")]
pub struct GraphNode {
    pub name: String,
    /// False for "ghost" nodes: link targets no note file backs yet.
    pub exists: bool,
    pub tags: Vec<String>,
    pub degree: i64,
}

#[derive(Serialize)]
#[serde(rename_all = "PascalCase")]
pub struct GraphEdge {
    pub source: String,
    pub target: String,
}

#[derive(Serialize)]
#[serde(rename_all = "PascalCase")]
pub struct GraphData {
    pub nodes: Vec<GraphNode>,
    pub edges: Vec<GraphEdge>,
}

pub fn open(db_path: &Path) -> rusqlite::Result<Connection> {
    let conn = Connection::open(db_path)?;
    conn.execute_batch(
        "CREATE TABLE IF NOT EXISTS notes (
             name    TEXT PRIMARY KEY,
             path    TEXT NOT NULL,
             content TEXT NOT NULL
         );
         CREATE TABLE IF NOT EXISTS links (
             source TEXT NOT NULL,
             target TEXT NOT NULL
         );
         CREATE TABLE IF NOT EXISTS tags (
             note TEXT NOT NULL,
             tag  TEXT NOT NULL
         );
         CREATE TABLE IF NOT EXISTS recents (
             note      TEXT PRIMARY KEY,
             opened_at INTEGER NOT NULL
         );
         CREATE INDEX IF NOT EXISTS idx_links_target ON links(target);
         CREATE INDEX IF NOT EXISTS idx_tags_tag ON tags(tag);",
    )?;
    Ok(conn)
}

/// Walk the vault and rebuild the whole index. Hidden folders (including
/// `.plinth` itself) are skipped. Returns the number of notes indexed.
pub fn reindex_vault(conn: &Connection, vault: &Path) -> Result<usize, String> {
    conn.execute_batch("DELETE FROM notes; DELETE FROM links; DELETE FROM tags;")
        .map_err(|e| e.to_string())?;
    let mut count = 0;
    for entry in WalkDir::new(vault)
        .into_iter()
        .filter_entry(|e| !is_hidden(e))
        .filter_map(|e| e.ok())
    {
        let path = entry.path();
        if path.extension().and_then(|s| s.to_str()) != Some("md") {
            continue;
        }
        let Some(name) = path.file_stem().and_then(|s| s.to_str()) else {
            continue;
        };
        let content = fs::read_to_string(path).unwrap_or_default();
        index_note(conn, name, &path.to_string_lossy(), &content).map_err(|e| e.to_string())?;
        count += 1;
    }
    Ok(count)
}

pub(crate) fn is_hidden(entry: &walkdir::DirEntry) -> bool {
    entry.depth() > 0
        && entry
            .file_name()
            .to_str()
            .map(|s| s.starts_with('.'))
            .unwrap_or(false)
}

/// Upsert a single note and replace its outgoing links and tags.
pub fn index_note(conn: &Connection, name: &str, path: &str, content: &str) -> rusqlite::Result<()> {
    conn.execute(
        "INSERT INTO notes(name, path, content) VALUES (?1, ?2, ?3)
         ON CONFLICT(name) DO UPDATE SET path = ?2, content = ?3",
        params![name, path, content],
    )?;
    conn.execute("DELETE FROM links WHERE source = ?1", params![name])?;
    conn.execute("DELETE FROM tags WHERE note = ?1", params![name])?;
    for target in link_parser::extract_links(content) {
        conn.execute(
            "INSERT INTO links(source, target) VALUES (?1, ?2)",
            params![name, target],
        )?;
    }
    for tag in link_parser::extract_tags(content) {
        conn.execute(
            "INSERT INTO tags(note, tag) VALUES (?1, ?2)",
            params![name, tag],
        )?;
    }
    Ok(())
}

pub fn list_notes(conn: &Connection) -> rusqlite::Result<Vec<NoteMeta>> {
    let mut stmt = conn.prepare("SELECT name, path FROM notes ORDER BY name COLLATE NOCASE")?;
    let rows = stmt.query_map([], |r| {
        Ok(NoteMeta {
            name: r.get(0)?,
            path: r.get(1)?,
        })
    })?;
    rows.collect()
}

/// Resolve a note name (case-insensitively) to its file path and the
/// canonical name stored in the index. Callers must index/track under the
/// canonical name so "[[plinth roadmap]]" and "Plinth Roadmap.md" stay one note.
pub fn note_path(conn: &Connection, name: &str) -> rusqlite::Result<Option<(PathBuf, String)>> {
    let mut stmt = conn.prepare("SELECT path, name FROM notes WHERE name = ?1 COLLATE NOCASE")?;
    let mut rows = stmt.query_map(params![name], |r| {
        Ok((r.get::<_, String>(0)?, r.get::<_, String>(1)?))
    })?;
    match rows.next() {
        Some(row) => {
            let (path, canonical) = row?;
            Ok(Some((PathBuf::from(path), canonical)))
        }
        None => Ok(None),
    }
}

/// True when every char of `needle` appears in `hay` in order ("plnth" ~ "plinth").
fn is_subsequence(needle: &str, hay: &str) -> bool {
    let mut hay_chars = hay.chars();
    'outer: for nc in needle.chars() {
        for hc in hay_chars.by_ref() {
            if hc == nc {
                continue 'outer;
            }
        }
        return false;
    }
    true
}

/// Ranked search: exact title substring beats all-terms-in-title beats a
/// fuzzy (subsequence) title match; matching the body adds on top. Every
/// hit carries a context snippet around the first body match.
pub fn search(conn: &Connection, query: &str) -> rusqlite::Result<Vec<SearchHit>> {
    let q = query.trim().to_lowercase();
    if q.is_empty() {
        return Ok(Vec::new());
    }
    let terms: Vec<&str> = q.split_whitespace().collect();
    let compact: String = q.chars().filter(|c| !c.is_whitespace()).collect();

    let mut stmt = conn.prepare("SELECT name, content FROM notes")?;
    let rows = stmt.query_map([], |r| {
        Ok((r.get::<_, String>(0)?, r.get::<_, String>(1)?))
    })?;

    let mut scored: Vec<(i64, SearchHit)> = Vec::new();
    for row in rows {
        let (name, content) = row?;
        let name_l = name.to_lowercase();
        let content_l = content.to_lowercase();

        let mut score = if name_l.contains(&q) {
            100
        } else if terms.iter().all(|t| name_l.contains(t)) {
            80
        } else if compact.len() >= 3 && is_subsequence(&compact, &name_l) {
            55
        } else {
            0
        };

        // First term that appears in the body anchors the snippet.
        let body_anchor = terms
            .iter()
            .filter_map(|t| content_l.find(t).map(|pos| (pos, *t)))
            .min();
        if terms.iter().all(|t| content_l.contains(t)) {
            score += 35;
        }

        if score > 0 {
            let snippet = match body_anchor {
                Some((_, term)) => make_snippet(&content, term),
                None => content.chars().take(80).collect(),
            };
            scored.push((score, SearchHit { name, snippet }));
        }
    }

    scored.sort_by(|a, b| b.0.cmp(&a.0).then_with(|| a.1.name.cmp(&b.1.name)));
    Ok(scored.into_iter().take(50).map(|(_, hit)| hit).collect())
}

/// Record that a note was opened just now (for the Recent section).
pub fn touch_recent(conn: &Connection, name: &str) -> rusqlite::Result<()> {
    let now_ms = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_millis() as i64)
        .unwrap_or(0);
    conn.execute(
        "INSERT INTO recents(note, opened_at) VALUES (?1, ?2)
         ON CONFLICT(note) DO UPDATE SET opened_at = ?2",
        params![name, now_ms],
    )?;
    Ok(())
}

/// Most recently opened notes, newest first. The join drops entries whose
/// note has since been deleted or renamed.
pub fn recent_notes(conn: &Connection, limit: i64) -> rusqlite::Result<Vec<String>> {
    let mut stmt = conn.prepare(
        "SELECT r.note FROM recents r JOIN notes n ON n.name = r.note
         ORDER BY r.opened_at DESC LIMIT ?1",
    )?;
    let rows = stmt.query_map(params![limit], |r| r.get(0))?;
    rows.collect()
}

/// Drop a note from every index table. Backlinks pointing at it stay in
/// other notes' text and simply become create-on-click links again.
pub fn remove_note(conn: &Connection, name: &str) -> rusqlite::Result<()> {
    conn.execute("DELETE FROM notes WHERE name = ?1 COLLATE NOCASE", params![name])?;
    conn.execute("DELETE FROM links WHERE source = ?1 COLLATE NOCASE", params![name])?;
    conn.execute("DELETE FROM tags WHERE note = ?1 COLLATE NOCASE", params![name])?;
    conn.execute("DELETE FROM recents WHERE note = ?1 COLLATE NOCASE", params![name])?;
    Ok(())
}

fn make_snippet(content: &str, query: &str) -> String {
    let lower = content.to_lowercase();
    let q = query.to_lowercase();
    let Some(pos) = lower.find(&q) else {
        return content.chars().take(80).collect();
    };
    // to_lowercase can shift byte offsets for exotic characters, so clamp to
    // the nearest char boundary in the original text.
    let mut start = pos.saturating_sub(40).min(content.len());
    while start > 0 && !content.is_char_boundary(start) {
        start -= 1;
    }
    let mut end = (pos + q.len() + 40).min(content.len());
    while end < content.len() && !content.is_char_boundary(end) {
        end += 1;
    }
    let mut snippet = content[start..end].replace('\n', " ");
    if start > 0 {
        snippet = format!("…{snippet}");
    }
    if end < content.len() {
        snippet.push('…');
    }
    snippet
}

/// Notes whose bodies contain `[[name]]`, case-insensitive on the target.
pub fn backlinks(conn: &Connection, name: &str) -> rusqlite::Result<Vec<String>> {
    let mut stmt = conn.prepare(
        "SELECT DISTINCT source FROM links
         WHERE target = ?1 COLLATE NOCASE
         ORDER BY source COLLATE NOCASE",
    )?;
    let rows = stmt.query_map(params![name], |r| r.get(0))?;
    rows.collect()
}

pub fn all_tags(conn: &Connection) -> rusqlite::Result<Vec<TagCount>> {
    let mut stmt = conn.prepare("SELECT tag, COUNT(*) FROM tags GROUP BY tag ORDER BY tag")?;
    let rows = stmt.query_map([], |r| {
        Ok(TagCount {
            tag: r.get(0)?,
            count: r.get(1)?,
        })
    })?;
    rows.collect()
}

/// The whole vault as a graph: every note a node, every distinct [[link]]
/// an edge. Targets resolve case-insensitively to their canonical note;
/// targets with no note behind them become ghost nodes (exists = false) so
/// broken links show up in the Firmament instead of vanishing.
pub fn graph(conn: &Connection) -> rusqlite::Result<GraphData> {
    let notes = list_notes(conn)?;
    let mut canonical: HashMap<String, String> = HashMap::new();
    for n in &notes {
        canonical.insert(n.name.to_lowercase(), n.name.clone());
    }

    let mut note_tags: HashMap<String, Vec<String>> = HashMap::new();
    let mut stmt = conn.prepare("SELECT note, tag FROM tags ORDER BY tag")?;
    let rows = stmt.query_map([], |r| {
        Ok((r.get::<_, String>(0)?, r.get::<_, String>(1)?))
    })?;
    for row in rows {
        let (note, tag) = row?;
        note_tags.entry(note).or_default().push(tag);
    }

    let mut stmt = conn.prepare("SELECT source, target FROM links")?;
    let rows = stmt.query_map([], |r| {
        Ok((r.get::<_, String>(0)?, r.get::<_, String>(1)?))
    })?;

    // BTreeMap keeps ghost ordering deterministic across reloads.
    let mut ghosts: BTreeMap<String, String> = BTreeMap::new();
    let mut seen: HashSet<(String, String)> = HashSet::new();
    let mut edges: Vec<GraphEdge> = Vec::new();
    for row in rows {
        let (source, raw_target) = row?;
        let target_lower = raw_target.to_lowercase();
        let target = match canonical.get(&target_lower) {
            Some(name) => name.clone(),
            None => ghosts
                .entry(target_lower.clone())
                .or_insert(raw_target)
                .clone(),
        };
        let source_lower = source.to_lowercase();
        // A note linking to itself adds nothing worth drawing.
        if source_lower == target_lower {
            continue;
        }
        if seen.insert((source_lower, target_lower)) {
            edges.push(GraphEdge { source, target });
        }
    }

    let mut degree: HashMap<String, i64> = HashMap::new();
    for e in &edges {
        *degree.entry(e.source.to_lowercase()).or_insert(0) += 1;
        *degree.entry(e.target.to_lowercase()).or_insert(0) += 1;
    }

    let mut nodes: Vec<GraphNode> = Vec::new();
    for n in notes {
        nodes.push(GraphNode {
            degree: *degree.get(&n.name.to_lowercase()).unwrap_or(&0),
            tags: note_tags.remove(&n.name).unwrap_or_default(),
            name: n.name,
            exists: true,
        });
    }
    for (lower, display) in ghosts {
        nodes.push(GraphNode {
            name: display,
            exists: false,
            tags: Vec::new(),
            degree: *degree.get(&lower).unwrap_or(&0),
        });
    }
    Ok(GraphData { nodes, edges })
}

pub fn notes_for_tag(conn: &Connection, tag: &str) -> rusqlite::Result<Vec<String>> {
    let mut stmt = conn.prepare(
        "SELECT DISTINCT note FROM tags WHERE tag = ?1 ORDER BY note COLLATE NOCASE",
    )?;
    let rows = stmt.query_map(params![tag.to_lowercase()], |r| r.get(0))?;
    rows.collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    /// The first-milestone flow, minus the GUI: build a vault on disk,
    /// index it, follow links, check backlinks/tags/search.
    #[test]
    fn milestone_flow() {
        let dir = std::env::temp_dir().join(format!("plinth-test-{}", std::process::id()));
        fs::create_dir_all(&dir).unwrap();
        fs::write(
            dir.join("2026-07-03.md"),
            "# 2026-07-03\n\nWorking on [[plinth roadmap]] today, then [[Someday]]. #dev\n",
        )
        .unwrap();
        fs::write(
            dir.join("Plinth Roadmap.md"),
            "# Plinth Roadmap\n\nShip milestone one. #dev #roadmap\n",
        )
        .unwrap();

        let conn = open(&dir.join("index.db")).unwrap();

        assert_eq!(reindex_vault(&conn, &dir).unwrap(), 2);

        // Sidebar list, sorted case-insensitively.
        let names: Vec<String> = list_notes(&conn).unwrap().into_iter().map(|n| n.name).collect();
        assert_eq!(names, vec!["2026-07-03", "Plinth Roadmap"]);

        // [[Plinth Roadmap]] resolves case-insensitively (with spaces) and
        // reports the canonical name; the daily note shows as its backlink.
        let (_, canonical) = note_path(&conn, "plinth roadmap").unwrap().unwrap();
        assert_eq!(canonical, "Plinth Roadmap");
        assert_eq!(backlinks(&conn, "Plinth Roadmap").unwrap(), vec!["2026-07-03"]);

        // Tag explorer: #dev on both notes, #roadmap on one.
        let tags = all_tags(&conn).unwrap();
        assert_eq!(tags.len(), 2);
        assert_eq!((tags[0].tag.as_str(), tags[0].count), ("dev", 2));
        assert_eq!(notes_for_tag(&conn, "roadmap").unwrap(), vec!["Plinth Roadmap"]);

        // Full-text search hits the body, not just the title.
        let hits = search(&conn, "milestone").unwrap();
        assert_eq!(hits.len(), 1);
        assert_eq!(hits[0].name, "Plinth Roadmap");
        assert!(hits[0].snippet.to_lowercase().contains("milestone"));

        // Fuzzy title match: subsequence with typos-by-omission still finds it.
        let fuzzy = search(&conn, "plnth rdmp").unwrap();
        assert_eq!(fuzzy.len(), 1);
        assert_eq!(fuzzy[0].name, "Plinth Roadmap");

        // Multi-term search must match all terms.
        assert_eq!(search(&conn, "ship nothing").unwrap().len(), 0);
        assert_eq!(search(&conn, "ship milestone").unwrap().len(), 1);

        // The Firmament graph: 2 real notes plus a ghost for the broken
        // link, with the lowercase [[plinth roadmap]] edge resolved to
        // canonical.
        let g = graph(&conn).unwrap();
        assert_eq!(g.nodes.len(), 3);
        let hub = g.nodes.iter().find(|n| n.name == "Plinth Roadmap").unwrap();
        assert!(hub.exists);
        assert_eq!(hub.degree, 1);
        assert_eq!(hub.tags, vec!["dev".to_string(), "roadmap".to_string()]);
        let ghost = g.nodes.iter().find(|n| n.name == "Someday").unwrap();
        assert!(!ghost.exists);
        assert_eq!(ghost.degree, 1);
        assert_eq!(g.edges.len(), 2);
        assert!(g
            .edges
            .iter()
            .any(|e| e.source == "2026-07-03" && e.target == "Plinth Roadmap"));

        // Recents: most recently opened first, capped, and pruned on delete.
        touch_recent(&conn, "Plinth Roadmap").unwrap();
        std::thread::sleep(std::time::Duration::from_millis(5));
        touch_recent(&conn, "2026-07-03").unwrap();
        assert_eq!(
            recent_notes(&conn, 10).unwrap(),
            vec!["2026-07-03", "Plinth Roadmap"]
        );

        remove_note(&conn, "plinth roadmap").unwrap();
        assert_eq!(list_notes(&conn).unwrap().len(), 1);
        assert_eq!(recent_notes(&conn, 10).unwrap(), vec!["2026-07-03"]);
        assert!(all_tags(&conn).unwrap().iter().all(|t| t.tag != "roadmap"));

        fs::remove_dir_all(&dir).ok();
    }
}
