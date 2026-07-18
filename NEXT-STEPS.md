# Plinth — Next Steps

Status as of 2026-07-18: **v0.2.0 — "the Firmament" — is code-complete and
committed.** The vault graph view (Ctrl+G), command palette (Ctrl+K), browser
dev mode, real screenshots, and the updated landing page are all in the repo.
v0.1.0 remains the published release on GitHub
(https://github.com/SignalTheoryCo/plinth) until you cut the new one.

## To ship v0.2.0

1. **Push to GitHub.** Everything is committed locally on `main`; just
   `git push`.
2. **Build the installer.** Run `build.cmd` (or `npm run tauri build`).
   The bundle lands in `src-tauri/target/release/bundle/nsis/` as
   `Plinth_0.2.0_x64-setup.exe`.
3. **Publish the release.** New GitHub release tagged `v0.2.0`, attach the
   installer, paste the highlights from README (Firmament, palette). The
   landing page Download button points at `/releases/latest`, so it starts
   serving 0.2.0 automatically.

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
