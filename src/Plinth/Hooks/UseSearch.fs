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

    React.useEffect (
        (fun () ->
            if query.Trim().Length < 2 then
                setResults [||]
                React.createDisposable ignore
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

                React.createDisposable (fun () -> JS.clearTimeout timer)),
        [| box query |]
    )

    { Query = query
      Results = results
      SetQuery = setQuery
      Clear = fun () -> setQuery "" }
