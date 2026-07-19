# Plinth — Next Steps

Status as of 2026-07-18 (night): **v0.2.0 — "the Firmament" — is SHIPPED.**
Pushed, installer built, and published as the latest GitHub release
(https://github.com/SignalTheoryCo/plinth/releases/tag/v0.2.0). The landing
page Download button serves it via `/releases/latest`.

## Decided 2026-07-18 (Sebbe): Plinth is FREE and open source

Draft B adopted as the README (with the v0.2.0 features folded in), MIT
LICENSE added, landing byline now links to sebbejones.com. Both draft files
deleted after adoption; `demo-vault/` gitignored (local screenshot content).
For the record, draft A's paid-product idea (one-time purchase + optional
"Plinth Sync" subscription) is parked, not lost: revisit only if Plinth ever
outgrows its role as free evidence for the Teach AI Your Business course.

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
