# Technical plan

Target: Jellyfin 10.11 (`Jellyfin.Controller` 10.11.0), .NET 9, existing scaffold in
`Jellyfin.Plugin.EpisodeContinuity/`. Analyzer settings are strict (StyleCop, warnings as errors).

## Project layout

```
Jellyfin.Plugin.EpisodeContinuity/
  Plugin.cs                          existing; adds config page
  PluginServiceRegistrator.cs        registers services, hosted service, IStartupFilter
  Configuration/
    PluginConfiguration.cs           settings (see spec R5)
    configPage.html                  settings UI
  Continuity/
    EpisodeRef.cs                    DTO: Id?, SeasonNumber, EpisodeNumber, Name?, IsVirtual
    ContinuityResult.cs              DTO: MissingBefore, MissingAfter, PreviousAvailable, NextAvailable
    IEpisodeSource.cs                Guid seriesId -> IReadOnlyList<Episode>; abstracts ILibraryManager
    LibraryEpisodeSource.cs          production IEpisodeSource over ILibraryManager
    ContinuityAnalyzer.cs            pure logic: ordering, gap-before / gap-after
    ContinuityService.cs             resolves item -> Episode -> analyzer; formats messages
  Playback/
    PlaybackWarningService.cs        IHostedService; hooks ISessionManager.PlaybackStart; pushes messages
    WebClientRegistry.cs             device ids that have the web script active (heartbeat + expiry)
  Web/
    ScriptInjectionStartupFilter.cs  IStartupFilter middleware rewriting /web/index.html
    FileTransformationBridge.cs      reflection registration with File Transformation plugin
    TransformationPatches.cs         static callback: string IndexHtml(PatchRequestPayload)
    WebResources/client.js           the injected script (embedded resource)
    WebResources/client.css          styles, inlined by client.js
  Api/
    EpisodeContinuityController.cs   REST: Items/{id}/Continuity, client.js, ClientConfig, Hello
Jellyfin.Plugin.EpisodeContinuity.Tests/
  ContinuityAnalyzerTests.cs         gap scenarios
  ContinuityMessageTests.cs          message formatting
  ScriptInjectionStartupFilterTests.cs  middleware with DefaultHttpContext
  WebClientRegistryTests.cs          expiry / suppression
```

## Step 1. Continuity core

`ContinuityAnalyzer.Analyze(IReadOnlyList<Episode> seriesEpisodes, Guid itemId, bool numberingGaps)`:

1. Filter: `ParentIndexNumber > 0`, `IndexNumber != null`. Exclude virtual items with a future
   `PremiereDate`.
2. Sort by `(ParentIndexNumber, IndexNumber)`; on ties prefer the non-virtual item.
3. If `numberingGaps`: for each pair of adjacent available episodes in the same season, synthesise
   `EpisodeRef` entries for integers between `prev.IndexNumberEnd ?? prev.IndexNumber` and
   `next.IndexNumber` that no existing item covers.
4. Locate the target item. Walk backwards collecting virtual/synthesised refs until the first
   available episode: that is `MissingBefore`, `PreviousAvailable`. Walk forwards likewise for
   `MissingAfter`, `NextAvailable`.

`LibraryEpisodeSource` queries
`GetItemList(new InternalItemsQuery { IncludeItemTypes=[Episode], AncestorIds=[seriesId], Recursive=true })`
without an `IsVirtualItem` filter so virtual items are included. No caching in v1; a series query
runs at most twice per episode played.

`ContinuityService.GetForItem(Guid itemId)` returns `ContinuityResult.Empty` for anything that is
not an `Episode` with a `SeriesId`. `ContinuityMessages.SkipWarning(result)` builds header/body
text used by both the server push and the web overlay.

## Step 2. Server push

`PlaybackWarningService : IHostedService` subscribes to `ISessionManager.PlaybackStart` and
unsubscribes on stop. Handler:

