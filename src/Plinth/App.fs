/// Entry point: mount the app into #root.
module Plinth.App

open Browser.Dom
open Feliz
open Plinth.Pages

let private root = ReactDOM.createRoot (document.getElementById "root")
root.render (NoteView.NoteView())
