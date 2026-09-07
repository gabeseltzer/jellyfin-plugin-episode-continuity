# Jellyfin Episode Continuity plugin

Warns viewers when the episode about to play is **not** the one that follows the previous
episode, because episodes in between are missing from the library. Have S01E01 and S01E03 but not
S01E02? Autoplay in Jellyfin silently jumps from 1 to 3. This plugin makes that visible.

## What it does

| Surface | Where | Behaviour |
| --- | --- | --- |
| Up-next warning | Jellyfin web | The "Next episode in N seconds" dialog gets a warning naming the missing episode(s) and the episode that will actually play. |
| Pre-play interstitial | Jellyfin web | Starting an episode that skips missing ones pauses playback and shows a countdown dialog with **Continue** and **Go back**. |
| Server toast | Any client that supports display messages | On playback start, the server sends a toast (or a dialog, or pause + dialog, configurable). Skipped for web sessions where the script above is active. |

"Missing" means an episode the metadata provider (TVDB, TMDb, ...) knows about that Jellyfin holds
as a virtual item, so shows need a metadata match. A hidden setting,
`TreatNumberingGapsAsMissing`, additionally treats non-contiguous episode numbers on disk as
missing; it is meant for development against unmatched libraries.

## How the web features work

Jellyfin has no official client extension point, so the plugin adds one `<script>` tag to the web
app's `index.html` at request time through ASP.NET middleware. No file on disk is modified and the
change disappears when the plugin is removed. If the
[File Transformation](https://github.com/IAmParadox27/jellyfin-plugin-file-transformation) plugin
is installed, the plugin registers with it instead so only one component rewrites the page.

The script talks to the plugin's own endpoints:

- `GET /EpisodeContinuity/Items/{itemId}/Continuity`
- `GET /EpisodeContinuity/ClientConfig`
- `POST /EpisodeContinuity/Hello`
- `GET /EpisodeContinuity/client.js`

## Installation

Requires Jellyfin 10.11. Add the repository manifest URL from the releases page under
Dashboard → Plugins → Repositories, install **Episode Continuity**, restart Jellyfin. Settings live
under Dashboard → Plugins → Episode Continuity.

## Development

Open in VS Code and reopen in the devcontainer. It starts a Jellyfin 10.11 server on
http://localhost:8097 with `media/` mounted as `/media` and `dist/plugins` as the plugin directory.

- `scripts/deploy.sh` builds the plugin, copies it into Jellyfin, and restarts the server.
- `dotnet test` runs the unit tests.
- `scripts/e2e/` drives real jellyfin-web in headless Chrome; see its README.
- `scripts/package.sh` produces a release zip and repository manifest.

`spec.md` records the agreed scope, `docs/research.md` the feasibility research and prior art,
`docs/plan.md` the implementation plan.

## License

GPL-3.0-or-later.
