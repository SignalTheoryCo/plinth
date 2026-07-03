mod link_parser;
mod note_index;

use note_index::{NoteMeta, SearchHit, TagCount};
use rusqlite::Connection;
use std::fs;
use std::path::PathBuf;
use std::sync::Mutex;
use tauri::State;

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
#[tauri::command]
fn read_file(name: String, state: State<AppState>) -> Result<String, String> {
    let name = name.trim().to_string();
    validate_name(&name)?;
    with_vault(&state, |v| {
        match note_index::note_path(&v.conn, &name).map_err(|e| e.to_string())? {
            Some(path) => fs::read_to_string(&path).map_err(|e| e.to_string()),
            None => {
                let path = v.root.join(format!("{name}.md"));
                let content = format!("# {name}\n\n");
                fs::write(&path, &content).map_err(|e| e.to_string())?;
                note_index::index_note(&v.conn, &name, &path.to_string_lossy(), &content)
                    .map_err(|e| e.to_string())?;
                Ok(content)
            }
        }
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
        let path = match note_index::note_path(&v.conn, &name).map_err(|e| e.to_string())? {
            Some(path) => path,
            None => v.root.join(format!("{name}.md")),
        };
        fs::write(&path, &content).map_err(|e| e.to_string())?;
        note_index::index_note(&v.conn, &name, &path.to_string_lossy(), &content)
            .map_err(|e| e.to_string())?;
        note_index::list_notes(&v.conn).map_err(|e| e.to_string())
    })
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
            search,
            get_backlinks,
            get_tags,
            get_notes_by_tag
        ])
        .run(tauri::generate_context!())
        .expect("error while running Plinth");
}
