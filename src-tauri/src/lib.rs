mod link_parser;
mod note_index;

use note_index::{NoteMeta, SearchHit, TagCount};
use rusqlite::Connection;
use serde::Serialize;
use std::fs;
use std::io::Write;
use std::path::{Path, PathBuf};
use std::sync::Mutex;
use tauri::State;
use walkdir::WalkDir;

#[derive(Serialize)]
#[serde(rename_all = "PascalCase")]
pub struct NoteContent {
    pub name: String,
    pub content: String,
}

struct Vault {
    root: PathBuf,
    conn: Connection,
}

struct AppState(Mutex<Option<Vault>>);

/// Reject note names that could escape the vault ("../evil", "a/b", "C:x").
fn validate_name(name: &str) -> Result<(), String> {
    if name.is_empty() {
        return Err("Note name is empty".into());
    }
    if name.contains(['/', '\\', ':']) || name.contains("..") {
        return Err(format!("Invalid note name: {name}"));
    }
    Ok(())
}

fn with_vault<T>(
    state: &State<AppState>,
    f: impl FnOnce(&mut Vault) -> Result<T, String>,
) -> Result<T, String> {
    let mut guard = state.0.lock().map_err(|e| e.to_string())?;
    match guard.as_mut() {
        Some(vault) => f(vault),
        None => Err("No vault selected".into()),
    }
}

#[tauri::command]
fn set_vault(path: String, state: State<AppState>) -> Result<Vec<NoteMeta>, String> {
    let root = PathBuf::from(&path);
    if !root.is_dir() {
        return Err(format!("Not a folder: {path}"));
    }
    let cache_dir = root.join(".plinth");
    fs::create_dir_all(&cache_dir).map_err(|e| e.to_string())?;
    let conn = note_index::open(&cache_dir.join("index.db")).map_err(|e| e.to_string())?;
    note_index::reindex_vault(&conn, &root)?;
    let notes = note_index::list_notes(&conn).map_err(|e| e.to_string())?;
    *state.0.lock().map_err(|e| e.to_string())? = Some(Vault { root, conn });
    Ok(notes)
}

#[tauri::command]
fn read_dir(state: State<AppState>) -> Result<Vec<NoteMeta>, String> {
    with_vault(&state, |v| {
        note_index::list_notes(&v.conn).map_err(|e| e.to_string())
    })
}

/// Read a note by name. Following a [[link]] to a note that does not exist
/// yet should create it, so a miss creates the file with a heading stub.
/// Returns the canonical name so "[[plinth roadmap]]" opens as "Plinth Roadmap".
#[tauri::command]
fn read_file(name: String, state: State<AppState>) -> Result<NoteContent, String> {
    let name = name.trim().to_string();
    validate_name(&name)?;
    with_vault(&state, |v| {
        let result = match note_index::note_path(&v.conn, &name).map_err(|e| e.to_string())? {
            Some((path, canonical)) => NoteContent {
                content: fs::read_to_string(&path).map_err(|e| e.to_string())?,
                name: canonical,
            },
            None => {
                let path = v.root.join(format!("{name}.md"));
                let content = format!("# {name}\n\n");
                fs::write(&path, &content).map_err(|e| e.to_string())?;
                note_index::index_note(&v.conn, &name, &path.to_string_lossy(), &content)
                    .map_err(|e| e.to_string())?;
                NoteContent { name: name.clone(), content }
            }
        };
        note_index::touch_recent(&v.conn, &result.name).map_err(|e| e.to_string())?;
        Ok(result)
    })
}

/// Write a note and refresh its index entry. Returns the updated note list
/// so the sidebar can stay in sync with one round trip.
#[tauri::command]
fn write_file(
    name: String,
    content: String,
    state: State<AppState>,
) -> Result<Vec<NoteMeta>, String> {
    let name = name.trim().to_string();
    validate_name(&name)?;
    with_vault(&state, |v| {
        let (path, canonical) =
            match note_index::note_path(&v.conn, &name).map_err(|e| e.to_string())? {
                Some(resolved) => resolved,
                None => (v.root.join(format!("{name}.md")), name.clone()),
            };
        fs::write(&path, &content).map_err(|e| e.to_string())?;
        note_index::index_note(&v.conn, &canonical, &path.to_string_lossy(), &content)
            .map_err(|e| e.to_string())?;
        note_index::list_notes(&v.conn).map_err(|e| e.to_string())
    })
}

