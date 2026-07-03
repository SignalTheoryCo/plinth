//! SQLite index over the vault: notes, links, tags.
//!
//! The database is a rebuildable cache stored inside the vault at
//! `.plinth/index.db`. The markdown files are always the source of truth —
//! the whole index is rebuilt from them every time a vault is opened, so the
//! vault folder can be moved freely.

use crate::link_parser;
use rusqlite::{params, Connection};
use serde::Serialize;
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

fn is_hidden(entry: &walkdir::DirEntry) -> bool {
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

/// Resolve a note name (case-insensitively) to its file path.
pub fn note_path(conn: &Connection, name: &str) -> rusqlite::Result<Option<PathBuf>> {
    let mut stmt = conn.prepare("SELECT path FROM notes WHERE name = ?1 COLLATE NOCASE")?;
    let mut rows = stmt.query_map(params![name], |r| r.get::<_, String>(0))?;
    match rows.next() {
        Some(path) => Ok(Some(PathBuf::from(path?))),
        None => Ok(None),
    }
}

/// Case-insensitive substring search over note names and bodies.
pub fn search(conn: &Connection, query: &str) -> rusqlite::Result<Vec<SearchHit>> {
    let pattern = format!("%{}%", query);
    let mut stmt = conn.prepare(
        "SELECT name, content FROM notes
         WHERE name LIKE ?1 OR content LIKE ?1
         ORDER BY name COLLATE NOCASE
         LIMIT 50",
    )?;
    let rows = stmt.query_map(params![pattern], |r| {
        Ok((r.get::<_, String>(0)?, r.get::<_, String>(1)?))
    })?;
    let mut hits = Vec::new();
    for row in rows {
        let (name, content) = row?;
        hits.push(SearchHit {
            snippet: make_snippet(&content, query),
            name,
        });
    }
    Ok(hits)
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

pub fn notes_for_tag(conn: &Connection, tag: &str) -> rusqlite::Result<Vec<String>> {
    let mut stmt = conn.prepare(
        "SELECT DISTINCT note FROM tags WHERE tag = ?1 ORDER BY note COLLATE NOCASE",
    )?;
    let rows = stmt.query_map(params![tag.to_lowercase()], |r| r.get(0))?;
    rows.collect()
}
