/// Search input with an inline results list.
module Plinth.Components.Search

open Feliz
open Plinth.Hooks.UseSearch

[<ReactComponent>]
let SearchBox (search: SearchApi) (onOpen: string -> unit) =
    Html.div [
        prop.className "border-b border-stone-200 p-3"
        prop.children [
            Html.input [
                prop.className
                    "w-full rounded border border-stone-300 bg-white px-3 py-1.5 text-sm outline-none focus:border-emerald-600 dark:border-stone-600 dark:bg-stone-800 dark:text-stone-200"
                prop.placeholder "Search notes…"
                prop.value search.Query
                prop.onChange (fun (v: string) -> search.SetQuery v)
            ]
            if search.Results.Length > 0 then
                Html.div [
                    prop.className
                        "mt-2 max-h-64 overflow-y-auto rounded border border-stone-200 bg-white shadow-sm dark:border-stone-700 dark:bg-stone-800"
                    prop.children (
                        search.Results
                        |> Array.toList
                        |> List.map (fun hit ->
                            Html.button [
                                prop.key hit.Name
                                prop.className
                                    "block w-full px-3 py-2 text-left hover:bg-stone-100 dark:hover:bg-stone-700"
                                prop.onClick (fun _ ->
                                    onOpen hit.Name
                                    search.Clear ())
                                prop.children [
                                    Html.p [
                                        prop.className
                                            "truncate text-sm font-medium text-stone-800 dark:text-stone-200"
                                        prop.text hit.Name
                                    ]
                                    Html.p [
                                        prop.className "truncate text-xs text-stone-400 dark:text-stone-500"
                                        prop.text hit.Snippet
                                    ]
                                ]
                            ])
                    )
                ]
        ]
    ]
