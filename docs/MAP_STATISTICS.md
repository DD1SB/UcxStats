# Map statistics and Live QSO activity

This feature is included in **0.12.3.5 Beta**, following public 0.12.3.4.
The source deletion reconciliation defect is fixed by the transactional import
path documented below. Current Beta verification and final packaging paths are in
the release notes. New native Windows import checks remain pending.
The user has accepted the latest Windows runtime tests, including the timeline,
compact/miniature modes, draggable panels, adjustable height and threshold overflow
fix. That acceptance is user-reported; the Linux checks here do not execute
WPF/WebView2. See [release notes](releases/v0.12.3.5.md) and the
[installer guide](WINDOWS_INSTALLER.md) for final packaging and install checks.

Intermediate 0.12.3.4 artifact names and stage-specific pending checks below are
historical development records, superseded by this milestone acceptance. The
acceptance checklists remain useful for future regressions. Final 0.12.3.5
verification and packaging are recorded in the release notes.

Enable **QSO-Statistik** in Leaflet's existing **Karteninhalt** control. A compact
panel defaults to bottom left and can be dragged by its ⠿ header. Its main summary and band table follow the existing
band checkboxes. Collapse it with its header arrow, or hide it with the
checkbox. It uses local HTML/CSS/JavaScript and needs no additional UI framework.
The panel alone stops clicks and scrolling; the rest of the map remains interactive.
It supports the existing windowed, second-monitor and F11 fullscreen maps.

## Exact scope

Statistics query SQLite using the map's `QsoScope`, independently of markers:

| Map entry | Included logical QSO rows |
| --- | --- |
| All-QSO map | Every row in `qso` |
| Selected sources | Rows whose `source_file` belongs to the selected sources |
| Selected QSO grid rows | Exact `(source_file, source_record_index)` keys |
| SQL result | Distinct database IDs from `qso.id AS qso_id` in the result |
| Live Map | Selected source intersected with its inclusive UTC start date/time |

When a scope contains several restrictions they intersect. An empty explicit
source/key/ID selection matches nothing. Repeated selections do not multiply rows.
QSO identity remains the repository's source path plus source-record index; the
persisted database ID identifies selected/SQL rows. Two imported sources remain
two sources even if their log contents resemble one another. SQL results do not
create new QSOs or change identity. `QsoScopeSql` uses JSON selection parameters
to avoid SQLite expression-depth and variable limits for large selections.

Each window owns its scope, snapshot, cancellation source and generation guards.
Reusing a normal map replaces its scope. Independent map windows retain their own
statistics and band selections. Map synchronization continues to transmit view
only. Thresholds and allowance are shared through the existing settings service.
Panel/contest visibility belongs to each open window; saving it establishes the
default for subsequently opened windows without toggling other open panels.
Normal and SQL maps can now open with zero markers so locator-less selections
still have a statistics panel.

## Counts and bands

- **QSOs gesamt:** all logical scope rows on the selected bands, including missing
  or malformed remote locators and missing station profiles.
- **Kartierbare QSOs:** rows satisfying the exact shared marker-query predicate:
  persisted remote latitude and longitude exist, and the effective station profile
  exists. This uses the importer's existing locator validation and coordinates;
  non-empty locator text alone is insufficient. Multiple wrapped-world Leaflet
  markers for one QSO still count as one QSO. A missing distance cache entry does
  not remove an otherwise mappable QSO.
- **Nicht kartierbar:** total minus mappable QSOs.
- **DXCCs:** distinct effective numeric `dxcc_entity_id` values recognized as
  current or deleted in the bundled ADIF 3.1.7 catalog. The existing UCX/DCL policy
  resolves conflicts; explicit ADIF 0/DCL 999 non-DXCC evidence suppresses an
  identity. Unknown/unresolved/invalid IDs are excluded. Callsign prefixes are
  never used as identity evidence. Deleted identities can count here even though
  the DXCC polygon overlay's status policy marks deleted entities unknown.
- **Großfelder:** distinct first two characters of a completely valid Maidenhead
  locator, trimmed and uppercased by the existing converter's rules.
- **Grids:** distinct first four characters of a completely valid locator with at
  least four characters. Valid two-character locators count only as fields;
  four/six/eight/ten-character locators require no six-character minimum.
  Malformed suffixes invalidate the complete locator, including its prefixes.

All scope bands are available in the existing controls even when a band has no
mappable QSO and DXCC geometry is unavailable. Numeric UCX band order covers
160 m through 10 GHz, including VHF/UHF/microwave. Unknown stored codes retain the
existing fallback label and numeric ordering. The combined summary and expanded
band table update immediately. No bands selected means zero counts and **Keine
Bänder ausgewählt**, never an implicit all-bands selection. To see all bands,
select all represented band checkboxes.

QSO counts add across disjoint bands. DXCC/field/grid counts use exact distinct
unions: an identity present on 20 m and 40 m counts once when both are selected.
.NET groups distinct identities by their band membership sets; the compact JSON
contains the count of identities in each group. The browser selects intersecting
groups and adds each group's count once. It receives no raw QSO list or per-QSO
timestamps for statistical calculation.

## ODX

ODX is the maximum finite, nonnegative cached `qso_geo_cache.distance_km` joined
on `(qso_id, effective station_profile_id)`. An individual QSO's station override
wins over its source's profile, exactly as in the marker query. The cache retains
the repository's geodesic definition (great-circle distance, mean Earth radius
6371.0088 km); statistics do not recalculate distance or choose a global home QTH.
Missing distances show **nicht verfügbar**. Equal distances choose the lowest
persisted QSO ID, including across bands. Details show callsign, normalized remote
locator, band and UTC timestamp when valid. Each band's expandable ODX details
use the same rule. Distances use the profile's authoritative coordinates, including
manual coordinates when configured; locator precision and cache availability
limit the result.

## Contest time estimate

**Contestzeit anzeigen** explicitly enables the normally hidden contest section.
It always uses the full map QSO scope, independently of visible bands. This is
intended for contest logs; annual-log results are usually less useful.

The estimator parses the stored date/time as UTC, excludes invalid/missing
values (reporting their count), sorts chronologically, and collapses duplicate
instants. A single timestamp or empty log has zero elapsed/countable/pause time;
an empty log has no first/last timestamp. First/last remain UTC across midnight,
overnight, multi-day sessions and local daylight-saving changes.

