# Plinth — Next Steps

Status as of 2026-07-18 (night): **v0.2.0 — "the Firmament" — is SHIPPED.**
Pushed, installer built, and published as the latest GitHub release
(https://github.com/SignalTheoryCo/plinth/releases/tag/v0.2.0). The landing
page Download button serves it via `/releases/latest`.

## Open decision

- **Pick the README direction.** Two drafts sit untracked in the repo root and
  they disagree on the business model, not just tone: `README-draft-A-product.md`
  (paid one-time purchase + optional Plinth Sync subscription) vs.
  `README-draft-B-statement.md` (free and open source, statement piece). This
  also decides how Plinth is framed as evidence in the Teach AI Your Business
  course. The current README shipped with v0.2.0 and is fine until this is
  settled. (`demo-vault/` is also untracked; sort it with this decision.)

## Still open from before

4. Put the landing page online (GitHub Pages serving `landing/`, or drag the
   folder onto Netlify). The page now has real screenshots and the Firmament
   section — worth doing while it's fresh.
5. Decide on code signing (removes the "Windows protected your PC" warning).
   Azure Trusted Signing is the current low-cost route. Not a blocker.
6. Make the build stand on its own: install the .NET SDK and Rust as normal
   system tools so `build.cmd` doesn't depend on Claude's app storage.
7. Small thing to check: confirm the app never creates a `.plinth` folder
   inside its own index folder (old doubly-nested `.plinth/.plinth/` sighting).

## How to pick up next session

Open this file. If v0.2.0 isn't on GitHub yet, do items 1–3 — that's ten
minutes and the Firmament is live.
