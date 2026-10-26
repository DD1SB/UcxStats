# DXCC map overlay

## External configuration

UcxStats does not distribute DXCC polygon data. Use **Kartenoptionen…** in the main window or map toolbar to select an absolute path to an external `dxcc.geojson`. The setting is stored in `%LOCALAPPDATA%\UcxStats\map-dxcc.json`; `EnabledByDefault` is optional and defaults to `false`.

Selection runs the existing `DxccGeoJsonLoader`. Missing or invalid data is reported in the settings dialog and map status line but never blocks the normal map. Unknown hashes receive general structural validation only; the AmazingProducer corrections are applied exclusively to the known SHA-256 profile.

For each generated page, the validated normalized document is written to that map window's temporary `MapWeb` folder and fetched through the existing `https://ucxstats.local` virtual host. It is not copied to SQLite or the application distribution.

## Shortpath and Longpath widths

The same settings dialog has independent **Shortpath (px)** and **Longpath (px)**
choices: 0.5–8.0 px in 0.5 px increments, with the original **1.3 px Shortpath**
default available as an additional choice. The original Longpath default is
**1.0 px**. Reset selects those exact defaults; Save persists and applies them;
Cancel leaves the saved widths and open maps unchanged. Reset only affects widths.

General presentation is stored separately in
`%LOCALAPPDATA%\UcxStats\map-settings.json`:

```json
{
  "ShortpathWidth": 1.3,
  "LongpathWidth": 1.0
}
```

Missing fields use valid legacy values, then the original defaults, including on
existing installations.
Each field accepts finite JSON numbers in the inclusive range 0.5–8.0; no rounding
is applied. Other valid fractions, such as 1.75, remain available in the dialog
when loaded. Null, strings (including numeric strings), booleans, arrays, objects,
non-finite/overflowed numbers, zero, negative and out-of-range values fall back
independently to valid legacy values or that path's original default. An invalid
DXCC path does not discard otherwise valid widths. DXCC-specific settings remain
in `map-dxcc.json`, for example:

```json
{
  "GeoJsonPath": null,
  "EnabledByDefault": false
}
```

After a successful general save, the shared `MapSettingsService` broadcasts to all
open `MapWindow` instances. Each executes `MapPageBuilder.BuildPathWidthsScript`,
calling `window.ucxSetPathWidths`. This iterates only `b.paths` and `b.longPaths`
and calls each resident Leaflet polyline's `setStyle({weight:…})` when its width
changes, including disabled bands and hidden path groups. Only `weight` changes;
colors, opacity, dash patterns, pane and renderer remain intact. No map HTML is
rewritten, WebView navigated, geometry regenerated, or view/layer state reset.
Navigation completion reapplies the latest saved widths if a save overlaps the
initial page load. Closing a map unsubscribes it from settings notifications.

`addQso` reads the current JavaScript widths for every Shortpath segment and all
five wrapped Longpath copies. Both `ucxAppendQsos` and `ucxReplaceQsos` use it,
including QSOs on new bands. Width updates do not restart the Live Map watcher or
alter its scope. DXCC polygons, QSO/station markers, day/night polygons and any
unrelated layers/graticules are outside the update loop. The current map has no
graticule implementation; these controls do not add one.

`MapPathWidthTests` covers configuration compatibility, round trips, independent
invalid-field fallback, fractions, locale-independent scripts and settings
broadcasts. Its Node.js 18+ harness executes the complete generated JavaScript
with Leaflet/DOM doubles to exercise runtime style changes, resident layer
identity, hidden bands, Live Map additions/new bands/replacements/reset, preserved
map state and untouched unrelated layers. These tests run through
`dotnet test UcxStats.sln`; they do not render WPF or WebView2.

## Configuration migration and recovery

`App.MapSettings` owns one `MapSettingsService`. MainWindow, map settings dialogs,
normal/SQL maps and Live Map share that instance; they do not load their own
copies of general settings. `MapSettingsStore` performs migration once when the
service is initialized. Page generation and runtime updates both read
`Current.PathWidths`; DXCC loading remains in `DxccMapConfigurationStore`.

Each width is resolved independently: a finite number in the inclusive 0.5–8.0
range in `map-settings.json`, otherwise a valid legacy value in `map-dxcc.json`,
otherwise the original 1.3/1.0 px default. A partial valid JSON object is completed
once using these rules, preserving unknown general fields. With neither file, or
DXCC configuration containing no legacy widths, startup creates no general file.
A complete valid general file is not rewritten, even when legacy widths differ.