The default pause threshold is **60 minutes**, configurable in whole minutes from
5 through 180. For consecutive distinct timestamps, a gap **strictly greater than**
the threshold counts entirely as a recognized pause. A gap equal to the threshold
counts entirely toward estimated operating time. Gross time is last minus first;
estimated pause time sums pause gaps; estimated counted time is gross minus pause
time. The count covers all recognized pauses; the expandable browser list shows
at most the first 100 intervals to bound annual-log payloads.

An optional **Zeitbudget** accepts a positive whole-minute allowance (up to one
year); leave blank for no limit. The panel shows the configured budget and remaining
or exceeded estimated time. No contest duration, 36-hour allowance or ruleset is
assumed. Time does not accumulate after the last logged QSO.

These are **QSO-derived estimates**, not measured transmitter-on time or guaranteed
contest-rule compliance. An operator can actively call CQ without logging a QSO.
This milestone does not implement contest-specific pause or operating-time rules.

## Draggable panels and sliders

**Karteninhalt**, **QSO-Statistik** and Live Map's **Live-Verlauf – Einstellungen**
are independent draggable panels. Drag the marked ⠿ header, or focus that handle
and use arrow keys; Home/Pos1 restores that panel's default anchor. The separate
header arrow collapses the body. Checkboxes, buttons, sliders and disclosure
controls keep their own interactions. Control keyboard events do not pan the map.
Pointer/focus interaction raises a panel above overlapping panels; Tab can reach
its header. Pointer capture keeps a drag attached to its handle; canceling a drag restores its
previous position.

Positions use normalized free-area coordinates between eight-pixel viewport
margins, so right/bottom alignment survives changes in panel size, window size,
fullscreen, monitor or CSS-pixel/DPI scale. Resize observers re-clamp panels and
scroll their bodies while keeping headers reachable. Invalid coordinates restore
default anchors; finite offscreen values clamp into [0,1]. **Panelpositionen
zurücksetzen** in Karteninhalt resets only positions, retaining visibility and
collapse state. Positions and panel collapse states persist in general settings.

Each existing window owns its presentation state, including positions, duration,
bucket size, snapping, visibility and collapse. A save establishes defaults for
new windows. Existing windows retain their own layout; pan/zoom synchronization
never copies panel settings. Normal single-window map reuse preserves the saved
presentation state when its dataset changes.

Contest pause selection now uses a horizontal **5–180 minute** slider with
one-minute precision and a numeric value. Its strict gap comparison and full-scope
contest calculation are unchanged. Live warning/prolonged thresholds live only in
the timeline settings panel. Moving warning up to/beyond prolonged advances
prolonged to warning + 1; moving prolonged down to/below warning reduces warning
to prolonged − 1. Both displayed values update before persistence. The sliders
have fixed UI limits: **Warnung 1–179 minutes**, **Sehr lange Lücke 2–180 minutes**,
with one-minute precision and defaults **10/15 minutes**. Their maxima never grow
while dragging. The wider configuration bounds remain
`1 <= warning < prolonged <= 1440`; see the overflow fix below for existing values
outside the UI range. Keyboard arrows give one-minute precision. Threshold changes
apply without HTML navigation.

## Live QSO timeline

Only Live Map shows **Live-QSO-Verlauf**, a shallow full-width footer immediately
above the existing WPF status line. It replaces the embedded activity card in the
general statistics panel. The timeline and its draggable settings panel can be
shown/hidden separately in Karteninhalt. Collapsing or hiding timeline settings
does not hide the histogram. Karteninhalt's **Verlauf-Einstellungen** checkbox is
the primary show/hide control for the separate settings panel. **F2** on the
focused resize strip opens and expands that panel and focuses its duration slider.
There is no inline settings button in any presentation mode.
Normal and SQL maps do not create a timeline or its settings panel.

The left edge is the oldest visible UTC time; the right edge is **Jetzt**. Bar
height is the number of scoped QSOs in that UTC bucket. Green/teal bars represent
QSOs; contiguous empty buckets share a subtle amber band. Isolated short empty
buckets use very low emphasis. Bands gain emphasis at the configured warning and
prolonged thresholds. A dashed line marks the current ongoing interval since the
last QSO, using its actual timestamp even inside a nonempty bucket. Tooltips give
bucket boundaries, QSO counts and partial-bucket indications. These are **QSO gaps,
not confirmed operator inactivity, transmitter-off time or operating pauses**.

The footer includes elapsed time since the latest valid scoped QSO, warning state,
callsign, **Band des letzten QSOs**, and visible-window QSO count. The timestamp is
available in the activity tooltip. This never identifies the currently tuned band.
Elapsed time clamps to zero for future timestamps and explicitly notes the clock
discrepancy. Missing timestamps have an unavailable state. Duplicate latest times
retain the existing lowest-QSO-ID tie rule.

### Adjustable timeline height

Drag the thin marked strip along the timeline's upper edge upward to enlarge the
histogram, or downward to reduce it. The preference uses whole CSS pixels from
**20 to 320 px**. The existing responsive default remains **148 px**, or **174 px**
when the viewport is at most 750 px wide, until a height is explicitly chosen.
Double-click the strip to return to that responsive default. The existing
**Verlauf zurücksetzen** action also restores the default height.

The strip is a focusable horizontal separator. Tab to it, then use **Up/Down in
10 px steps**, or **Shift+Up/Down in 1 px steps**. **Home/Pos1** selects 20 px and
**End/Ende** selects 320 px. Its accessible value reports both the displayed height
and a larger preferred height when the window temporarily constrains it. Pointer
capture keeps resizing attached to the strip; cancellation restores the prior
preference. Pointer previews remain local and save once on release; keyboard
changes save immediately through the existing atomic settings service. **F2**
opens and focuses the separate settings panel in every presentation mode.

`LiveTimelineHeightPx` in general `map-settings.json` stores the preferred height.
Missing/null values retain the responsive default; non-integer/invalid types also
fall back to it. Integer values outside the supported range clamp to 20–320. Existing saved heights (including 80 px) remain valid and are
loaded without rewriting the file.
Each open window retains its own presentation preference, while saving establishes
the default for subsequently opened windows.

