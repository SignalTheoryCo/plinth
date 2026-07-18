/// Main layout: sidebar + editor + backlinks — or the welcome screen
/// when no vault has been chosen yet.
module Plinth.Pages.NoteView

open Browser.Dom
open Feliz
open Plinth
open Plinth.Types
open Plinth.Hooks
open Plinth.Hooks.UseSettings
open Plinth.Components

let private pillar (title: string) (text: string) =
    Html.div [
        prop.className
            "flex-1 rounded-lg border border-stone-200 bg-white p-4 shadow-sm dark:border-stone-700 dark:bg-stone-800"
        prop.children [
            Html.h3 [
                prop.className "font-semibold text-emerald-900 dark:text-emerald-300"
                prop.text title
            ]
            Html.p [
                prop.className "mt-1 text-sm text-stone-500 dark:text-stone-400"
                prop.text text
            ]
        ]
    ]

let private centered (children: ReactElement list) =
    Html.div [
        prop.className "flex flex-1 items-center justify-center text-stone-400 dark:text-stone-500"
        prop.children children
    ]

let private welcome (onPick: unit -> unit) =
    Html.div [
        prop.className "flex flex-1 flex-col items-center justify-center gap-8 px-8"
        prop.children [
            Html.div [
                prop.className "max-w-xl text-center"
                prop.children [
                    Html.h1 [
                        prop.className "font-serif text-5xl font-bold text-emerald-900 dark:text-emerald-300"
                        prop.text "Plinth"
                    ]
                    Html.p [
                        prop.className "mt-4 leading-relaxed text-stone-600 dark:text-stone-300"
                        prop.text
                            "Software changes, companies fold, and clouds crash. Your thoughts deserve a foundation that doesn't move. Plinth is a local-first markdown notebook that strips away the noise, leaving you with just your notes, in plain text, exactly where you left them."
                    ]
                ]
            ]
            Html.div [
                prop.className "flex w-full max-w-3xl gap-6"
                prop.children [
                    pillar "Zero Cloud" "Every file lives natively on your hard drive as standard Markdown."
                    pillar "Zero Plugins" "Core features are baked in so you never troubleshoot broken community code."
                    pillar "Zero Friction" "Open the app, type your thoughts, and close it. No setup required."
                ]
            ]
            Html.button [
                prop.className
                    "rounded-lg bg-emerald-800 px-6 py-3 text-lg text-white shadow hover:bg-emerald-700"
                prop.onClick (fun _ -> onPick ())
                prop.text "Choose your vault folder"
            ]
        ]
    ]

