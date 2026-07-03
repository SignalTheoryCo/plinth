/// The bridge to the Rust backend: every call the frontend can make.
module Plinth.Tauri

open Fable.Core
open Fable.Core.JsInterop
open Plinth.Types

[<Import("invoke", "@tauri-apps/api/core")>]
let private invoke<'T> (cmd: string) (args: obj) : JS.Promise<'T> = jsNative

[<Import("open", "@tauri-apps/plugin-dialog")>]
let private openDialog (options: obj) : JS.Promise<string option> = jsNative

/// Ask the user to pick the vault folder. None if they cancelled.
let pickFolder () : JS.Promise<string option> =
    openDialog
        {| directory = true
           multiple = false
           title = "Choose your Plinth vault folder" |}

/// Open a vault: rebuilds the SQLite index and returns the note list.
let setVault (path: string) : JS.Promise<NoteMeta[]> =
    invoke "set_vault" {| path = path |}

let readDir () : JS.Promise<NoteMeta[]> =
    invoke "read_dir" (createObj [])

/// Read a note's content; creates the note if it doesn't exist yet.
let readFile (name: string) : JS.Promise<string> =
    invoke "read_file" {| name = name |}

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