The effective height additionally leaves 80 px of map area whenever space allows.
If the WebView is too short, the footer can temporarily shrink below 20 px, without
saving that reduced value. Enlarging the window or entering fullscreen restores
the preferred size automatically. The initial HTML reserves the saved height before
Leaflet's first fit; subsequent resizing uses only DOM/SVG and Leaflet size updates,
without HTML/WebView2 reloads or database queries. Map center/zoom, duration, bucket
size, data and normalized floating-panel positions remain unchanged. Floating
panels are temporarily re-clamped to stay reachable in the remaining map area.

The SVG uses its actual rendered dimensions and keeps bar-height ratios exact.
Time positions remain linear in UTC, and text is never stretched to fit a mode.
Presentation follows the **effective rendered height**, not the preferred setting:

| Effective height | Presentation |
| --- | --- |
| 20–39 px | Miniature: histogram/gap bands only; no header, activity text, button, legend or axis labels; zero padding |
| 40–99 px | Compact: histogram and two minimal UTC time labels; no header, activity text, button or legend |
| 100–320 px | Full: title, activity details, legend and axes |

Miniature bars use the whole available SVG height (apart from the footer's one-pixel
border). The resize strip overlays the upper edge, consumes no layout height, and
reveals its grip on hover/focus. Its Tab stop, resize keys and **F2 settings access**
work in all modes. Removing the inline button leaves more horizontal space for
counts and activity details; the tight header uses two columns without an empty
button column. SVG accessible names and
nonvisual tooltips retain QSO counts and qualify incomplete clock-recovery data.

The presentation mode is derived and is never persisted. Below 20 px in an
exceptionally constrained viewport the miniature presentation remains in use;
the saved preference is retained and restored with more space. The prior
responsive 148/174 px defaults and double-click reset are unchanged.

Timeline visibility has one control: **Live-QSO-Verlauf** in Karteninhalt, backed
by the existing `LiveTimelineVisible` property. The redundant **QSO-Verlauf
anzeigen** checkbox has been removed from the settings panel. **Verlauf
zurücksetzen** resets duration, raster, height and thresholds while preserving
visibility. Showing/collapsing the settings panel remains independent of showing
the timeline; it uses the existing settings-panel properties.

### Duration, snapping and bucket boundaries

Duration defaults to **60 minutes**, with a continuous **15–240 minute** slider in
one-minute steps. Optional hour snapping defaults on: while dragging, values within
±3 minutes of 60/120/180/240 preview that hour value. The raw thumb keeps moving;
dragging outside the zone immediately releases the snap. On release the thumb
settles on the selected value. Keyboard adjustments bypass snapping for deliberate
minute precision. Disabling snapping gives exact pointer values. The maximum
remains 240 minutes.

Choose **1, 2, 5 or 10 minute** buckets, default **5**. Bucket boundaries are fixed
multiples of that interval since the Unix UTC epoch; changing duration never moves
those boundaries. The rolling window includes timestamps from `now-duration`
through `now`, inclusive. Each fixed bucket is half-open `[bucketStart,bucketEnd)`;
a QSO exactly on a boundary belongs to the newer bucket once. Partial first/last
buckets count only timestamps inside the visible window. A QSO exactly at Jetzt
is included, with a narrow right-edge bar when the last bucket has zero visible
width at that instant. Counts never come from summing previously rounded buckets.
Band visibility does not affect this full Live source/UTC-start scope.

### Data, clock and refresh contract

The existing source/start-UTC `QsoScope`, repository query and FileSystemWatcher
remain authoritative. No second watcher or once-per-second database query is
introduced. Initial load and relevant imports reconstruct both statistics and
histogram, including locator-less QSOs. Corrections, deleted/replaced records and
out-of-order imports replace the compact series; they do not append stale counts.
Map viewport, layers, band selection, positions and presentation settings survive.
Restart/reconnection reconstructs the latest QSO and series from SQLite.

The statistics accumulator retains **multiplicity per UTC second**, separately
from the contest estimator's unchanged distinct timestamps. `LiveTimelinePayload`
transfers sparse `[secondOffset, count]` pairs, not QSO records. Coverage extends
250 minutes into the past (four hours plus ten minutes of clock recovery) and
240 minutes into the future, with at most **29,401 timestamp groups / 58,802
integers**, independent of QSO volume. Future groups stay invisible until their
time arrives. The payload separately reports future QSOs beyond coverage.

The explicit production clocks are .NET `DateTimeOffset.UtcNow` for generation
and browser `Date.now()` for display, on the same PC. The wall-clock timestamps are
normalized UTC; timezone/DST changes do not change elapsed arithmetic. A large
backward adjustment outside retained history, or forward adjustment past coverage
when further-future QSOs exist, produces a gray unavailable region and a qualified
minimum count. The next relevant refresh reconstructs coverage. Future records
beyond the four-hour look-ahead need that refresh before entering the chart. This
bounded recovery behavior avoids false empty-data claims after large clock jumps.

The browser builds prefix sums once per payload. Re-bucketing uses binary searches
over bounded timestamp groups, with at most 241 displayed bars. A one-second timer
updates elapsed text and current warning state even with no file events. The SVG
updates on minute boundaries, QSO entry/expiry, threshold transitions, clock
rollback, resize or settings/data changes; quiet seconds do not rebuild it. There
is no tick-count-based drift. Background browser throttling may delay ticks; the
next tick recalculates from UTC. A stopped watcher leaves the display running
against its last received database snapshot. All metrics remain QSO-derived;
there is no CAT integration or contest-rule compliance assertion.

## Configuration and runtime

General `%LOCALAPPDATA%\UcxStats\map-settings.json` persists:

| JSON property | Default | Valid values |
| --- | --- | --- |
| `StatisticsVisible` | `false` | Boolean |
| `ContestTimeVisible` | `false` | Boolean |
| `ContestPauseThresholdMinutes` | `60` | Integer 5–180 |
| `OperatingTimeAllowanceMinutes` | `null` | Null or integer 1–525600 |
| `LiveWarningThresholdMinutes` | `10` | Integer 1–1439, less than prolonged |
| `LiveProlongedGapThresholdMinutes` | `15` | Integer 2–1440, greater than warning |
| `LiveTimelineHeightPx` | `null` | Responsive default, or integer 20–320 CSS px |
| `LiveTimelineMinutes` | `60` | Integer 15–240 |
| `LiveTimelineSnap` | `true` | Boolean |
| `LiveTimelineBucketMinutes` | `5` | 1, 2, 5 or 10 |
| `LiveTimelineVisible` | `true` | Boolean, Live Map only |
| `LiveTimelineSettingsVisible` | `true` | Boolean, Live Map only |
| `LiveTimelineSettingsCollapsed` | `true` | Boolean |
| `ContentsPanelCollapsed` | `false` | Boolean |
| `StatisticsPanelCollapsed` | `false` | Boolean |
| `ContentsPanelPosition` | `null` | Null/default anchor or `{ "X": 0..1, "Y": 0..1 }` |
| `StatisticsPanelPosition` | `null` | Same coordinate format |
| `TimelinePanelPosition` | `null` | Same coordinate format |

