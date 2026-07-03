module Plinth.Types

/// Metadata for a note in the vault, as returned by the Rust index.
type NoteMeta = { Name: string; Path: string }

/// One full-text search result.
type SearchHit = { Name: string; Snippet: string }

/// A tag and how many notes carry it.
type TagCount = { Tag: string; Count: int }

/// Everything the main pane can be showing, as one discriminated union —
/// impossible states (an editor with no note, a note without a vault)
/// simply cannot be represented.
type NoteState =
    | NoVault
    | VaultLoading
    | LoadingNote of name: string
    | EditingNote of name: string * content: string
    | NoteError of message: string
