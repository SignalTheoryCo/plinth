module Plinth.Utils.Date

open System

/// Daily-note name for today, e.g. "2026-07-03". The ".md" extension is
/// added by the Rust side; note names never carry it.
let todayName () =
    let now = DateTime.Now
    sprintf "%04i-%02i-%02i" now.Year now.Month now.Day