Missing/invalid values fall back independently; an invalid threshold ordering
restores the 10/15 pair. Numeric strings and fractional minutes are rejected.
The panel validates inputs before sending preferences. The existing settings
service broadcasts successful saves; thresholds and allowance update open maps,
while each existing window retains its presentation settings. ShortpathWidth/LongpathWidth
and unknown JSON fields are retained; saving the existing widths dialog also
retains statistics settings. Existing atomic flush/replace/no-op behavior and
legacy width migration remain. No statistics preferences enter `map-dxcc.json`.
Malformed JSON is reported and preserved rather than overwritten.

The generated page exposes `ucxSetMapStatistics(snapshot)`,
`ucxSetLiveTimeline(snapshot)`, the compatible `ucxSetLiveActivity(activity)`,
`ucxSetStatisticsVisible(visible)` and
`ucxSetStatisticsSettings(settings)`. Queries, locator validation, identity
aggregation and time estimation run in .NET. WPF dispatches SQLite and aggregation
to a worker thread, cancels superseded work and checks generations before sending
results. Settings changes recalculate time from retained .NET timestamps, without
rereading the database. Statistic/visibility updates execute scripts, never
navigate/reload HTML or refetch GeoJSON. Pan/zoom does not query statistics.

The status bar reports query/aggregation time, JSON serialization time, UTF-8
payload sizes and initial/refreshed timeline render time. Tests exercise synthetic 11,000 and 100,000 QSO SQLite logs; these
measurements are development observations, not Windows performance guarantees.
Work is linear in scope rows plus timestamp sorting, with pure locator-validation results cached across windows and snapshots by the
repository (bounded at approximately 100,000 distinct input strings). Full snapshots after relevant
Live imports are intentional for this milestone. Large logs still incur the
existing marker/path rendering cost, independently of statistical payloads.

## UI refinement verification (2026-10-26)

Solution restore, serial build (`-m:1 --disable-build-servers`) and all **338 tests**
pass, with zero build warnings/errors. This milestone adds **42 cases** to the
296-test foundation: 7 timeline data cases, 16 configuration cases and 19 generated
map runtime scenarios. Existing statistics, DXCC, Maidenhead, ODX, contest-time,
path-width and synchronization checks remain passing. VSTest requires its local
loopback socket outside the restricted sandbox. The parser smoke executable is
also checked. No Windows/WPF/WebView2 execution is claimed.

A measured synthetic run produced:

| Timeline QSO count | Snapshot generation | Serialization | UTF-8 payload |
| --- | --- | --- | --- |
| 11,000 | 0.43 ms | 0.41 ms | 82,252 bytes |
| 100,000 | 0.65 ms | 4.76 ms | 105,653 bytes |

These dense fixtures place up to 14,400 distinct second groups within four hours;
the larger count includes duplicate timestamps. The first serialization includes
cold startup. The generated SVG renderer averaged **2.065 ms per update** over 100
updates with 14,400 groups / 100,800 QSOs at a four-hour / one-minute raster. That
Node measurement uses Leaflet/DOM doubles and measures script/string construction,
not browser layout/paint. Real WebView2 rendering and pointer/DPI behavior remain
Windows acceptance work. Ordinary sparse live logs generally send fewer groups;
no timing assertion or real-data Windows performance guarantee is made.

The verification ZIP and checksum are local artifacts:

```text
artifacts/packaging/win-x64/0.12.3.4-map-ui-timeline-verified/
artifacts/UcxStats-Packaging-0.12.3.4-map-ui-timeline-verified-win-x64.zip
artifacts/UcxStats-Packaging-0.12.3.4-map-ui-timeline-verified-win-x64.zip.sha256
```

The self-contained .NET 10.0.12 win-x64 payload contains 435 validated files. The
application version remains 0.12.3.4; these artifacts are not a published release.
The packaging regression suite passes against this exact bundle and ZIP:
**OK (31 tests, 9 PowerShell-related skips)**. No Windows installer execution or
WPF/WebView2 runtime validation was performed.

## Statistics foundation verification (2026-10-26)

Using .NET SDK 10.0.113 and Node.js 18.19.1, solution restore and serial build
(`-m:1 --disable-build-servers`) succeeded with zero warnings/errors. All **296**
.NET tests passed, including 47 added cases beyond the existing 249-test baseline;
the parser smoke executable passed. VSTest needed permission to open its local
loopback socket outside the restricted sandbox.

The synthetic database performance tests measured the following, excluding fixture
import/setup time:

| Scoped QSOs | Query + aggregation | Serialization | UTF-8 snapshot |
| --- | --- | --- | --- |
| 11,000 | 48.4 ms | 2.0 ms | 3,014 bytes |
| 100,000 | 477.9 ms | 20.6 ms | 3,050 bytes |

These fixtures span all 17 UCX bands, share 100 valid grids and omit station/geo
cache assignments. The first serialization measurement includes cold startup;
these are observations rather than timing assertions. Exact numbers vary with
machine, cache state and identity distribution. The payload grows with distinct
membership patterns and bounded pause details, rather than containing each QSO.
Separate repository tests check actual mappable/profile/cache behavior. Large
11,000 source-record selections and 100,000-ID selections also pass without SQLite
parameter/depth errors. This does not measure real Windows marker rendering.

The statistics-foundation build also passed locked restore and self-contained .NET
10.0.12 `win-x64` publishing. The validator accepted all **435** payload files.
The packaging regression suite reported **OK (31 tests, 9 PowerShell-related
skips)** against this exact rebuilt bundle and ZIP, not an earlier release:

```text
artifacts/packaging/win-x64/0.12.3.4-map-statistics-verified/
artifacts/UcxStats-Packaging-0.12.3.4-map-statistics-verified-win-x64.zip
artifacts/UcxStats-Packaging-0.12.3.4-map-statistics-verified-win-x64.zip.sha256
```