- return if disabled, mode `Off`, item not an Episode, or `WebClientRegistry.IsActive(session.DeviceId)`;
- return if `(session.Id, item.Id)` was warned within the last 10 minutes;
- compute continuity; return if `MissingBefore` is empty;
- `Toast`: `SendMessageCommand` with `TimeoutMs`; `Modal`: without; `PauseAndModal`:
  `SendPlaystateCommand(Pause)`, message, then `Task.Delay(N)` and `Unpause`.
- Errors are logged and swallowed; never throws into the session manager.

## Step 3. REST controller

`[Route("EpisodeContinuity")]`:

- `GET Items/{itemId}/Continuity` `[Authorize]` → `ContinuityResult`.
- `GET client.js` `[AllowAnonymous]` → embedded JS, `Cache-Control: no-cache` plus `?v=` in the tag.
- `GET ClientConfig` `[Authorize]` → feature flags and countdown seconds for the script.
- `POST Hello` `[Authorize]` body `{ deviceId }` → marks the web script active for that device.

## Step 4. Web injection

- `ScriptInjectionStartupFilter`: modelled on Jellyfin Enhanced. GET only, path ends with
  `/web/index.html`, `/web/` or equals `/web`; strips `Accept-Encoding`, `Range`, `If-Range`;
  buffers; if 200 text/html and tag absent, inserts `<script plugin="EpisodeContinuity" src="../EpisodeContinuity/client.js?v=…" defer></script>`
  before `</body>`; fixes `Content-Length`, removes `ETag`/`Last-Modified`. Passes through when
  web features are off or `FileTransformationBridge.IsRegistered`.
- `FileTransformationBridge.TryRegister()` in the hosted service start: find an assembly whose
  name contains `Jellyfin.Plugin.FileTransformation`, get `PluginInterface.RegisterTransformation`,
  build the payload with that assembly's own `JObject.Parse` (obtained via the method's parameter
  type, avoiding any Newtonsoft reference in our plugin), invoke. Payload: id = plugin id,
  `fileNamePattern = "index.html"`, callback = `TransformationPatches.IndexHtml`.
- `TransformationPatches.IndexHtml(PatchRequestPayload)` reuses the same tag builder.

## Step 5. client.js

Runs in every browser session; waits for `window.ApiClient` and a logged-in user.

- **Item tracking**: patch `window.fetch` and `XMLHttpRequest.open` to notice
  `/Items/{id}/PlaybackInfo`; record `currentItemId`, prefetch its continuity, and POST `Hello`
  (throttled) so the server suppresses its own toast.
- **Up-next**: `MutationObserver` on `document.body` for an added `.upNextDialog`. If the current
  item's `missingAfter` is non-empty, insert `<div class="ec-upnext-warning">` naming the missing
  episodes and `nextAvailable` above the title. Remember `warnedNextItemId`.
- **Interstitial**: when a PlaybackInfo for an item with `missingBefore` is seen and that item is
  not `warnedNextItemId`, wait for a `<video>` to emit `playing`, pause it, and show a fixed
  overlay with message, countdown, Continue, Go back. Continue/timeout → `video.play()`;
  Go back → `history.back()` (the OSD stops playback on navigation).
- Feature flags and countdown seconds come from `ClientConfig`; all DOM work is defensive and
  wrapped so a failure never breaks the player.

## Step 6. Config page and docs

Add the settings from spec R5 (hidden flag omitted). Update `README.md`, `build.yaml`,
`meta.json` descriptions.

## Step 7. Verification

- `dotnet build` clean under the strict analyzers; `dotnet test` green.
- `scripts/deploy.sh`, flip `TreatNumberingGapsAsMissing` in the plugin's XML config, remove
  `media/Alpha Show/Season 01/Alpha Show - S01E02.mp4`, rescan, then in the browser at
  http://localhost:8097: play S01E01 to the end and confirm the up-next warning; open S01E03
  directly and confirm the interstitial; disable web features and confirm the toast.
