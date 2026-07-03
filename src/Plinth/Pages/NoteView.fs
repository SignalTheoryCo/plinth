/// Main layout: sidebar + editor + backlinks — or the welcome screen
/// when no vault has been chosen yet.
module Plinth.Pages.NoteView

open Feliz
open Plinth.Types
open Plinth.Hooks
open Plinth.Components

let private pillar (title: string) (text: string) =
    Html.div [
        prop.className "flex-1 rounded-lg border border-stone-200 bg-white p-4 shadow-sm"
        prop.children [
            Html.h3 [ prop.className "font-semibold text-emerald-900"; prop.text title ]
            Html.p [ prop.className "mt-1 text-sm text-stone-500"; prop.text text ]
        ]
    ]

let private centered (children: ReactElement list) =
    Html.div [
        prop.className "flex flex-1 items-center justify-center text-stone-400"
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
                        prop.className "font-serif text-5xl font-bold text-emerald-900"
                        prop.text "Plinth"
                    ]
                    Html.p [
                        prop.className "mt-4 leading-relaxed text-stone-600"
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

    let currentName =
        match api.Current with
        | EditingNote(n, _)
        | LoadingNote n -> Some n
        | _ -> None

    Html.div [
        prop.className "flex h-screen bg-stone-50 font-sans text-stone-800"
        prop.children [
            match api.Current with
            | NoVault -> welcome api.PickVault
            | _ ->
                Html.aside [
                    prop.className "flex w-64 flex-none flex-col border-r border-stone-200 bg-stone-100/60"
                    prop.children [
                        Html.div [
                            prop.className "px-4 pt-4"
                            prop.children [
                                Html.span [
                                    prop.className "font-serif text-xl font-bold tracking-tight text-emerald-900"
                                    prop.text "Plinth"
                                ]
                            ]
                        ]
                        Search.SearchBox search api.OpenNote
                        Sidebar.Sidebar api
                    ]
                ]

                match api.Current with
                | NoVault -> Html.none
                | VaultLoading -> centered [ Html.p [ prop.text "Indexing vault…" ] ]
                | LoadingNote n -> centered [ Html.p [ prop.text (sprintf "Opening %s…" n) ] ]
                | NoteError msg ->
                    centered [
                        Html.div [
                            prop.className "max-w-md rounded-lg border border-red-200 bg-red-50 p-6 text-center"
                            prop.children [
                                Html.p [ prop.className "font-medium text-red-800"; prop.text "Something went wrong" ]
                                Html.p [ prop.className "mt-2 text-sm text-red-600"; prop.text msg ]
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
                    Editor.Editor name content api.Dirty api.UpdateContent api.OpenNote api.FilterByTag

                Backlinks.Backlinks currentName api.Backlinks api.OpenNote
        ]
    ]