These are local verification artifacts. No Windows installer compilation,
installation, WPF/WebView2 runtime or projector acceptance was performed.

## Implementation file inventory

- Data: `src/UcxStats.Data/Database/IQsoRepository.cs`, `QsoScopeSql.cs`,
  `SqliteQsoRepository.cs`, new `QsoStatistics.cs` and `ContestTimeEstimator.cs`.
- Map: `src/UcxStats.Map/MapPageBuilder.cs`, `MapSettings.cs`, `MapSettingsStore.cs`,
  new `MapStatisticsPayload.cs`, `MapStatisticsPanel.cs`, `LiveTimelinePayload.cs`,
  `LiveTimelinePanel.cs` and `MapPanelLayout.cs`.
- WPF: `src/UcxStats.Wpf/MapWindow.xaml.cs`, `LiveMapWindow.xaml.cs`,
  `MainWindow.xaml.cs`, `QueryWindow.xaml.cs`, `DxccMapSettingsWindow.xaml.cs`.
- Tests in `tests/UcxStats.Data.Tests`: new `QsoStatisticsTests.cs`,
  `ContestTimeTests.cs`, `StatisticsSettingsTests.cs`, `StatisticsPerformanceTests.cs`,
  `MapStatisticsIntegrationTests.cs`, `LiveTimelineDataTests.cs`, `MapUiSettingsTests.cs`,
  `MapUiRefinementTests.cs`, `TimelineHeightSettingsTests.cs`, `Fixtures/map-statistics.cjs`, `Fixtures/map-runtime.cjs`
  and `Fixtures/map-ui-refinement.cjs`; extended
  `RepositoryDxccTests.cs`, `MapPathWidthTests.cs` and the test project fixture list.
- Project graph: Data now references the existing Geo project for shared locator
  validation. Normal lock files in Data, Map, WPF, Data.Tests and SmokeTest plus
  Windows lock files in Data, Map and WPF were refreshed; package versions did not
  change. Existing stale project-version constraints now match 0.12.3.4.
- Documentation: `README.md`, `DONE.md`, `TODO.md`, `docs/ARCHITECTURE.md`,
  `docs/MAP_DXCC.md`, `docs/WINDOWS_INSTALLER.md` and this new map statistics guide.

## Windows acceptance checklist

Linux .NET and Node/Leaflet/DOM tests do not execute WPF or WebView2. Record the
Windows build, machine, dataset size, measured timings and result for each item:

- [ ] Open all/source/selected/SQL scopes, including a locator-less-only selection;
  compare totals against SQL and mappable counts against the repository's marker
  query. Test large source-record and ID selections (>11,000 keys).
- [ ] Open two independent scopes, reuse one map, and rapidly replace its selection;
  verify each final snapshot matches its scope and stale work cannot overwrite it.
- [ ] Work a shared DXCC/grid on several bands; toggle arbitrary combinations,
  all off, and back on. Verify distinct unions, no-bands message and bands with no
  markers. Include 2 m, 70 cm and a microwave band.
- [ ] Check malformed locators, two/four/six/eight-character locators, unresolved
  DXCCs and explicit non-DXCC records. Check ODX against cached distances for two
  source profiles and a per-QSO override, ties and missing distance cache.
- [ ] Toggle, collapse and resize the panel; test second-monitor/projector
  readability, F11/Escape, pointer/scroll behavior outside the panel and small
  windows. Hide/show must not change band selection, view or layer visibility.
- [ ] Show contest time, check duplicate/missing timestamps, UTC midnight,
  exactly-60/over-60-minute gaps, multiple pauses and multi-day sessions. Change
  threshold and allowance; confirm band toggles do not alter time totals.
- [ ] Start Live Map with a historical source/start UTC range. With no file writes,
  observe the timer advancing and exact warning/prolonged thresholds (use 1/2
  minutes for a short check). Confirm labels describe QSO gaps and last-QSO band.
- [ ] Import a new relevant QSO with and without locator, modify/delete a record,
  and replace the UCX file; verify summary, time and activity update while map
  position, band/layer selection, path widths and DXCC geometry remain resident.
- [ ] Stop/start monitoring, close/reopen Live Map and restart UcxStats; latest-QSO
  reconstruction must respect source and inclusive UTC start. Verify future-clock
  note and zero-clamp behavior. Minimize/restore and check elapsed-time correction.
- [ ] Save settings, restart, edit path widths, and test fresh/legacy/invalid and
  read-only/interrupted configurations. Verify both general widths and statistics
  preferences survive, and no preferences are written to DXCC configuration.
- [ ] With several synchronized maps, toggle panels/bands and import Live QSOs;
  ensure only pan/zoom synchronize and all existing DXCC/path interactions work.
- [ ] On the real >11,000-QSO dataset and a larger log, record aggregation,
  serialization, payload, initial render and Live refresh times; confirm UI remains
  responsive and no repeated HTML/GeoJSON loading occurs for statistics updates.


## UI refinement Windows acceptance additions

- [ ] Drag each panel using mouse and touch/pen; operate every checkbox, button,
  slider and details control without moving panels or panning the map. Test keyboard
  arrows/Home on handles and arrows on range controls, plus marker/DXCC popups.
- [ ] Resize, move across monitors with different DPI, enter/leave F11 fullscreen,
  collapse/show panels and restart. Verify headers remain reachable, position reset
  preserves visibility/collapse, and each independent/reused window keeps its layout.
- [ ] Inspect the wide shallow footer above the unchanged WPF status line at 1080p,
  higher resolution and narrow windows. Check labels/contrast/projector readability.
  Normal/SQL maps must have no timeline. Hide/collapse settings independently.
- [ ] Sweep duration through every hour snap zone and beyond it; verify smooth
  escape, exact one-minute keyboard edits, disabled snapping, and 15/240 limits.
  Change bucket size and check UTC alignment and partial edge counts against SQL.
- [ ] Move both live thresholds across each other; verify both values and persisted
  strict ordering. Change the contest slider and confirm its established time
  calculation and full-scope behavior remain unchanged.
- [ ] Observe the timer without writes, warning/prolonged transitions, midnight,
  future timestamps and system-clock corrections. Verify no negative elapsed time,
  no future histogram bars, and gray/qualified counts outside recoverable coverage.
