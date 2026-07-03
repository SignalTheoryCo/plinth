# Plinth

> Software changes, companies fold, and clouds crash. Your thoughts deserve a
> foundation that doesn't move. Plinth is a local-first markdown notebook that
> strips away the noise, leaving you with just your notes, in plain text,
> exactly where you left them.

## The three pillars

- **Zero Cloud** — Every file lives natively on your hard drive as standard Markdown.
- **Zero Plugins** — Core features are baked in so you never troubleshoot broken community code.
- **Zero Friction** — Open the app, type your thoughts, and close it. No setup required.

## Features

- Markdown notes stored as plain `.md` files in a folder you choose
- Daily notes auto-created as `YYYY-MM-DD.md`
- Wiki-style `[[Note Name]]` links between notes
- Full-text search across all notes
- Backlinks panel showing which notes link to the current one
- `#tag` support with a tag explorer in the sidebar
- No cloud, no accounts, no sync — move the folder and the app still works

## Tech stack

- [Fable](https://fable.io) — F# compiled to JavaScript
- [Tauri v2](https://v2.tauri.app) — Rust backend for file system + SQLite
- [Feliz](https://zaid-ajaj.github.io/Feliz/) (React) + Tailwind CSS
- SQLite as a rebuildable index/search cache stored inside the vault (`.plinth/index.db`);
  the markdown files are always the source of truth

## Prerequisites

- Node.js 20+
- .NET SDK 8+
- Rust (stable, MSVC toolchain on Windows) + WebView2 (preinstalled on Windows 11)

## Development

```sh
npm install
dotnet tool restore
npm run tauri dev     # compiles F# + Rust, opens the app with hot reload
```

`npm run tauri dev` runs Fable in watch mode, serves the frontend with Vite,
and launches the Tauri shell. To build a release bundle: `npm run tauri build`.

## Project structure

```
src-tauri/            Rust backend (Tauri commands, SQLite index, link parser)
src/Plinth/           F# frontend (Feliz components, hooks, utils)
Plinth.fsproj         F# project file (compile order lives here)
```