[<ReactComponent>]
let NoteView () =
    let api = UseNotes.useNotes ()
    let search = UseSearch.useSearch ()
    let settings = useSettings ()
    let showGraph, setShowGraph = React.useState false
    let graphData, setGraphData = React.useState<GraphData option> None
    let showPalette, setShowPalette = React.useState false

    let currentName =
        match api.Current with
        | EditingNote(n, _)
        | LoadingNote n -> Some n
        | _ -> None

    // Case-insensitive existence check drives broken-link styling in preview.
    let noteSet =
        api.Notes
        |> Array.map (fun n -> n.Name.ToLowerInvariant())
        |> Set.ofArray

    let noteExists (name: string) =
        noteSet.Contains (name.Trim().ToLowerInvariant())

    // Fetch a fresh snapshot each time the Firmament opens, so new
    // notes and links written since the last look are already in the sky.
    let openGraph () =
        promise {
            try
                let! g = Tauri.getGraph ()
                setGraphData (Some g)
                setShowGraph true
            with _ -> ()
        }
        |> Promise.start

    let hasVault = api.Current <> NoVault

    // Ctrl/Cmd+D opens (creating if needed) today's daily note.
    let keyboardEffect () : unit -> unit =
        let handler (e: Browser.Types.Event) =
            let ke = e :?> Browser.Types.KeyboardEvent

            if (ke.ctrlKey || ke.metaKey) && ke.key.ToLowerInvariant() = "d" then
                ke.preventDefault ()
                api.OpenToday ()

        window.addEventListener ("keydown", handler)
        fun () -> window.removeEventListener ("keydown", handler)

    React.useEffect (keyboardEffect, [||])

    // Ctrl/Cmd+K palette and Ctrl/Cmd+G Firmament. Re-bound whenever
    // the toggles change so the handler never closes over stale state.
    let overlayKeysEffect () : unit -> unit =
        let handler (e: Browser.Types.Event) =
            let ke = e :?> Browser.Types.KeyboardEvent
            let key = ke.key.ToLowerInvariant()

            if (ke.ctrlKey || ke.metaKey) && (key = "k" || key = "p") then
                ke.preventDefault ()

                if hasVault then
                    setShowPalette (not showPalette)
            elif (ke.ctrlKey || ke.metaKey) && key = "g" then
                ke.preventDefault ()

                if hasVault then
                    if showGraph then setShowGraph false else openGraph ()

        window.addEventListener ("keydown", handler)
        fun () -> window.removeEventListener ("keydown", handler)

    React.useEffect (overlayKeysEffect, [| box showPalette; box showGraph; box hasVault |])

    let paletteActions: Palette.PaletteAction list =
        [ { Label = "Open today's daily note"
            Hint = "Ctrl+D"
            Run = api.OpenToday }
          { Label = "Open the Firmament"
            Hint = "Ctrl+G"
            Run = openGraph }
          { Label =
              (if settings.Theme = Dark then
                   "Switch to light theme"
               else
                   "Switch to dark theme")
            Hint = "theme"
            Run =
              fun () ->
                  settings.SetTheme (
                      if settings.Theme = Dark then Light else Dark
                  ) }
          { Label = "Export vault as .zip…"
            Hint = "vault"
            Run = api.ExportVault }
          { Label = "Change vault folder…"
            Hint = "vault"
            Run = api.PickVault } ]

    Html.div [
        prop.className (
            (if settings.Theme = Dark then "dark " else "")
            + "relative flex h-screen bg-stone-50 font-sans text-stone-800 dark:bg-stone-900 dark:text-stone-200"
        )
        prop.children [
            match api.Current with
            | NoVault -> welcome api.PickVault
            | _ ->
                Html.aside [
                    prop.className
                        "flex w-64 flex-none flex-col border-r border-stone-200 bg-stone-100/60 dark:border-stone-700 dark:bg-stone-800/60"
                    prop.children [
                        Html.div [
                            prop.className "flex items-center justify-between px-4 pt-4"
                            prop.children [
                                Html.span [
                                    prop.className
                                        "font-serif text-xl font-bold tracking-tight text-emerald-900 dark:text-emerald-300"
                                    prop.text "Plinth"
                                ]
                                Settings.SettingsMenu settings api.Vault api.PickVault api.ExportVault
                            ]
                        ]
                        Search.SearchBox search api.OpenNote
                        Sidebar.Sidebar api openGraph
                    ]
                ]

                match api.Current with
                | NoVault -> Html.none
                | VaultLoading -> centered [ Html.p [ prop.text "Indexing vault…" ] ]
                | LoadingNote n -> centered [ Html.p [ prop.text (sprintf "Opening %s…" n) ] ]
                | NoteError msg ->
                    centered [
                        Html.div [
                            prop.className
                                "max-w-md rounded-lg border border-red-200 bg-red-50 p-6 text-center dark:border-red-900 dark:bg-red-950"
                            prop.children [
                                Html.p [
                                    prop.className "font-medium text-red-800 dark:text-red-300"
                                    prop.text "Something went wrong"
                                ]
                                Html.p [
                                    prop.className "mt-2 text-sm text-red-600 dark:text-red-400"
                                    prop.text msg
                                ]
                                Html.button [
                                    prop.className
                                        "mt-4 rounded bg-emerald-800 px-4 py-1.5 text-sm text-white hover:bg-emerald-700"
                                    prop.onClick (fun _ -> api.OpenToday ())
                                    prop.text "Back to today's note"
                                ]
                            ]
                        ]
                    ]
                | EditingNote(name, content) ->
                    Editor.Editor
                        { Name = name
                          Content = content
                          Dirty = api.Dirty
                          FontPx = settings.EditorPx
                          NoteExists = noteExists
                          OnChange = api.UpdateContent
                          OnLink = api.OpenNote
                          OnTag = api.FilterByTag
                          OnDelete = fun () -> api.DeleteNote name }

                Backlinks.Backlinks currentName api.Backlinks api.OpenNote

                if showGraph then
                    match graphData with
                    | Some g ->
                        Graph.Firmament
                            { Data = g
                              Current = currentName
                              Dark = settings.Theme = Dark
                              OnOpen =
                                fun name ->
                                    setShowGraph false
                                    api.OpenNote name
                              OnClose = fun () -> setShowGraph false }
                    | None -> Html.none

                if showPalette then
                    Palette.Palette
                        { Notes = api.Notes
                          Recents = api.Recents
                          Actions = paletteActions
                          OnOpen = api.OpenNote
                          OnClose = fun () -> setShowPalette false }
        ]
    ]
