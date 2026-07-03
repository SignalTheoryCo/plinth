/// Debounced full-text search: waits 250 ms after the last keystroke,
/// ignores queries shorter than two characters.
module Plinth.Hooks.UseSearch

open Fable.Core
open Feliz
open Plinth
open Plinth.Types

type SearchApi =
    { Query: string
      Results: SearchHit[]
      SetQuery: string -> unit
      Clear: unit -> unit }

let useSearch () : SearchApi =
    let query, setQuery = React.useState ""
    let results, setResults = React.useState<SearchHit[]> [||]

    let searchEffect () : unit -> unit =
        if query.Trim().Length < 2 then
            setResults [||]
            ignore
        else
            let timer =
                JS.setTimeout
                    (fun () ->
                        promise {
                            try
                                let! hits = Tauri.search (query.Trim())
                                setResults hits
                            with _ ->
                                setResults [||]
                        }
                        |> Promise.start)
                    250

            fun () -> JS.clearTimeout timer

    React.useEffect (searchEffect, [| box query |])

    { Query = query
      Results = results
      SetQuery = setQuery
      Clear = fun () -> setQuery "" }