- [ ] Import new/duplicate/out-of-order QSOs, correct timestamps, delete records and
  replace a UCX file. Verify exact counts and last-QSO details after restart, while
  map geometry, styles, bands, positions and duration remain resident.
- [ ] Record real WebView2 timeline render/layout and Live refresh times with the
  >11,000-QSO dataset and larger logs. Linux DOM doubles do not measure browser
  layout/paint, DPI behavior, native slider gestures or WPF/WebView2 execution.


## Timeline height Windows acceptance

The 338-test timeline layout was visually accepted on Windows, as reported by the
user. The height-resize addition still needs these native WebView2 checks:

- [ ] Confirm unchanged 148/174 px defaults without a saved height, and restoration
  of an explicitly saved height after restart.
- [ ] Drag the upper edge through 20–320 px, including release outside the strip;
  verify the resize cursor/focus mark, pointer cancellation and double-click reset.
- [ ] Tab to the separator and check Up/Down (10 px), Shift+Up/Down (1 px), Home/End,
  accessible values and no accidental map panning/zooming.
- [ ] Test small windows, fullscreen transitions and different monitor DPI. Confirm
  temporary clamping preserves the saved preference and restores it on enlargement.
- [ ] Inspect bar proportions, readable time/activity labels and compact layout at
  minimum, default and maximum heights, including a narrow window.
- [ ] During Live imports and quiet timer operation, resize repeatedly and confirm
  duration, bucket size, floating-panel preferences, map center/zoom, DXCC and paths
  remain intact. Normal maps must show no resize control.


Height implementation verification (2026-10-26): solution restore, serial build
(`-m:1 --disable-build-servers`) and all **362 tests** pass with zero build warnings
or errors. The 24 added height cases cover nine generated-page runtime scenarios
and fifteen configuration/persistence cases. Existing Live/statistics/DXCC/path
regressions and the parser smoke executable pass. VSTest uses its loopback socket
outside the restricted sandbox; no new Windows runtime verification is claimed.

A fresh self-contained .NET 10.0.12 win-x64 payload validates all 435 files. The
packaging suite passes against this exact bundle/ZIP: **OK (31 tests, 9
PowerShell-related skips)**. Local Windows test artifacts are:

```text
artifacts/packaging/win-x64/0.12.3.4-timeline-height/
artifacts/UcxStats-Packaging-0.12.3.4-timeline-height-win-x64.zip
artifacts/UcxStats-Packaging-0.12.3.4-timeline-height-win-x64.zip.sha256
```

Height-specific edits are in `LiveTimelinePanel.cs`, `MapPageBuilder.cs`,
`MapStatisticsPanel.cs`, `MapSettings.cs` and `MapSettingsStore.cs` in UcxStats.Map;
`MapUiRefinementTests.cs`, `TimelineHeightSettingsTests.cs`, `Fixtures/map-runtime.cjs`
and `Fixtures/map-ui-refinement.cjs` in Data.Tests; and README, DONE, TODO and this
guide. QSO aggregation, watcher behavior, distance/DXCC/contest calculations and
normal-map host behavior are unchanged by this height addition.


## Miniature/compact timeline Windows acceptance

Windows feedback confirmed that height resizing works; it also identified excess
header space in short timelines. The three-mode refinement needs native checks:

- [ ] Drag through **20, 39, 40, 99, 100 and 320 px**. Confirm miniature mode contains
  only histogram/gap bands, compact mode adds just two time labels, and full mode
  restores the title/status details, without a settings button. Check bars fill the miniature strip.
- [ ] Hover/focus the upper edge, resize with pointer and keyboard (10 px / Shift
  1 px), and verify Home/End select 20/320. Double-click must restore 148/174 defaults.
- [ ] In miniature mode, Tab to the strip and press F2. Confirm settings expand and
  the duration slider receives focus. Also verify Karteninhalt's Verlauf-Einstellungen
  control, including when the timeline is hidden or settings were collapsed/dragged.
- [ ] Confirm only Karteninhalt's Live-QSO-Verlauf checkbox controls visibility;
  resetting timeline settings must preserve a hidden timeline. Check saved 80 px
  heights migrate unchanged, and 20/39/40/99/100 px persist across restart.
- [ ] Resize the window, switch fullscreen/DPI/monitor, import/correct Live QSOs,
  and change duration/raster in every mode. Check preferred height, panel layout,
  map view/synchronization, relative bars, UTC buckets and restrained gap coloring.


Miniature/compact verification (2026-10-26): restore, serial build
(`-m:1 --disable-build-servers`) and all **378 tests** pass with zero build warnings
or errors. Sixteen new cases extend the 362-test height baseline: nine generated
runtime scenarios, six additional persisted-height boundaries, and one existing-file
migration check. Tests cover 20/39/40/99/100/320 px, SVG occupancy/ratios, hidden
headers/labels, F2 keyboard access, single visibility/reset behavior, viewport
transitions, quiet ticks, Live updates and retained map/statistics/style state.
The tests use Node/Leaflet/DOM doubles; native Windows behavior remains acceptance
work. No new data aggregation, watcher or timeline runtime contract was introduced.

A fresh self-contained .NET 10.0.12 win-x64 payload validates all 435 files. The
packaging regression suite passes against this exact bundle and ZIP: **OK (31
tests, 9 PowerShell-related skips)**. The parser smoke test also passes. The
local Windows test ZIP and checksum are:

```text
artifacts/UcxStats-Packaging-0.12.3.4-timeline-miniature-win-x64.zip
artifacts/UcxStats-Packaging-0.12.3.4-timeline-miniature-win-x64.zip.sha256
```

This refinement changes `src/UcxStats.Map/LiveTimelinePanel.cs` and `MapSettings.cs`;
`MapUiRefinementTests.cs`, `TimelineHeightSettingsTests.cs`, `Fixtures/map-runtime.cjs`
and `Fixtures/map-ui-refinement.cjs` in Data.Tests; and README, DONE, TODO and this
guide. Presentation modes are derived from effective height, without a new saved
mode or visibility property. Package version remains 0.12.3.4 and no release,
commit or push is implied.


## Timeline settings access cleanup

The inline **Einstellungen** button is removed from every timeline mode, including
full height. Karteninhalt's existing **Verlauf-Einstellungen** checkbox is the
primary show/hide control. It retains the panel's collapsed state and preferred
position. **F2** on the focused resize strip remains the accessible open command:
it shows and expands the separate panel and focuses the duration slider.