Migration writes the complete general file before removing only `ShortpathWidth`
and `LongpathWidth` from the old DXCC object. All other known/unknown DXCC fields,
including nested objects and nulls, are preserved. The old file is never deleted.
After cleanup, repeated startups perform reads only. DXCC saves merge their two
known fields into the existing object, preserve unknown fields, and cannot emit
general width properties. The dialog saves general settings first and only saves
DXCC options when those controls changed.

Both stores use a unique temporary file in the destination directory, serialize
and flush the complete document to disk, close it, then replace the target with
`File.Move(..., overwrite: true)`. Parsed equality skips unchanged saves, retaining
the exact existing bytes and modification time. Readers see a complete previous
or new document. A failed general commit leaves legacy values intact; failure
after that commit leaves the new settings authoritative and cleanup retries on
the next startup without rewriting them. Failed writes clean up their temporary
file when possible; a temporary file left by a killed process is ignored, never
loaded or promoted.

Malformed/unreadable JSON is reported in the settings dialog and is not overwritten
automatically. Valid legacy values/defaults can still be used in memory, but DXCC
cleanup waits for a committed complete general file. Repair the malformed file or
move it aside after backing it up before saving that file again. Unknown fields
cannot be recovered from syntactically invalid JSON. Each file replacement is
atomic; there is no transaction covering both files, no arbitration between
concurrent application processes/external editors, and no guarantee against every
filesystem/power-loss scenario. If a DXCC save fails after a successful general
save, widths remain saved/applied and the dialog reports that partial outcome.

`MapSettingsMigrationTests` covers fresh installs, new/legacy/partial settings,
per-field precedence and invalid values, unknown-field preservation, idempotence,
unchanged bytes/timestamps, interrupted writes before/after the general commit,
temporary files and failed-save notifications. Existing generated-JavaScript
tests continue to cover runtime and Live Map behavior.

## Status and interaction

Only ADIF-catalog `Current` entities participate in progress states:

| State | Fill | Outline | Meaning |
|---|---|---|---|
| Not worked | red | dotted | no QSO on an enabled band |
| Worked | yellow | dashed | QSO, no Paper/LoTW/eQSL/ClubLog confirmation |
| eQSL / ClubLog confirmed | light blue | dash-dot | eQSL and/or ClubLog, no Paper/LoTW |
| LoTW / Paper confirmed | green | stronger solid | LoTW and/or received paper QSL |
| Unknown / deleted | gray | long dash, lower opacity | deleted, unknown, invalid, non-DXCC, or status unavailable |

DCL-only confirmation does not promote a polygon. With multiple enabled bands, the highest status wins. With no enabled bands, current entities show Not worked and the legend/popup explicitly says **No bands selected**.

Geometry remains resident and is indexed by normalized numeric DXCC ID. Band changes call `setStyle()`; names and prefixes are display metadata only. Popup band lists reflect the current band selection.

The pane order is base tiles, DXCC polygons, day/night shading, paths, then QSO/station markers and controls. DXCC geometry never contributes to `fitBounds`. View synchronization does not synchronize overlay visibility.

## Live Map and errors

Live Map keeps its existing incremental marker/path behavior. After a source refresh it recomputes and sends one complete compact aggregate snapshot. This happens even if no `MapQsoPoint` was added, so a locator-less QSO can change DXCC status without creating a marker.

No configuration, an empty path, a missing/invalid file, unknown-hash warnings, browser geometry-load failure, or status-query failure leaves the base map and all existing layers operational. Status-query failure renders geometry as non-evaluable rather than claiming entities are unworked.

## World wrapping

The initial implementation renders one correct primary-world vector overlay. Tiles, QSO/station markers, night shading, and long paths retain their existing adjacent-world behavior. Leaflet may therefore show no duplicate DXCC vector copy after panning into a repeated world. This avoids duplicate interactive polygons and fragile antimeridian rewriting; Fiji, Asiatic Russia, Alaska/Aleutians, Pacific entities, and Antarctica should be checked in the primary world on Windows.

## Windows acceptance checklist

