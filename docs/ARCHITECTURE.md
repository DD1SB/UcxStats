# Architecture

The 0.12.3.5 Beta milestone is described in the [release notes](releases/v0.12.3.5.md).
Numeric versions remain centrally defined; Beta does not add a second version source.

## Source ownership

UcxLog owns every `.UCX` file. UcxStats opens UCX sources with `FileAccess.Read` and shared read/write access only.

**Non-goal:** UcxStats never modifies UCX files and is not intended to replace UcxLog.

All derived data, statistics, cached coordinates, application settings and later saved queries live in UcxStats' own SQLite database.

## Current data flow (v0.3)

```text
UCX file (read-only)
    |
    v
UcxStats.Core
  - 364-byte reader
  - parser
  - SHA-256 per raw record
  - diagnostics
    |
    +--------------------> WPF QSO table
    |
    v
UcxStats.Geo
  - Maidenhead center coordinate
  - Grid2 / Grid4 / Grid6
    |
    v
UcxStats.Data
  - SQLite
  - idempotent upsert by source path + record index
```

## Planned boundaries

- `UcxStats.Core`: UCX format only; no UI or database dependency.
- `UcxStats.Data`: private SQLite persistence, scoped read-only statistics and query APIs.
- `UcxStats.Geo`: deterministic geographic calculations and later short/long-path geometry.
- `UcxStats.Map`: browser-neutral map-page generation, map settings, statistics panel, external DXCC configuration, normalized GeoJSON serialization and compact statistics/status payloads.
- `UcxStats.Wpf`: local desktop UI.
- Future map: a small Leaflet web surface hosted by UcxStats and consumable from WebView2, browser/projector or OBS browser source.

## SQLite location

Default:

```text
%LOCALAPPDATA%\UcxStats\ucxstats.db
```

The UCX source path is stored as an absolute path. Moving the same log file to another location therefore creates a distinct source identity in the current implementation.

## Station-location context (planned for v0.6)
Distance, bearing and path geometry must not be tied to the computer's current location. A UCX source can have a default station profile (for example a portable contest site), and an individual QSO may override that profile. Derived geometry is therefore keyed by QSO plus station profile.

## DXCC overlay foundation

DXCC and WAE identity share low-level QSO and confirmation data but are separate domains:

```text
QSO ── numeric DXCC ID ──> future 340-entity DXCC overlay
  └── UCX/DCL ADIF2 ─────> existing WAE entity/subdivision resolver
```

No fake DXCC IDs are assigned to WAE-only entities. Existing `APP_DCL_ADIF2` mappings and all `WaeEntityResolver` special cases remain intact.

The implemented map data flow is:

```text
external user-provided dxcc.geojson
              |
              v
     validator / normalizer ------> per-window MapWeb staging
                                             |
                                             v
                                  resident Leaflet GeoJSON
                                             ^
                                             |
SQLite qso -> QsoScope -> entity+band aggregate snapshot
```

Geometry is read-only and external. It is neither bundled nor persisted in SQLite. `IDxccEntityCatalog` supplies independent `Current`, `Deleted`, or `Unknown` classification because geometry presence does not establish current DXCC status. Its complete offline catalog is generated from the Released ADIF 3.1.7 machine-readable DXCC Entity Code TSV dated 2026-03-22. The generated data contains only numeric ID, name, status, and the code-0 sentinel marker; it has no runtime network dependency.

The catalog, UCX mapping, WAE resolver and geometry normalizer are separate layers. UCX mapping version 3 is generated from the official UcxLog 8.50 country entity definitions and validated against—but not generated from—the ADIF 3.1.7 catalog. It covers normal current and deleted UCX entities independently of user log history; source-defined WAE/R150S subdivisions without numeric DXCC IDs remain unresolved. Mapping generation does not create WAE entities or join geometry by name. The known-hash GeoJSON normalization is cross-checked against numeric catalog IDs after normalization.

`MapWindow` carries its exact `QsoScope` independently of `MapQsoPoint`. Marker/path queries still require coordinates, while DXCC aggregation does not; consequently a locator-less QSO can update a polygon. Source maps use source scopes, selected-grid maps use source-record keys, SQL maps use explicit QSO IDs, and Live Map combines source scope with its UTC lower bound.

Each map has a private local `MapWeb/<window-id>` directory behind the existing `https://ucxstats.local` WebView2 mapping. Valid normalized geometry is staged there and fetched once per generated page. JavaScript indexes layers only by numeric entity ID and exposes `ucxSetDxccStatus`, `ucxSetDxccVisible`, and `ucxRefreshDxccStyles`. Band changes run `setStyle()` over the resident layer; they do not re-fetch GeoJSON. DXCC geometry uses a pane below day/night, paths, and markers and is excluded from marker `fitBounds`.

Configuration failure, geometry failure, and status-query failure are isolated from normal map initialization. Each window owns its own visibility flag; `MapSyncService` continues to synchronize view only. Live Map keeps incremental marker insertion and sends a complete compact status snapshot after relevant database refreshes.

