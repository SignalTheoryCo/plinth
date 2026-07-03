/// Right rail: which notes link to the current one.
module Plinth.Components.Backlinks

open Feliz

let private muted (text: string) =
    Html.p [ prop.className "px-4 text-xs text-stone-400"; prop.text text ]

[<ReactComponent>]
let Backlinks (current: string option) (links: string[]) (onOpen: string -> unit) =
    Html.div [
        prop.className "flex h-full w-60 flex-none flex-col border-l border-stone-200 bg-stone-100/60"
        prop.children [
            Html.h2 [
                prop.className
                    "px-4 pt-4 pb-1 text-[11px] font-semibold uppercase tracking-wider text-stone-400"
                prop.text "Backlinks"
            ]
            match current with
            | None -> muted "Open a note to see what links to it."
            | Some name ->
                if links.Length = 0 then
                    muted (sprintf "Nothing links to \"%s\" yet." name)
                else
                    Html.div [
                        prop.children (
                            links
                            |> Array.toList
                            |> List.map (fun l ->
                                Html.button [
                                    prop.key l
                                    prop.className
                                        "block w-full truncate px-4 py-1.5 text-left text-sm text-emerald-800 hover:bg-stone-200"
                                    prop.onClick (fun _ -> onOpen l)
                                    prop.text l
                                ])
                        )
                    ]
        ]
    ]
