module Plinth.Types

/// Metadata for a note in the vault, as returned by the Rust index.
type NoteMeta = { Name: string; Path: string }

/// A note's canonical name plus its markdown body.
type NoteContent = { Name: string; Content: string }

/// One full-text search result.
type SearchHit = { Name: string; Snippet: string }

/// A tag and how many notes carry it.
type TagCount = { Tag: string; Count: int }

/// One star in the Firmament. Exists = false marks a "ghost": a link
/// target no note file backs yet.
type GraphNode =
    { Name: string
      Exists: bool
      Tags: string[]
      Degree: int }

/// One [[link]] between two notes, canonical names on both ends.
type GraphEdge = { Source: string; Target: string }

/// The whole vault as nodes + edges, as returned by the Rust index.
type GraphData =
    { Nodes: GraphNode[]
      Edges: GraphEdge[] }

/// Everything the main pane can be showing, as one discriminated union —
/// impossible states (an editor with no note, a note without a vault)
/// simply cannot be represented.
type NoteState =
    | NoVault
    | VaultLoading
    | LoadingNote of name: string
    | EditingNote of name: string * content: string
    | NoteError of message: string