General presentation uses `MapSettings` in `map-settings.json`; DXCC path and
default visibility remain in `map-dxcc.json`. `MapPathWidths` preserves the
original 1.3/1.0 px defaults and validates finite 0.5–8.0 px values per field.
`App.MapSettings` initializes one `MapSettingsService`, shared by MainWindow,
dialogs, normal/SQL maps and Live Map. `MapSettingsStore` resolves each field from
valid general settings, valid legacy DXCC widths, then defaults. It atomically
commits complete general settings before removing only legacy width fields from
the DXCC JSON object, retaining all unknown fields. Completed startup migration
does not rewrite either file; fresh installs create neither file until needed.
Both stores flush a same-directory temporary file and atomically replace the
target, skipping unchanged documents. Malformed files are reported and preserved;
interrupted migrations retry safely. The two files do not form one transaction.

WPF's map settings dialog persists general values through that service, which
broadcasts successful changes; open maps execute
`ucxSetPathWidths`, restyling only resident Shortpath/Longpath polylines via
`setStyle({weight:…})`. HTML navigation is unnecessary. The JavaScript `addQso`
function reads current widths for initial, appended and replaced Live Map paths.
See [map documentation](MAP_DXCC.md) for persistence and Windows acceptance checks.


## Scoped map statistics and activity

`IQsoRepository.GetMapStatisticsAsync(QsoScope)` uses one SQLite read with the same
station/cache joins and marker eligibility constant as `GetMapQsoPointsAsync`.
The unfiltered base rows retain QSOs lacking locators or station profiles. It reads
the already resolved numeric DXCC identity, validates it against the existing ADIF
catalog/non-DXCC policy, validates complete locators with `MaidenheadConverter`,
and takes ODX from the effective profile's distance cache. Pure locator normalization is
cached across windows/snapshots in a bounded repository cache; distance is never approximated again.

`QsoStatisticsAccumulator` produces numerically sorted per-band summaries, ODX,
latest valid UTC QSO and distinct-identity membership groups. A membership group
counts identities sharing the same set of bands. Selecting intersecting groups
provides exact arbitrary-band distinct unions without a QSO-level browser payload
or exponential precomputation. Empty band selection is explicit. Large ID/source
key scopes use parameterized SQLite `json_each` sets, avoiding variable/depth limits
while preserving scope intersection and empty-selection behavior.

`ContestTimeEstimator` independently sorts/distincts normalized UTC instants and
classifies whole consecutive gaps strictly greater than the minute threshold as
pauses. It returns gross/countable/pause time, pause count/intervals and optional
allowance balance. It always uses the full map scope. `MapStatisticsPayload`
serializes compact camelCase DTOs with at most 100 pause intervals; retained .NET
timestamps and raw QSO rows are excluded. Changing time preferences reuses those
.NET timestamps without a new database read.

`MapWindow` owns each window's snapshot, cancellation and generation state. It
runs SQLite/aggregation/serialization on workers, checks superseded/closed states,
and sends `ucxSetMapStatistics`, `ucxSetLiveActivity` and
`ucxSetStatisticsSettings` through WebView2. Navigation completion reconstructs the
current scope; relevant Live imports refresh statistics even without new markers.
Statistics/preferences never navigate HTML or reload DXCC geometry. Existing normal
map reuse still rebuilds its page when replacing the map dataset. Markerless normal
and SQL maps are allowed so their QSO statistics remain accessible.

`MapStatisticsPanel` supplies local CSS/JavaScript inside the existing generated
Leaflet page. Controls are independently collapsible/showable; band selection
selects preaggregated groups. Only Live Map enables the one-second browser activity
timer. It calculates UTC elapsed time from the latest scoped QSO on every tick,
clamps future timestamps and labels the last-QSO band. It makes no claim about
actual transmitting, station pauses or current radio tuning. View synchronization
remains unchanged and does not transmit band selections/statistics.

The shared `MapSettingsService` atomically persists panel/contest visibility,
pause threshold, optional allowance and warning/prolonged-gap thresholds in
`map-settings.json`, together with existing ShortpathWidth/LongpathWidth and unknown
fields. Each existing window retains its own panel/contest visibility while saves
establish defaults for new windows; thresholds/allowance broadcast to open maps.
DXCC configuration remains separate. Details and Windows acceptance are in
[MAP_STATISTICS](MAP_STATISTICS.md).


## Map UI refinement and Live timeline

`QsoStatisticsSnapshot.TimestampCountsUtc` retains per-second QSO multiplicities
alongside the unchanged distinct instants used for contest estimates. The existing
single scoped repository query and watcher remain the only data refresh path.
`LiveTimelinePayload` bounds sparse offset/count pairs to 250 minutes past / 240
minutes future at an explicit generation UTC time; it includes latest-QSO details
and a beyond-coverage future count. No full QSO rows are added to browser payloads.