The header no longer reserves button space; its tight layout uses title/count
columns with the activity row across the width. The histogram continues to fill
available space. Panel dragging, collapse/position persistence, miniature/compact
thresholds, height keys and Live/UTC bucket contracts are unchanged. Obsolete
button markup, click binding, button CSS and button-focus recovery are removed.

Windows follow-up: confirm no inline button at 20/40/100/148/320 px, header space
usage at full height, checkbox show/hide preserving collapse/position, and F2/Tab
focus in each mode. Repeat while resizing/fullscreen and receiving Live QSOs.
Linux regression tests do not constitute native WebView2 validation.


Cleanup verification (2026-10-26): restore and the full serial solution build pass
with zero warnings/errors, and all **379 tests** pass. Generated-page tests assert
that no inline button or obsolete selector exists, and exercise checkbox show/hide,
retained collapse/position, and F2 duration focus at 20/80/148/320 px while preserving
map/statistics/style state. The parser smoke executable also passes. These Linux
checks do not constitute native Windows/WebView2 validation.

Changed files for this cleanup: `src/UcxStats.Map/LiveTimelinePanel.cs`,
`tests/UcxStats.Data.Tests/MapUiRefinementTests.cs`,
`tests/UcxStats.Data.Tests/Fixtures/map-ui-refinement.cjs`, and this guide.
No settings/data model or runtime snapshot contract changes were required.

A fresh self-contained .NET 10.0.12 win-x64 test payload validates all 435 files:

```text
artifacts/packaging/win-x64/0.12.3.4-timeline-ui-cleanup/
artifacts/UcxStats-Packaging-0.12.3.4-timeline-ui-cleanup-win-x64.zip
artifacts/UcxStats-Packaging-0.12.3.4-timeline-ui-cleanup-win-x64.zip.sha256
```

The packaging regression suite passes against this exact bundle/ZIP:
**OK (31 tests, 9 PowerShell-related skips)**. No Windows installer execution or
native WebView2 validation was performed for this cleanup.


## Live threshold slider overflow fix

The Windows report was reproduced using the generated JavaScript's threshold and
control-update functions. The old native warning slider allowed 180 minutes. At
its right edge, coupling raised prolonged to 181; control refresh then raised
**both slider maxima** to 181. Repeated edge input raised warning/prolonged again
until the settings model's 1439/1440-minute ceiling. Moving prolonged left shrank
the scale back to 180. The fault was a changing-range feedback loop during a native
range gesture, rather than proof of an unbounded custom pointer conversion.

The UI now uses fixed **1–179 / 2–180 minute** ranges, independently of the model's
wider supported configuration range. Moving warning to/beyond prolonged raises
prolonged just to warning + 1. Moving prolonged to/below warning lowers warning
just to prolonged − 1. Otherwise the neighboring value stays unchanged. Defaults
remain 10/15 minutes. All edits are integral and preserve strict ordering.

Both threshold sliders own their pointer gesture. On primary-button pointer-down,
they focus and capture the pointer and freeze the slider rectangle and fixed
limits for that gesture. Client X is clamped to that rectangle **before** conversion
to minutes, then the value is clamped again. Non-finite coordinates and malformed,
fractional or non-finite input values are ignored. Native range dragging is
suppressed; native keyboard range input remains available. Moving outside the
track or viewport cannot expand the range or drive Leaflet panning.

Values preview locally while dragging. Pointer-up, including captured or window
pointer-up outside the control, commits one validated pair. A move reporting no
pressed buttons also ends a gesture whose release was missed. Pointer cancellation,
lost capture, Escape, window blur, page hiding or a hidden document cancels the
preview and restores the latest saved/backend thresholds. Capture is released and
drag state cleared before any follow-up event. Live settings refreshes preserve
an active preview and update its restoration values. Repeated drags need no reload.

A compact `liveThresholdPreferences` message carries just the two integers. The
.NET UI guard rejects pairs outside the fixed slider ranges or without strict
ordering before the existing atomic settings save. It updates only those two
properties on the current settings; path widths, panel positions, timeline choices
and unknown JSON fields are retained. Statistics, timeline snapshot contracts and
refresh/watchers are unchanged.

Previously saved 1439/1440 values remain valid **configuration** values. Loading
or opening settings does not silently rewrite them. The controls show bounded
179/180 values and an explicit note with the actual configured thresholds; activity
analysis continues using the actual values. Editing either slider explicitly brings
both thresholds into the UI range, with the coupling rule above. For example,
choosing prolonged 120 from 1439/1440 saves 119/120. Cancelling the drag restores
the original wide values. **Verlauf zurücksetzen** restores 10/15. This preserves
intentional wider configurations while preventing sliders from creating overflow.

### Windows reproduction and retest

1. Open Live Map and **Verlauf-Einstellungen**. Drag Warnung right, beyond the
   track and outside the window, and jitter the pointer while holding the button.
   Values must stop at 179/180; release outside must end the drag.
2. Drag each slider far left/right repeatedly. Verify the 1/2 and 179/180 endpoints,
   one-minute keyboard steps, and minimal neighbor adjustment on crossing. After
   release, pointer movement must not change values or pan the map.
3. Repeat with Escape, focus switching/Alt+Tab, cancellation and capture loss. The
   preview must roll back without leaving a stuck drag; the next drag must work.
4. Start with a backed-up settings file containing 1439/1440. Verify the explicit
   configuration note, bounded slider scale, unchanged file on load, cancellation,
   then an explicit edit and restart restoring the saved in-range pair.
5. Receive Live QSOs and resize/fullscreen the map during testing. Confirm timer,
   histogram, UTC buckets, durations, band filters, panel positions/collapse,
   synchronization, DXCC and Shortpath/Longpath behavior remain intact. Repeat at
   the Windows DPI scales/monitor arrangement used for the original reproduction.

Automated runtime coverage executes the actual generated map JavaScript with
Node/DOM/Leaflet doubles, including 1300 repeated beyond-edge moves, rapid reversals,
release outside the viewport, pointer-ID isolation, cancellation/capture/focus loss,
invalid values/coordinates, repeated gestures, wider saved values and incoming Live
settings updates. These checks do not constitute Windows/WebView2 runtime validation.


