/// Plain-textarea editor with a Preview toggle. No rich text, no WYSIWYG.
module Plinth.Components.Editor

open Feliz
open Plinth.Utils

type EditorProps =
    { Name: string
      Content: string
      Dirty: bool
      FontPx: int
      NoteExists: string -> bool
      OnChange: string -> unit
      OnLink: string -> unit
      OnTag: string -> unit
      OnDelete: unit -> unit }

[<ReactComponent>]
let Editor (props: EditorProps) =
    let preview, setPreview = React.useState false

    Html.div [
        prop.className "flex h-full min-w-0 flex-1 flex-col"
        prop.children [
            Html.div [
                prop.className
                    "flex items-center justify-between border-b border-stone-200 px-6 py-3 dark:border-stone-700"
                prop.children [
                    Html.div [
                        prop.className "flex items-baseline gap-3"
                        prop.children [
                            Html.h1 [
                                prop.className "font-serif text-lg font-semibold text-stone-800 dark:text-stone-100"
                                prop.text props.Name
                            ]
                            Html.span [
                                prop.className (
                                    if props.Dirty then "text-xs text-amber-600 dark:text-amber-400"
                                    else "text-xs text-stone-400 dark:text-stone-500"
                                )
                                prop.text (if props.Dirty then "unsaved" else "saved")
                            ]
                        ]
                    ]
                    Html.div [
                        prop.className "flex items-center gap-2"
                        prop.children [
                            Html.button [
                                prop.className
                                    "rounded border border-stone-300 px-3 py-1 text-sm text-stone-600 hover:bg-stone-100 dark:border-stone-600 dark:text-stone-300 dark:hover:bg-stone-800"
                                prop.onClick (fun _ -> setPreview (not preview))
                                prop.text (if preview then "Edit" else "Preview")
                            ]
                            Html.button [
                                prop.className
                                    "rounded border border-red-200 px-3 py-1 text-sm text-red-600 hover:bg-red-50 dark:border-red-900 dark:text-red-400 dark:hover:bg-red-950"
                                prop.title "Delete this note"
                                prop.onClick (fun _ -> props.OnDelete ())
                                prop.text "Delete"
                            ]
                        ]
                    ]
                ]
            ]
            if preview then
                Html.div [
                    prop.className "flex-1 overflow-y-auto px-6 py-4"
                    prop.style [ style.fontSize (length.px props.FontPx) ]
                    prop.children [ Markdown.render props.Content props.NoteExists props.OnLink props.OnTag ]
                ]
            else
                Html.textarea [
                    prop.className
                        "flex-1 resize-none bg-transparent px-6 py-4 font-mono leading-relaxed text-stone-800 outline-none dark:text-stone-200"
                    prop.style [ style.fontSize (length.px props.FontPx) ]
                    prop.value props.Content
                    prop.custom ("spellCheck", false)
                    prop.placeholder "Type your thoughts. Link with [[Note Name]], tag with #tag."
                    prop.onChange (fun (v: string) -> props.OnChange v)
                ]
        ]
    ]
