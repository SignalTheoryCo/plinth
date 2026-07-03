/// Plain-textarea editor with a Preview toggle. No rich text, no WYSIWYG.
module Plinth.Components.Editor

open Feliz
open Plinth.Utils

[<ReactComponent>]
let Editor
    (name: string)
    (content: string)
    (dirty: bool)
    (onChange: string -> unit)
    (onLink: string -> unit)
    (onTag: string -> unit)
    =
    let preview, setPreview = React.useState false

    Html.div [
        prop.className "flex h-full min-w-0 flex-1 flex-col"
        prop.children [
            Html.div [
                prop.className "flex items-center justify-between border-b border-stone-200 px-6 py-3"
                prop.children [
                    Html.div [
                        prop.className "flex items-baseline gap-3"
                        prop.children [
                            Html.h1 [
                                prop.className "font-serif text-lg font-semibold text-stone-800"
                                prop.text name
                            ]
                            Html.span [
                                prop.className (
                                    if dirty then "text-xs text-amber-600"
                                    else "text-xs text-stone-400"
                                )
                                prop.text (if dirty then "unsaved" else "saved")
                            ]
                        ]
                    ]
                    Html.button [
                        prop.className
                            "rounded border border-stone-300 px-3 py-1 text-sm text-stone-600 hover:bg-stone-100"
                        prop.onClick (fun _ -> setPreview (not preview))
                        prop.text (if preview then "Edit" else "Preview")
                    ]
                ]
            ]
            if preview then
                Html.div [
                    prop.className "flex-1 overflow-y-auto px-6 py-4"
                    prop.children [ Markdown.render content onLink onTag ]
                ]
            else
                Html.textarea [
                    prop.className
                        "flex-1 resize-none bg-transparent px-6 py-4 font-mono text-[15px] leading-relaxed text-stone-800 outline-none"
                    prop.value content
                    prop.spellcheck false
                    prop.placeholder "Type your thoughts. Link with [[Note Name]], tag with #tag."
                    prop.onChange (fun (v: string) -> onChange v)
                ]
        ]
    ]
