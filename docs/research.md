# Episode Continuity: research notes

Research done 2026-09-07 against Jellyfin 10.11 (the version this devcontainer runs) and current
jellyfin-web master.

## 1. What a Jellyfin plugin can and cannot do

Plugins are server-side .NET assemblies. There is **no official client-side extension point**; a
2023 proposal for generalized client UI customization was rejected by the core team
(jellyfin-meta discussion #34). Everything below is either server-side or a documented workaround.

### Server-side hooks available (verified in `MediaBrowser.Controller` 10.11.0)

| Hook | What it gives us |
| --- | --- |
| `ISessionManager.PlaybackStart` / `PlaybackProgress` / `PlaybackStopped` | `PlaybackProgressEventArgs` with `Item` (BaseItem), `Session`, `Users`, `PlaybackPositionTicks`, `IsPaused`, `PlaySessionId`. Web client reports progress about every 10 s. |
| `ISessionManager.SendMessageCommand` | Pushes `DisplayMessage` to a session. jellyfin-web shows a **toast** when `TimeoutMs` is set, and a **blocking alert dialog with OK** when it is not. |
| `ISessionManager.SendPlaystateCommand` | Pause / Unpause / Stop / Seek on the client. |
| `ISessionManager.SendPlayCommand` / `SendBrowseCommand` | Start something else or navigate the client. |
| `ILibraryManager` | Query episodes of a series/season with `IndexNumber`, `IndexNumberEnd`, `ParentIndexNumber`, `LocationType.Virtual` (provider-known missing episodes). `ItemAdded` / `ItemRemoved` / `ItemUpdated` events for cache invalidation. |
| `IUserDataManager` | Per-user played state (for "you skipped an episode you *do* have"). |
| `IPluginServiceRegistrator` | Register singletons, `IHostedService`, and `IStartupFilter` (ASP.NET middleware). |
| `ControllerBase` | Plugin REST endpoints under any route. |
| `IScheduledTask`, `IHasWebPages` | Scheduled scans, config and dashboard pages. |

Client support for pushed commands varies. Web supports all of them. Android TV historically did
not display `DisplayMessage` (jellyfin-androidtv issues #3428, #5257). Treat server-push as
"best effort, works on web and some others".

### Getting code into the web client

Three precedents, all used by shipping plugins:

1. **Request-time middleware** (Jellyfin Enhanced, `ScriptInjectionStartupFilter`): register an
   `IStartupFilter` via the service registrator, buffer the `/web/index.html` response, insert a
   `<script src="/EpisodeContinuity/client.js">` before `</body>`. No disk writes, survives
   jellyfin-web updates, works on 10.11 and 12. Strips `Accept-Encoding`/`Range`, drops
   `ETag`/`Last-Modified`.
2. **File Transformation plugin** (IAmParadox27): optional dependency; register a transformation
   by locating its assembly in `AssemblyLoadContext.All` and invoking
   `PluginInterface.RegisterTransformation(JObject)` with `{id, fileNamePattern,
   callbackAssembly, callbackClass, callbackMethod}`. Reflection because plugins live in
   separate load contexts.
3. **Rewrite `index.html` on disk** (Intro Skipper legacy): needs a writable web dir, breaks in
   rootless Docker, wiped on update. Not recommended.

Injected JS has access to the global `ApiClient` (authenticated), the DOM, and can call our
plugin's REST endpoint. It does **not** get module access to `playbackManager`; the current
item id is obtained by hooking `XMLHttpRequest`/`fetch` for `/PlaybackInfo` or `/Sessions/Playing`
calls (Intro Skipper's approach) or by polling `ApiClient.getSessions`.

### How jellyfin-web decides what plays next (why the problem exists)

`playbackmanager.js` builds the queue with
`getEpisodes(SeriesId, { IsVirtualUnaired: false, IsMissing: false, startItemId, limit: 100 })`.
Missing episodes are filtered out, so autoplay silently jumps E1 → E3. The up-next dialog
(`components/upnextdialog/upnextdialog.js`) is created by the legacy video OSD
(`apps/legacy/controllers/playback/video/index.js`, `showComingUpNextIfNeeded`) when the
episode is at least 10 min long and remaining time hits the user's threshold, and is filled from
`playbackManager.nextItem()`. Useful DOM hooks:

- container `.upNextContainer` → dialog `.upNextDialog`
- `h2.upNextDialog-nextVideoText` ("Next episode in N s" / "Up next")
- `h3.upNextDialog-title`, `.upNextDialog-mediainfo`
- buttons `.btnStartNow`, `.btnHide`

Note: jellyfin-web master also has a new `apps/modern/routes/video` player. Selectors above are
for the legacy OSD shipped with 10.11; the injected script should feature-detect.

## 2. How Jellyfin already models "missing"

When the metadata provider (TVDB/TMDB) lists an episode that is not on disk, the server creates a
**virtual episode** (`LocationType.Virtual`, `IsMissing`). The user display setting "Display
missing episodes within seasons" shows these greyed out. This only works when the provider knows
the episode; unmatched shows or shows with no provider get nothing.

For our purposes there are two complementary signals:

- **Structural gap**: episode numbers on disk within a season are not contiguous
  (E1, E3 → E2 missing). Needs no provider. Cannot see trailing gaps (E4+) or a whole missing
  season boundary with certainty.
- **Provider gap**: a virtual episode sits between two real ones (or after the last real one).
  Covers trailing episodes and cross-season boundaries when metadata is matched.

Edge cases to handle: multi-episode files (`IndexNumber`..`IndexNumberEnd`), specials
(season 0, excluded), episodes without an `IndexNumber` (skip), season boundaries (S01E10 →
S02E01 is contiguous unless a virtual S01E11 exists), absolute numbering for anime.

## 3. Prior art

- **Plex** feature request "Missed Episodes Warning" (2016): exactly this idea, referencing an old
  Kodi/XBMC add-on that warned when you watched E6 then E8. Closed as a duplicate of "show missing
  seasons and episodes"; never implemented.
- **Jellyfin plugins** Mind the Gaps, MissingEpisodesJellyfin, jellyfin-missing-episode-plugin:
  all produce *reports* (dashboard pages, optional placeholders, Sonarr links). None warn at
  playback time.
- **Jellyfin native**: virtual missing episodes + display setting (above).
- **UI-injection precedents**: Intro Skipper, Jellyfin Enhanced, JavaScript Injector, File
  Transformation.

Nothing found that intercepts autoplay or warns before starting an episode. That is the novel part.

## 4. Candidate surfaces

| # | Surface | Where it works | Feasibility | Notes |
| --- | --- | --- | --- | --- |
| A | **Up-next dialog warning**: rewrite the dialog text to "Episode 2 is missing. Next available: Episode 3" | Web only | Medium: needs injected JS, MutationObserver on `.upNextDialog`, one call to our REST endpoint | The user's primary ask. |
| B | **Pre-play interstitial**: when an episode with a gap before it starts, pause and show a dialog with countdown + Continue/Cancel | Web only | Medium: injected JS; detect start via `/PlaybackInfo` hook; use `ApiClient` to look up gap; overlay dialog; auto-continue after N s | The user's second ask. |
| C | **Server-push warning**: on `PlaybackStart`, if a gap precedes the item, `SendMessageCommand` to that session (toast or modal). Optionally `Pause` then `Unpause` after N s | All clients, best effort (web good, Android TV poor) | Easy: pure server side | Zero-injection fallback; also fires when autoplay lands on E3 so it doubles as an autoplay warning. |
| D | **Stop autoplay at a gap**: injected JS intercepts the `getEpisodes` response and truncates the queue at the first gap | Web only | Medium | Optional mode: "don't autoplay across a gap". |
| E | **Metadata marking**: add a tag (`Missing previous episode`) and/or prefix the episode Overview with a warning line; also tag the Season/Series as incomplete | All clients, in lists and detail pages | Easy–medium: `ILibraryManager` updates on library change events + scheduled task; must be reversible | Visible everywhere without any injection. Slightly invasive to metadata. |
| F | **Detail-page badge**: injected JS adds a warning chip on the episode/season page | Web only | Easy once A/B infra exists | |
| G | **Gaps report page + scheduled scan** | Dashboard | Easy | Well-covered by existing plugins; low priority. |
| H | **Placeholder virtual episodes** for structural gaps | All clients (with display setting on) | Medium; Mind the Gaps does this and has to garbage-collect them itself | Would make Jellyfin's own UI show the hole. Does not change autoplay. |

## 5. Recommendation

Layer it:

1. **Core** (server): `GapDetector` service over `ILibraryManager` combining structural and
   provider gaps, cached per season and invalidated on library events; REST endpoint
   `GET /EpisodeContinuity/Item/{id}` returning `{ hasGapBefore, missing: [...], previousAvailable,
   nextAvailable, nextIsContiguous }`; unit-testable with fakes.
2. **Server-push** (C) for every client, configurable: off / toast / modal / pause+modal.
3. **Web overlay** (A + B, later D/F) as an injected script served from our controller, injected via
   `IStartupFilter` middleware, with File Transformation used when present. When the script is
   active on a web session it tells the server to suppress C for that session to avoid double
   warnings.
4. **Optional** (E) tagging, off by default.
5. Skip G and H for v1; existing plugins cover them.
