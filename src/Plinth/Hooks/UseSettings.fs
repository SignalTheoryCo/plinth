/// App settings: theme and editor font size, persisted in localStorage.
/// (The vault path itself is not a setting — it's chosen per session and
/// everything else lives inside the vault folder.)
module Plinth.Hooks.UseSettings

open Browser.WebStorage
open Feliz

type Theme =
    | Light
    | Dark

type FontSize =
    | Small
    | Medium
    | Large

type SettingsApi =
    { Theme: Theme
      FontSize: FontSize
      /// Editor/preview font size in pixels, derived from FontSize.
      EditorPx: int
      SetTheme: Theme -> unit
      SetFontSize: FontSize -> unit }

let private themeKey = "plinth-theme"
let private sizeKey = "plinth-font-size"

let private loadTheme () =
    match localStorage.getItem themeKey with
    | "dark" -> Dark
    | _ -> Light

let private loadSize () =
    match localStorage.getItem sizeKey with
    | "small" -> Small
    | "large" -> Large
    | _ -> Medium

let useSettings () : SettingsApi =
    let theme, setThemeState = React.useState (loadTheme ())
    let size, setSizeState = React.useState (loadSize ())

    let setTheme t =
        localStorage.setItem (themeKey, (match t with Dark -> "dark" | Light -> "light"))
        setThemeState t

    let setFontSize s =
        localStorage.setItem (
            sizeKey,
            (match s with
             | Small -> "small"
             | Medium -> "medium"
             | Large -> "large")
        )
        setSizeState s

    { Theme = theme
      FontSize = size
      EditorPx =
        (match size with
         | Small -> 14
         | Medium -> 15
         | Large -> 17)
      SetTheme = setTheme
      SetFontSize = setFontSize }