1. Open a normal map with no configured GeoJSON; verify the disabled DXCC entry and all existing layers.
2. Use **Kartenoptionen…**, select a valid `dxcc.geojson`, and verify the validation summary.
3. Open a map and enable **DXCC status**.
4. Verify the known normalized data displays 340 current numeric entities.
5. Compare red/yellow/light-blue/green entities with known worked and confirmed examples.
6. Toggle bands and verify immediate restyling without a page reload or view change.
7. Clear every band and verify **No bands selected** behavior.
8. Open an entity popup and verify ID, name/prefix, status, QSO count, and band lists.
9. Add or select an in-scope QSO without a locator and verify it can affect entity status without adding a marker.
10. Enable day/night shading and verify both layers remain readable.
11. Toggle short and long paths and verify their appearance and popups remain unchanged.
12. Open synchronized maps and verify only pan/zoom is synchronized.
13. Disable single-window mode, open multiple maps, and verify independent DXCC visibility.
14. Enable single-window mode and reuse the map; verify window position/size/fullscreen behavior.
15. Run Live Map, log a new QSO, and verify marker behavior plus status refresh; also test a locator-less QSO.
16. Inspect date-line/Pacific entities in the primary world, including Fiji and Alaska/Aleutians.
17. Restart UcxStats and verify the external path persists.
18. Rename the configured GeoJSON temporarily and verify graceful degradation with the normal map intact.

## Windows width acceptance procedure

1. Back up `map-settings.json` and `map-dxcc.json`. With both absent, open normal
   and Live maps: Shortpath must be 1.3 px, Longpath 1.0 px. Confirm startup alone
   does not create either file. Repeat with DXCC options only and no width fields.
2. Open two maps. Enable Longpath and day/night, select some bands, hide others,
   enable DXCC if configured, pan/zoom and enter F11 fullscreen on one map.
3. In **Kartenoptionen…**, choose Shortpath 2.5 px and save. Verify both open
   maps change immediately, including hidden paths when re-enabled. Verify
   Longpath remains 1.0 px, all view/visibility/band/fullscreen/DXCC states remain,
   and no WebView navigation or HTML rebuild occurs (DevTools/host debugger).
4. Change only Longpath to 3.5 px and save. Verify Shortpath stays 2.5 px and
   Longpath remains dashed. Check DXCC borders/popups, QSO/station marker borders,
   and night shading have not changed. Repeat with no DXCC resource configured.
5. While Live Map monitoring continues, append a synthetic QSO, then another on
   a new band; verify current widths on every segment and wrapped Longpath copy.
   Modify/delete a synthetic source record to trigger replacement and verify the
   same widths and retained view, bands, layer visibility and monitoring state.
6. Reset widths and save: verify 1.3/1.0 px immediately and on later Live additions.
   Change controls then Cancel: verify no applied or persisted change.
7. Close/reopen the application and verify both saved widths persist. With the
   app closed, try 1.75/2.25 in `map-settings.json` and verify exact values survive open/save.
   Try invalid values independently (zero, negative, string, null, excessive) and
   verify only that width defaults while the valid width and DXCC settings survive.
8. Exercise save during initial map loading, then close/open several maps and
   save again to check initialization and subscription cleanup. Restore the backup.
9. In a disposable configuration, place widths plus known/unknown DXCC fields in
   `map-dxcc.json` with no general file. Start: verify migrated values, preserved
   DXCC fields and removed legacy widths. Restart: verify unchanged bytes/times.
   Repeat with complete/partial/invalid general values and verify per-field
   precedence. Repeat with a locked/read-only destination and verify the legacy
   file survives an unsuccessful general commit, then retries after unlocking.

Record these on Windows; Linux build/tests/publish do not establish WPF/WebView2
runtime behavior or visual pixel rendering.


## Optional statistics panel

The independent [QSO statistics panel](MAP_STATISTICS.md) uses the same `QsoScope`,
effective numeric DXCC identity and band controls as this overlay. Its distinct
entity totals include recognized current/deleted identities; polygon status styling
retains its existing current-entity policy. Locator-less QSOs remain included in
statistics and can update Live activity. The panel also provides Maidenhead unions,
station-profile ODX and optional full-scope contest-time estimates. It works without
DXCC geometry, does not fetch/rebuild geometry on updates, and stores preferences
only in general `map-settings.json`, preserving path widths and DXCC configuration.
Follow its separate Windows acceptance checklist alongside the overlay/width checks.
