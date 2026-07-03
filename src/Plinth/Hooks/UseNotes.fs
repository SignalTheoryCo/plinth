/// All vault/note state management in one custom hook.
module Plinth.Hooks.UseNotes

open Fable.Core
open Feliz
open Plinth
open Plinth.Types
open Plinth.Utils

type NotesApi =
    { Vault: string option
      Notes: NoteMeta[]
      Tags: TagCount[]
      Backlinks: string[]
      Recents: string[]
      Current: NoteState
      TagFilter: (string * string[]) option
      Dirty: bool
      PickVault: unit -> unit
      OpenNote: string -> unit
      OpenToday: unit -> unit
      UpdateContent: string -> unit
      DeleteNote: string -> unit
      ExportVault: unit -> unit
      FilterByTag: string -> unit
      ClearTagFilter: unit -> unit }

let useNotes () : NotesApi =
    let vault, setVault = React.useState<string option> None
    let notes, setNotes = React.useState<NoteMeta[]> [||]
    let tags, setTags = React.useState<TagCount[]> [||]
    let backlinks, setBacklinks = React.useState<string[]> [||]
    let recents, setRecents = React.useState<string[]> [||]
    let current, setCurrent = React.useState<NoteState> NoVault
    let tagFilter, setTagFilter = React.useState<(string * string[]) option> None
    let dirty, setDirty = React.useState false
    // Bumped on every keystroke so a save that finishes can tell whether
    // newer edits arrived while it was writing (and must stay dirty).
    let editGen = React.useRef 0

    let refreshTags () =
        promise {
            let! t = Tauri.getTags ()
            setTags t
        }

    let openNote (name: string) =
        let name = name.Trim()
        setCurrent (LoadingNote name)

        promise {
            try
                let! note = Tauri.readFile name
                // The note may have just been created by following a link,
                // so refresh the sidebar list too.
                let! updated = Tauri.readDir ()
                setNotes updated
                let! bl = Tauri.getBacklinks note.Name
                setBacklinks bl
                let! recent = Tauri.getRecents ()
                setRecents recent
                do! refreshTags ()
                setDirty false
                setCurrent (EditingNote(note.Name, note.Content))
            with ex ->
                setCurrent (NoteError ex.Message)
        }
        |> Promise.start

    let openToday () = openNote (Date.todayName ())

    let pickVault () =
        promise {
            try
                let! choice = Tauri.pickFolder ()

                match choice with
                | None -> ()
                | Some path ->
                    setCurrent VaultLoading
                    let! initial = Tauri.setVault path
                    setVault (Some path)
                    setNotes initial
                    setTagFilter None
                    openNote (Date.todayName ())
            with ex ->
                setCurrent (NoteError ex.Message)
        }
        |> Promise.start

    let updateContent (text: string) =
        match current with
        | EditingNote(name, _) ->
            editGen.current <- editGen.current + 1
            setDirty true
            setCurrent (EditingNote(name, text))
        | _ -> ()

    // Debounced autosave, 700 ms after the last keystroke. The effect's
    // cleanup cancels the pending timer whenever the content changes again.
    let autosaveEffect () : unit -> unit =
        match current, dirty with
        | EditingNote(name, content), true ->
            let timer =
                JS.setTimeout
                    (fun () ->
                        let gen = editGen.current

                        promise {
                            try
                                let! updated = Tauri.writeFile name content
                                setNotes updated
                                let! bl = Tauri.getBacklinks name
                                setBacklinks bl
                                do! refreshTags ()

                                if editGen.current = gen then
                                    setDirty false
                            with ex ->
                                setCurrent (NoteError ex.Message)
                        }
                        |> Promise.start)
                    700

            fun () -> JS.clearTimeout timer
        | _ -> ignore

    React.useEffect (autosaveEffect, [| box current; box dirty |])

    let filterByTag (tag: string) =
        promise {
            let! names = Tauri.getNotesByTag tag
            setTagFilter (Some(tag, names))
        }
        |> Promise.start

    let deleteNote (name: string) =
        promise {
            try
                let! confirmed =
                    Tauri.confirmDialog (
                        sprintf "Delete \"%s\"?\n\nThe .md file will be removed from your vault. Links to it in other notes stay as text and will recreate it if clicked." name
                    )

                if confirmed then
                    let! updated = Tauri.deleteFile name
                    setNotes updated
                    setTagFilter None
                    do! refreshTags ()
                    let! recent = Tauri.getRecents ()
                    setRecents recent

                    // Land somewhere sensible: the most recent other note,
                    // or today's daily note if the vault is now empty.
                    match recent |> Array.tryHead with
                    | Some next -> openNote next
                    | None ->
                        match updated |> Array.tryHead with
                        | Some n -> openNote n.Name
                        | None -> openNote (Date.todayName ())
            with ex ->
                setCurrent (NoteError ex.Message)
        }
        |> Promise.start

    let exportVault () =
        promise {
            try
                let! dest = Tauri.saveZipDialog (sprintf "plinth-vault-%s.zip" (Date.todayName ()))

                match dest with
                | None -> ()
                | Some path ->
                    let! count = Tauri.exportVault path
                    do! Tauri.messageDialog (sprintf "Exported %i files to:\n%s" count path) false
            with ex ->
                do! Tauri.messageDialog ("Export failed: " + ex.Message) true
        }
        |> Promise.start

    { Vault = vault
      Notes = notes
      Tags = tags
      Backlinks = backlinks
      Recents = recents
      Current = current
      TagFilter = tagFilter
      Dirty = dirty
      PickVault = pickVault
      OpenNote = openNote
      OpenToday = openToday
      UpdateContent = updateContent
      DeleteNote = deleteNote
      ExportVault = exportVault
      FilterByTag = filterByTag
      ClearTagFilter = fun () -> setTagFilter None }
