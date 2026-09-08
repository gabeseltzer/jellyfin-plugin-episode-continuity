# Jellyfin Episode Continuity plugin

> **Disclaimer: this plugin was vibe-coded with AI assistance.** The design, code, tests and
> documentation were produced in a conversation with an AI coding assistant, reviewed and tried
> out by a human, but not audited line by line. It works on the author's setup. Read the code
> before trusting it with anything you care about, and please open an issue if something breaks.

Warns viewers when the episode about to play is **not** the one that follows the previous
episode, because episodes in between are missing from the library. Have S01E01 and S01E03 but not
S01E02? Autoplay in Jellyfin silently jumps from 1 to 3. This plugin makes that visible.

## What it does

The plugin detects the gap on the server and warns at playback time, in three places.

### Warning in the "Next episode" countdown (web)

When jellyfin-web shows its autoplay countdown and the queued episode is not the next one, the
dialog says which episodes are missing and which one will actually play.

![Up-next countdown with a missing-episode warning](docs/screenshots/upnext.png)

### Pre-play interstitial (web)

Starting an episode that skips missing ones holds the video and shows a dialog with a countdown,
**Continue** and **Go back**.

![Interstitial dialog before an episode that skips a missing one](docs/screenshots/interstitial.png)

### Server warning (all clients)

For clients without the web script, the server pushes a warning to the session that started
playback. It can be a toast, a dialog with an OK button, or pause plus dialog plus timed resume.
Best effort: some clients ignore display messages.

| Toast | Dialog |
| --- | --- |
| ![Server toast](docs/screenshots/server-toast.png) | ![Server dialog](docs/screenshots/server-dialog.png) |

A browser only ever sees one of the two: while the interstitial is enabled it replaces the server
warning in the web client.

### What counts as missing

"Missing" means an episode the metadata provider (TVDB, TMDb, ...) knows about that Jellyfin holds
as a virtual item, so shows need a metadata match. A hidden setting,
`TreatNumberingGapsAsMissing`, additionally treats non-contiguous episode numbers on disk as
missing; it is meant for development against unmatched libraries.

## Settings

Dashboard → Plugins → Episode Continuity.

![Settings page](docs/screenshots/settings.png)

| Setting | Default | Meaning |
| --- | --- | --- |
| Enabled | on | Master switch. |
| Server warning style | Toast | Off, toast, dialog, or pause + dialog + resume. |
| Toast duration | 8000 ms | How long the toast stays up. |
| Resume after | 10 s | Delay before unpausing in pause mode. |
| Cooldown between repeat warnings | 60 s | Do not warn the same session about the same episode again within this window. 0 warns on every start. |
| Inject the web client script | on | Required for the two web features. Restart Jellyfin after changing. |
| Warn in the "Next episode" countdown | on | Web up-next warning. |
| Show the web interstitial | on | Web pre-play dialog. Suppresses the server warning for that browser. |
| Interstitial countdown | 10 s | Seconds before the interstitial continues on its own. |

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

Requires Jellyfin 10.11.

1. Dashboard → Plugins → Repositories → **+**, and add the repository manifest URL from the
   [releases page](../../releases).
2. Dashboard → Plugins → Catalog, install **Episode Continuity**, restart Jellyfin.
3. Hard-refresh any open browser tab so it picks up the injected script.

To install by hand instead, unzip the release into a new folder under Jellyfin's `plugins`
directory and restart.

## Development

Open in VS Code and reopen in the devcontainer. It starts a Jellyfin 10.11 server on
http://localhost:8097 with `media/` mounted as `/media` and `dist/plugins` as the plugin directory.

- `scripts/deploy.sh` builds the plugin, copies it into Jellyfin, and restarts the server.
- `dotnet test` runs the unit tests.
- `scripts/e2e/` drives real jellyfin-web in headless Chrome; see its README. `screenshots.js`
  in the same folder regenerates the images in `docs/screenshots/`.
- `scripts/package.sh` produces a release zip and repository manifest.

`spec.md` records the agreed scope, `docs/research.md` the feasibility research and prior art,
`docs/plan.md` the implementation plan.

## License

[GPL-3.0-or-later](LICENSE).
