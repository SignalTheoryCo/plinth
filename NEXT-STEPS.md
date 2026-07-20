# Plinth — Next Steps

Status as of 2026-07-18 (night): **v0.2.0 — "the Firmament" — is SHIPPED.**
Pushed, installer built, and published as the latest GitHub release
(https://github.com/sebbejones/plinth/releases/tag/v0.2.0). The landing
page Download button serves it via `/releases/latest`.

## Decided 2026-07-18 (Sebbe): Plinth is FREE and open source

Draft B adopted as the README (with the v0.2.0 features folded in), MIT
LICENSE added, landing byline now links to sebbejones.com. Both draft files
deleted after adoption; `demo-vault/` gitignored (local screenshot content).
For the record, draft A's paid-product idea (one-time purchase + optional
"Plinth Sync" subscription) is parked, not lost: revisit only if Plinth ever
outgrows its role as free evidence for the Teach AI Your Business course.

## Decided 2026-07-19 (Sebbe): one identity, everything under sebbejones

The GitHub account was renamed from `SignalTheoryCo` to `sebbejones`, display
name to "Sebbe Jones". SignalTheoryCo was never public anywhere: it only ever
appeared as the account name and the commit email. Old repo URLs redirect, so
the v0.2.0 download link keeps working either way. Links in this file, the
README, and `landing/index.html` were updated to the new account.

## Where the landing page goes

**Decision: `sebbejones.com/plinth`, as a page on the existing site.** Not
GitHub Pages. sebbejones.com already deploys from the `sebbejones-site` repo to
Netlify, and that site already uses this exact pattern for proof pages
(`/hotels`, `/workshop`, `/course`). Plinth is proof material for the Teach AI
Your Business course, so it belongs in that set.

A GitHub Pages workflow was built and then removed on 2026-07-19: it would have
added a second hosting system next to Netlify for no gain, and it would have put
the page on a github.io address instead of the domain.

The page itself is verified good. Served locally from `landing/`, every asset
loaded, no console errors, all sections rendered.

**DONE 2026-07-20.** The page is live at https://sebbejones.com/plinth/,
rebuilt in the site's brand system (cream/Georgia, dark hero band, sticky nav)
in the `sebbejones-site` repo (`plinth/index.html` plus the three screenshots
in `plinth/assets/`). Footer links added on the homepage, /workshop, and
/course; llms.txt gained a "Proof: Plinth" section; the page carries
SoftwareApplication schema. `landing/index.html` in this repo stays as the
standalone original; the site copy is the deployed one, so style edits should
happen there.

## Still open

1. Decide on code signing (removes the "Windows protected your PC" warning).
   Azure Trusted Signing is the current low-cost route. Not a blocker — the
   landing page already explains the warning to first-time downloaders.
2. Make the build stand on its own: install the .NET SDK and Rust as normal
   system tools so `build.cmd` doesn't depend on Claude's app storage.
3. Small thing to check: confirm the app never creates a `.plinth` folder
   inside its own index folder (old doubly-nested `.plinth/.plinth/` sighting).

## How to pick up next session

Open this file. The landing page shipped to sebbejones.com/plinth on
2026-07-20, so the list above ("Still open") is what remains: code signing,
a self-standing build, and the `.plinth` nesting check. None are urgent.
