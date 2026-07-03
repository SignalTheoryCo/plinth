/// The bridge to the Rust backend: every call the frontend can make.
module Plinth.Tauri

open Fable.Core
open Fable.Core.JsInterop
open Plinth.Types

[<Import("invoke", "@tauri-apps/api/core")>]
let private invoke<'T> (cmd: string) (args: obj) : JS.Promise<'T> = jsNative

[<Import("open", "@tauri-apps/plugin-dialog")>]
let private openDialog (options: obj) : JS.Promise<string option> = jsNative

[<Import("save", "@tauri-apps/plugin-dialog")>]
let private saveDialogRaw (options: obj) : JS.Promise<string option> = jsNative

[<Import("confirm", "@tauri-apps/plugin-dialog")>]
let private confirmRaw (message: string) (options: obj) : JS.Promise<bool> = jsNative

[<Import("message", "@tauri-apps/plugin-dialog")>]
let private messageRaw (message: string) (options: obj) : JS.Promise<unit> = jsNative

/// Ask the user to pick the vault folder. None if they cancelled.
let pickFolder () : JS.Promise<string option> =
    openDialog
        {| directory = true
           multiple = false
           title = "Choose your Plinth vault folder" |}

/// Native yes/no confirmation dialog.
let confirmDialog (message: string) : JS.Promise<bool> =
    confirmRaw message {| title = "Plinth"; kind = "warning" |}

/// Native info/error message box.
let messageDialog (message: string) (isError: bool) : JS.Promise<unit> =
    messageRaw message {| title = "Plinth"; kind = (if isError then "error" else "info") |}

/// Ask where to save the vault export. None if cancelled.
let saveZipDialog (defaultName: string) : JS.Promise<string option> =
    saveDialogRaw
        {| defaultPath = defaultName
           title = "Export vault as zip"
           filters = [| {| name = "Zip archive"; extensions = [| "zip" |] |} |] |}

/// Open a vault: rebuilds the SQLite index and returns the note list.
let setVault (path: string) : JS.Promise<NoteMeta[]> =
    invoke "set_vault" {| path = path |}

let readDir () : JS.Promise<NoteMeta[]> =
    invoke "read_dir" (createObj [])

/// Read a note; creates it if it doesn't exist yet. Returns the canonical
/// name (so "[[plinth roadmap]]" opens as "Plinth Roadmap") plus content.
let readFile (name: string) : JS.Promise<NoteContent> =
    invoke "read_file" {| name = name |}

/// Delete a note's file; returns the refreshed note list.
let deleteFile (name: string) : JS.Promise<NoteMeta[]> =
    invoke "delete_file" {| name = name |}

/// Save a note; returns the refreshed note list for the sidebar.
let writeFile (name: string) (content: string) : JS.Promise<NoteMeta[]> =
    invoke "write_file" {| name = name; content = content |}

let search (query: string) : JS.Promise<SearchHit[]> =
    invoke "search" {| query = query |}

let getBacklinks (name: string) : JS.Promise<string[]> =
    invoke "get_backlinks" {| name = name |}

let getTags () : JS.Promise<TagCount[]> =
    invoke "get_tags" (createObj [])

let getNotesByTag (tag: string) : JS.Promise<string[]> =
    invoke "get_notes_by_tag" {| tag = tag |}

/// Last 10 opened notes, newest first.
let getRecents () : JS.Promise<string[]> =
    invoke "get_recents" (createObj [])

/// Zip the whole vault to `dest`; returns the number of files archived.
let exportVault (dest: string) : JS.Promise<int> =
    invoke "export_vault" {| dest = dest |}