Verification for this fix (2026-10-26): `dotnet restore UcxStats.sln`,
`dotnet build UcxStats.sln` and `dotnet test UcxStats.sln` pass (serial MSBuild with
build servers disabled). Build: **0 warnings, 0 errors**. Full suite: **406 passed,
0 failed, 0 skipped**, including **27 additional cases** (14 generated-page runtime
scenarios and 13 UI-contract/persistence cases); focused tests: **80 passed**.
The existing settings/atomic-write and map regressions remain in the full suite.

A fresh self-contained .NET 10.0.12 win-x64 payload validates all **435 files**:

```text
artifacts/packaging/win-x64/0.12.3.4-timeline-threshold-fix/
artifacts/UcxStats-Packaging-0.12.3.4-timeline-threshold-fix-win-x64.zip
artifacts/UcxStats-Packaging-0.12.3.4-timeline-threshold-fix-win-x64.zip.sha256
```

The packaging regressions pass against this exact bundle and archive:
**31 tests, 9 PowerShell-related skips, no failures**. No native Windows/WebView2
or Windows installer execution is claimed. No commit or push was performed.

Files changed for this fix: `LiveThresholdUi.cs`, `LiveTimelinePanel.cs`,
`MapPageBuilder.cs`, `MapStatisticsPanel.cs` in UcxStats.Map;
`UcxStats.Wpf/MapWindow.xaml.cs`; `LiveThresholdUiTests.cs`,
`MapUiRefinementTests.cs`, `Fixtures/map-ui-refinement.cjs` and
`Fixtures/map-runtime.cjs` in Data.Tests; and this guide. The configuration model,
Live data aggregation, snapshot contracts, watcher, and map synchronization
implementation remain unchanged.

## UCX deletion reconciliation and Live refresh

The release-blocking upsert-only import gap is corrected by complete source
reconciliation. Manual and Live imports share the production snapshot service.
A successful shortened source removes only absent path/index identities from that
source. A stable, successfully parsed/read zero-byte file removes its QSOs while
retaining source configuration and station profiles. Source lists retain the
zero-QSO entry so Live monitoring can restart while the source is empty.

Two complete reads 250 ms apart must agree in length, timestamps and every raw
record hash; a final read validates under the database writer transaction. Windows
also requires a read-only lease for a destructive commit, held until commit or
rollback. Missing/access-denied/sharing/partial/parser failures, changed files or
cancellation preserve the last committed snapshot. Watcher notifications merely
schedule the existing refresh; a missing file is never interpreted as empty.
The format has no commit marker, so a closed writer leaving a valid stable shorter
file cannot communicate whether that file was intended as its final output.

Deletes, updates, source count, geo cleanup/refill and ID reservation commit
atomically. Foreign-key cascades remove QSO caches, and all existing aggregate
queries now exclude the deleted rows: counts, numeric DXCCs, fields/grids, ODX,
contest estimates, latest QSO and histogram multiplicity. Other sources and
configuration remain intact. Surviving QSO IDs stay stable; removed IDs are not
reused. DCL evidence on unrelated/unchanged QSOs is preserved; replacing matching
fields at an index clears that row's stale DCL match and requires a DCL reimport.

Live refresh reuses the existing map replacement/statistics/timeline contracts.
Deleting the latest QSO selects the next latest remaining timestamp; deleting a
locator-less latest QSO also updates activity even if no marker changes. No rows
shows **Noch kein QSO mit gültiger UTC-Zeit** and zero histogram count. Map view,
fullscreen host, selected bands, DXCC visibility, path styles and panel preferences
are retained; no second watcher or HTML reload is introduced.

Windows follow-up for this fix: with a disposable copy, import two records, shorten
to one, then to zero; confirm markers/paths/statistics/latest/timeline at each step,
restart while empty, and restore the file. Exercise an active shared writer,
sharing-denied/missing/malformed files, rapid replacement and Stop/Start or source
switching during a read. Verify failure diagnostics, rollback and recovery without
map-state loss. The native Windows sharing test is skipped on Linux. Existing
user acceptance of the earlier UI milestone does not validate this new import fix.


Historical reconciliation-fix verification (2026-10-26): solution restore and Release build pass
with **0 warnings/errors**; Release tests report **431 passed, 1 skipped, 0 failed**
(26 new cases: 25 executed and one native Windows sharing case skipped on Linux).
The parser smoke executable passes. No NU1900 warnings were emitted; these checks
do not claim a fresh successful vulnerability-advisory scan. Native WPF/WebView2,
FileSystemWatcher delivery, Windows sharing and final installer checks remain.

After those integration tests passed, a **verification-only** self-contained
.NET 10.0.12 win-x64 bundle was generated and validated (435 payload files):

```text
artifacts/packaging/win-x64/0.12.3.5-source-reconciliation-check/
artifacts/UcxStats-Packaging-0.12.3.5-source-reconciliation-check-win-x64.zip
artifacts/UcxStats-Packaging-0.12.3.5-source-reconciliation-check-win-x64.zip.sha256
```

ZIP SHA-256: `53a7798929c9935cab65122b66d9d82363811d2aa75b66286d69016ba5dd93e2`.
The packaging suite against this exact bundle/archive reports **31 tests,
9 PowerShell-related skips, no failures**. The earlier attempt with a 0.12.3.4
artifact was correctly rejected by the current 0.12.3.5 version guard; it is not
used as validation of this fix. Normal and Windows lockfiles match the retained
0.12.3.5 project version; external dependency entries are unchanged.

That fix-stage archive preceded the final Beta package. At the fix stage no
commit/push/release or Windows installer execution was performed. The current
`UcxStats-Packaging-0.12.3.5-win-x64.zip` and its verification are documented in the
[Beta release notes](releases/v0.12.3.5.md); native Windows checks remain separate.


## Final 0.12.3.5 Beta verification

The final Beta uses the same approved feature implementation, including the source
reconciliation correction. Final Release tests pass (431 passed, 1 native Windows
skip), parser smoke passes, and the default publish validates 435 self-contained
win-x64 files. The final ZIP/checksum and packaging results (31 tests, 9 PowerShell
skips) are recorded in [release notes](releases/v0.12.3.5.md#final-beta-verification-2026-10-26).
The earlier suffixed verification artifacts above are historical. Native Windows
sharing, watcher/source-switching and installer acceptance remain separate checks.
