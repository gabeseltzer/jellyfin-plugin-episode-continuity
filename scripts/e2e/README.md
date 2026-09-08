# Browser end-to-end check

Drives real jellyfin-web in headless Chrome and asserts that the interstitial and the up-next
warning appear. Chrome (not Playwright's Chromium) is needed because the test media is H.264.

Prerequisites, once per devcontainer:

```bash
cd scripts/e2e && npm install && sudo npx playwright install chrome
```

Library state the test expects (see `docs/plan.md`, step 7):

- Jellyfin setup wizard completed with user `dev` / password `dev`, `/media` added as a Shows library.
- Plugin deployed (`scripts/deploy.sh`) and `TreatNumberingGapsAsMissing` set to `true` in the
  plugin configuration (POST `/Plugins/<id>/Configuration`).
- `Alpha Show - S01E02.mp4` deleted so S01E01 → S01E03 is a gap.
- `Alpha Show - S01E01.mp4` regenerated at 11 minutes (jellyfin-web only shows the up-next dialog
  for episodes of 10 minutes or more).

Run:

```bash
SERVER_ID=<from /System/Info/Public> E1=<S01E01 item id> E3=<S01E03 item id> npm test
```

Screenshots and the captured console log land in `scripts/e2e/out/`.

## README screenshots

`screenshots.js` takes the same environment variables and writes the images the README embeds
to `docs/screenshots/`. It flips plugin settings through the API while it runs and restores them
at the end.

```bash
SERVER_ID=... E1=... E3=... node screenshots.js
```
