# Episode Continuity plugin: spec

## Original ask (verbatim)

> This is a brand new jellyfin plugin. I want ot create a plugin that warns you that next episodes
> are not available if there's discontinuity in the available episodes. EX: if you have episode 1
> and 3 but not 2, the extension will warn you about that. Come up with some ideas for how to
> surface that to the user that make sense and are doable from a plugin's point of view. My one
> idea is to intercept the autoplay next episode. When when you're nearing the end of your episode
> and it pops up the "next episode in a few secconds" toast, i tshould ahve a warning if the next
> episode is not actually the next one because an episode is missing. I can also image that if you
> finsih episode 1, log off, come back the next day and start episode 3 (and episode 2 is missing)
> then befor eit starts it should give you a warning that you're skipping episode 2 (and when the
> user hits ok or after a timer, then it starts playing as normal)
>
> 1. Look for more ideas about how we can surface this kind of thing to the user that makes sense
> 2. research if these ideas are feasible within the limitaitons of a jellyfin plugin
> 3. Present the research and mroe ideas. Update the spec with the user's feedback
> 4. Write a techncial plan
> 5. execute on the plan.

Research and the full list of candidate surfaces live in `docs/research.md`. The technical plan is
`docs/plan.md`.

## Decisions (2026-09-07)

| Topic | Decision |
| --- | --- |
| v1 surfaces | (1) Web up-next dialog warning, (2) web pre-play interstitial with countdown, (3) server-push warning on playback start for all clients. |
| Not in v1 | Metadata tagging, stop-autoplay-at-gap, detail-page badge, gaps report page, placeholder episodes. |
| What counts as "missing" | **Provider virtual episodes only**: an episode the metadata provider (TVDB/TMDB) knows about that Jellyfin holds as a virtual/missing item. Numbering gaps on disk are *not* used by default. |
| Dev switch | Hidden config flag `TreatNumberingGapsAsMissing` (default off, not on the config page) additionally treats non-contiguous episode numbers within a season as missing, so the synthetic devcontainer library can exercise the feature end to end. |
| Server-push default | Toast only (non-blocking, playback continues). Pause+modal and modal-only are selectable. |
| Web injection | Our own request-time `index.html` middleware by default; when the File Transformation plugin is present we register with it instead so only one component rewrites the page. |

## Functional requirements

### Definitions

- **Ordering**: aired order, i.e. `(ParentIndexNumber, IndexNumber)` across the whole series.
  Specials (season 0) and episodes without an index number are ignored.
- **Available episode**: a non-virtual episode item.
- **Missing episode**: a virtual episode (`LocationType.Virtual`) whose premiere date is not in
  the future. With the dev switch on, also every integer strictly between two adjacent available
  episodes of the same season that no item covers (multi-episode files cover
  `IndexNumber..IndexNumberEnd`).
- **Gap before X**: the run of missing episodes immediately preceding available episode X in
  ordering. May span a season boundary (S01E11 missing, X = S02E01).
- **Gap after X**: the run of missing episodes between X and the next available episode.

### R1. Continuity service and API

- `GET /EpisodeContinuity/Items/{itemId}/Continuity` (authenticated) returns, for an episode:
  `missingBefore[]`, `missingAfter[]`, `previousAvailable`, `nextAvailable`, each entry with
  season number, episode number, name, and id where one exists. Non-episodes return an empty
  result.
- Pure detection logic is unit tested independently of Jellyfin's library manager.

### R2. Server-push warning (all clients)

- On `PlaybackStart` of an available episode that has a gap before it, send the session a
  `DisplayMessage` such as *"Episode 2 (title) is missing from your library. You are skipping
  straight to Episode 3."*
- Modes: `Off`, `Toast` (default, `TimeoutMs` set), `Modal` (no timeout), `PauseAndModal`
  (Pause, modal, Unpause after N seconds).
- Suppressed for a session that has the web script active (the script announces itself), and
  not repeated for the same session and item within a short window.

### R3. Web up-next dialog warning

- When jellyfin-web shows its "Next episode in N s" dialog and the next queued episode is not
  contiguous with the current one, insert a visible warning into the dialog naming the missing
  episode(s) and the episode that will actually play.
- Does not change the dialog's countdown behaviour in v1.

### R4. Web pre-play interstitial

- When an episode with a gap before it starts in the web player, pause it and show an overlay:
  *"You're skipping Episode 2 (title), which is missing from your library."* with a countdown,
  a **Continue** button and a **Go back** button.
- On Continue or when the countdown reaches zero, playback resumes. Go back stops playback and
  returns to the previous page.
- Not shown when the up-next warning was already shown for the same transition (autoplay case).

### R5. Configuration

Config page: Enabled; server-push mode; toast duration; auto-resume seconds; web features
enabled; up-next warning enabled; interstitial enabled; interstitial countdown seconds. Hidden:
`TreatNumberingGapsAsMissing`.

### R6. Web injection

- Script served from `GET /EpisodeContinuity/client.js` (anonymous; it loads before login).
- Injected via `IStartupFilter` middleware that rewrites `/web/index.html` responses in memory;
  idempotent; never breaks the page on error; disabled when web features are off.
- If the File Transformation plugin assembly is loaded, register the same injection with it via
  reflection and let the middleware pass through.

## Non-goals for v1

Per-user "you skipped an episode you *do* have" warnings, Sonarr integration, reports,
metadata changes, non-web client UI.