/// Delete a note's file and drop it from the index. Notes that linked to it
/// keep their [[link]] text; those links just create the note again if clicked.
#[tauri::command]
fn delete_file(name: String, state: State<AppState>) -> Result<Vec<NoteMeta>, String> {
    let name = name.trim().to_string();
    validate_name(&name)?;
    with_vault(&state, |v| {
        if let Some((path, _)) = note_index::note_path(&v.conn, &name).map_err(|e| e.to_string())? {
            fs::remove_file(&path).map_err(|e| e.to_string())?;
        }
        note_index::remove_note(&v.conn, &name).map_err(|e| e.to_string())?;
        note_index::list_notes(&v.conn).map_err(|e| e.to_string())
    })
}

#[tauri::command]
fn get_recents(state: State<AppState>) -> Result<Vec<String>, String> {
    with_vault(&state, |v| {
        note_index::recent_notes(&v.conn, 10).map_err(|e| e.to_string())
    })
}

/// Zip every visible file in the vault to `dest`. Returns the file count.
#[tauri::command]
fn export_vault(dest: String, state: State<AppState>) -> Result<usize, String> {
    with_vault(&state, |v| export_zip(&v.root, Path::new(&dest)))
}

fn export_zip(root: &Path, dest: &Path) -> Result<usize, String> {
    let file = fs::File::create(dest).map_err(|e| e.to_string())?;
    let mut zip = zip::ZipWriter::new(file);
    let options = zip::write::SimpleFileOptions::default()
        .compression_method(zip::CompressionMethod::Deflated);
    let mut count = 0;
    for entry in WalkDir::new(root)
        .into_iter()
        .filter_entry(|e| !note_index::is_hidden(e))
        .filter_map(|e| e.ok())
    {
        let path = entry.path();
        // Don't zip the archive into itself if it's being written into the vault.
        if !path.is_file() || path == dest {
            continue;
        }
        let rel = path.strip_prefix(root).map_err(|e| e.to_string())?;
        zip.start_file(rel.to_string_lossy().replace('\\', "/"), options)
            .map_err(|e| e.to_string())?;
        let data = fs::read(path).map_err(|e| e.to_string())?;
        zip.write_all(&data).map_err(|e| e.to_string())?;
        count += 1;
    }
    zip.finish().map_err(|e| e.to_string())?;
    Ok(count)
}

#[tauri::command]
fn search(query: String, state: State<AppState>) -> Result<Vec<SearchHit>, String> {
    with_vault(&state, |v| {
        note_index::search(&v.conn, &query).map_err(|e| e.to_string())
    })
}

#[tauri::command]
fn get_backlinks(name: String, state: State<AppState>) -> Result<Vec<String>, String> {
    with_vault(&state, |v| {
        note_index::backlinks(&v.conn, name.trim()).map_err(|e| e.to_string())
    })
}

#[tauri::command]
fn get_tags(state: State<AppState>) -> Result<Vec<TagCount>, String> {
    with_vault(&state, |v| {
        note_index::all_tags(&v.conn).map_err(|e| e.to_string())
    })
}

#[tauri::command]
fn get_notes_by_tag(tag: String, state: State<AppState>) -> Result<Vec<String>, String> {
    with_vault(&state, |v| {
        note_index::notes_for_tag(&v.conn, &tag).map_err(|e| e.to_string())
    })
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .manage(AppState(Mutex::new(None)))
        .invoke_handler(tauri::generate_handler![
            set_vault,
            read_dir,
            read_file,
            write_file,
            delete_file,
            search,
            get_backlinks,
            get_tags,
            get_notes_by_tag,
            get_recents,
            export_vault
        ])
        .run(tauri::generate_context!())
        .expect("error while running Plinth");
}