`LiveTimelinePanel` renders an SVG footer in the WebView's flex layout, immediately
above the existing WPF status row. Normal maps create no footer/settings control.
The page's `ucxSetLiveTimeline` replaces timestamp groups and prefix sums after
imports/corrections/deletions. Binary searches select exact counts for fixed UTC
buckets clipped to an inclusive rolling window. Duplicate QSOs retain multiplicity.
A one-second browser timer updates text/status; SVG updates occur only when needed
for time/data/settings/size transitions. UTC wall-clock assumptions and recovery
coverage are explicit; unavailable periods use qualified counts and gray regions.

`MapPanelLayout` re-parents the three Leaflet panels into the map container and
positions them within eight-pixel margins using normalized free-area coordinates.
Panel drag capture belongs only to marked drag handles. Separate collapse buttons and
control event propagation preserve native interactions and map panning elsewhere.
Resize observers clamp positions after viewport/panel-size/fullscreen/DPI changes.
Keyboard arrows move handles and Home resets an anchor. Resetting positions does
not reset visibility or collapse. Only view state enters `MapSyncService`.

General `MapSettings` adds timeline duration/snapping/bucket/visibility/collapse and
panel positions. `WithPresentationFrom` applies a window's private presentation
state over shared calculation/path preferences. Saves establish new-window defaults
without moving/changing existing windows. Slider input previews immediately; valid
preferences use the existing atomic persistence. Layout and live-threshold changes
send settings only; contest threshold/allowance changes recalculate from retained
.NET timestamps. No presentation action queries SQLite or navigates HTML/GeoJSON.
`MapWindow` keeps its cancellation/generation guards for new snapshots and reports
snapshot bytes, serialization and initial/refreshed timeline rendering overhead.


## v0.12.3.5 timeline presentation and threshold safety

Timeline height is an optional preferred CSS-pixel value in general settings.
Its 20–320 px preference is temporarily clamped to the viewport without saving that
clamp. SVG geometry uses the effective chart dimensions, preserving bar ratios,
UTC time coordinates and text size. Presentation derives from effective height:
miniature 20–39, compact 40–99, full 100–320. The resize separator stays focusable;
Up/Down use 10 px, Shift uses 1 px, Home/End select bounds, double-click restores
responsive defaults, and F2 opens/expands the separate settings panel. Karteninhalt
owns the sole timeline visibility checkbox and separate settings visibility.
There is no inline settings button or persisted presentation-mode property.

`LiveThresholdUi` defines fixed warning 1–179 / prolonged 2–180 minute UI limits
and validates the dedicated `liveThresholdPreferences` message before atomic save.
The model's wider 1439/1440 limits remain supported for existing configurations,
with an explicit note and bounded slider projection until an explicit edit.
The native-range feedback loop is removed: slider maxima never depend on thresholds.
Pointer gestures capture on the input, clamp coordinates before conversion, preview
locally and commit a valid pair on release. Cancel/lost capture/Escape/focus loss
restore the latest backend values; settings broadcasts retain active previews.
Crossing warning raises prolonged just to warning + 1; crossing prolonged lowers
warning just to prolonged − 1. No new query, watcher, map navigation or geometry
reload is introduced. Full generated-page and persistence regressions cover this
contract. The release's Windows runtime acceptance is user-reported.

## Transactional UCX source reconciliation

Both MainWindow manual import and Live Map call the existing `UcxImportService`.
The test project links that exact host-independent source file so Linux integration
checks exercise production file parsing/import, rather than a duplicate importer.
The service takes a per-database/source asynchronous gate before reading, compares
complete parser/hash/metadata snapshots across a 250 ms settling interval, builds
geo fields with the existing converter, and invokes `ReconcileSourceAsync` on a
worker. The manual grid uses the exact accepted snapshot, including diagnostics.

The repository validates one source with contiguous zero-based record indices,
reuses hash/upsert logic, removes only absent indices, updates source metadata and
refills effective-profile distance/bearing caches in one immediate transaction.
Foreign keys are enabled on every connection, and cache deletion cascades. A
metadata watermark prevents reuse of deleted QSO IDs without migrating the qso
primary-key layout. Changed DCL matching fields clear only that row's old DCL
match; unrelated confirmation evidence/configuration is retained. Zero-row sources
remain selectable. Incremental `UpsertManyAsync` does not delete missing rows.

The importer supplies final source validation under the writer transaction. A
destructive commit also holds a read-only file lease until commit/rollback;
Windows sharing failures cause rollback and use the existing Live delayed retry.
No transaction partially commits if parsing, validation, SQL/cache work or
cancellation fails. See [DATABASE](DATABASE.md#complete-ucx-source-snapshots-01235)
for stable-snapshot assumptions and dependent data handling.

The existing watcher/debounce/refresh pipeline is retained; Deleted notifications
now report an unavailable source rather than being treated as zero records. Source
and UTC filter are captured per refresh; cancelled/switched-source work cannot
publish stale data. Pending notifications get another pass after failure/cancel.
After success the same scoped marker query, DXCC/statistics query and timeline
payload run. The existing `ucxReplaceQsos`, `ucxSetMapStatistics` and
`ucxSetLiveTimeline` contracts remove deleted geometry/data without HTML navigation,
viewport changes or panel/band/style resets. Latest activity falls back to the
next remaining QSO, and zero rows produce the existing empty state.
