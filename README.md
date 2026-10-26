# UcxStats

UcxStats is a read-only statistics and presentation companion for UcxLog `.UCX` files.

It is **not** a logging program and never writes back to UcxLog files.

## v0.12.3.5 Beta — Map statistics and Live QSO timeline

This Beta follows public 0.12.3.4. Numeric application, assembly and installer
versions are **0.12.3.5**; Beta is the distribution label. See the
[release notes](docs/releases/v0.12.3.5.md) and
[Windows packaging instructions](docs/WINDOWS_INSTALLER.md).

The earlier map/timeline UI has user-reported Windows acceptance. Source deletion
reconciliation is covered by real-file integration tests; its native Windows
sharing/runtime checks and the final installer acceptance remain pending.

Map contents, QSO statistics and **Live-Verlauf – Einstellungen** now have draggable
⠿ headers and independent collapse controls. Positions use normalized coordinates,
recover after resize/fullscreen/DPI changes, and persist in `map-settings.json`.
Use **Panelpositionen zurücksetzen** to restore their anchors. Open windows retain
independent layouts even when map center/zoom synchronization is enabled.

Live Map replaces the embedded activity card with a shallow, full-width QSO
histogram above the existing status line. It shows the rolling UTC QSO count,
last-QSO information and a one-second gap timer. Drag its upper edge to choose a
20–320 px height, or focus that edge and use Up/Down (10 px; Shift: 1 px). The saved
preference survives temporary small-window clamping; double-click restores the
existing responsive default. Heights 20–39 px show only the histogram; 40–99 px
add minimal time labels; 100–320 px show full details. Settings remain available
through Karteninhalt or F2 on the resize strip. Timeline visibility is controlled
only by Karteninhalt's **Live-QSO-Verlauf** checkbox. Teal bars show QSOs; restrained
amber regions show QSO gaps, with a dashed current gap. Gaps do not establish
operator inactivity or actual operating pauses.

Separate timeline settings offer a 15–240 minute slider (default 60), optional
±3-minute hour snapping with smooth escape, 1/2/5/10-minute UTC buckets (default 5),
and fixed live warning/prolonged sliders (1–179 / 2–180 minutes; defaults 10/15)
that maintain valid ordering even when dragging outside the window. The contest pause
control is also a slider; its calculations remain unchanged. Re-bucketing handles
partial edges exactly, including duplicate timestamps. Bounded second-count data
uses the existing Live scope/watcher; the timer never queries SQLite. Settings and
Live updates preserve map geometry, view and styles. See the [map guide](docs/MAP_STATISTICS.md)
for precise semantics, clock limits, configuration and Windows acceptance checks.

### Optional map statistics

Enable **QSO-Statistik** in the existing Leaflet **Karteninhalt** control for a
collapsible summary and band table. Counts use the map's exact SQLite QSO scope:
all/source/selected/SQL/Live maps include locator-less QSOs in totals, while
**kartierbar** counts only QSOs eligible for actual markers. DXCCs, valid Maidenhead
fields and four-character grids use distinct unions across selected bands; zero
selected bands means no bands. ODX uses each QSO's effective station-profile cache.

The normally hidden **Contestzeit anzeigen** option estimates time from sorted UTC
QSO timestamps. Gaps strictly greater than a configurable 60-minute default count
entirely as pauses; visible bands do not change contest totals. An optional minute
allowance shows remaining/exceeded estimated time. These are QSO-derived estimates,
not measured transmission time or contest-rule compliance.

Live Map includes a one-second QSO-gap timer in its timeline footer, independent of file events,
with configurable 10/15-minute warning thresholds. It describes gaps between logged
QSOs and labels the **band of the last QSO**, without inferring current radio band
or operator inactivity. Statistics update in place without HTML/GeoJSON reloads.
Preferences persist alongside existing path widths in `map-settings.json`.
See [map statistics, semantics, configuration and Windows checks](docs/MAP_STATISTICS.md).
The threshold-overflow fix keeps the wider configuration range supported, with
explicit handling of older saved values. All general map preferences remain in
`map-settings.json`; DXCC configuration remains separate in `map-dxcc.json`.

### Safe UCX source synchronization

Manual and Live imports now reconcile complete source snapshots, including
shortened and genuinely empty files. Updates/deletions, source counts and geo
caches commit atomically; failed or changing reads retain the previous database
snapshot. Other sources, profiles and settings remain intact. Latest-QSO activity
and timeline counts follow the remaining QSOs. See [snapshot safety and limits](docs/DATABASE.md#complete-ucx-source-snapshots-01235).

## v0.5.2

- Direct parser for the reverse-engineered 364-byte UCX record format.
- Normal and contest QSOs use the same parser and are imported into the same statistics database.
- WPF QSO table with German date, 24-hour UTC, readable bands and scaled frequencies.
- Import diagnostics for unknown/implausible decoded values.
- SQLite persistence with idempotent re-import using source path + record index + SHA-256 record hash.
- Maidenhead locator center coordinates plus Grid2/Grid4/Grid6 persisted at import time.
- IOTA values are displayed in standard form (`EU-005`, `AF-024`, `OC-001`, ...), while the raw UCX enum remains preserved.
- VS Code build/run/debug configuration.

SQLite is stored by default at:

```text
%LOCALAPPDATA%\UcxStats\ucxstats.db
```

## Requirements

- Windows x64
- For source builds: .NET SDK 10.0.112 or a later stable 10.0.1xx patch, selected
  through `global.json` with `latestPatch` roll-forward (10.0.113 is supported)
- Microsoft Edge WebView2 Evergreen Runtime for maps
- VS Code + C# Dev Kit, or Visual Studio

v0.5.2 uses `Microsoft.Data.Sqlite` 10.0.11.

For the self-contained Windows installer, reproducible Linux publishing, prerequisites,
upgrades and data-preserving uninstall, see [Windows installer pipeline](docs/WINDOWS_INSTALLER.md).
Installed releases include .NET/WPF; end users do not need to install the .NET SDK.

The installer packaging workflow is standalone:

```text
Linux:   ./tools/publish-win-x64.sh
         → artifacts/UcxStats-Packaging-<version>-win-x64.zip (+ SHA-256)
Windows: extract ZIP anywhere → package.ps1 → output/UcxStats-Setup-<version>-win-x64.exe
```

Windows packaging needs only PowerShell 5.1/7 and Inno Setup 6.7.3. The ZIP includes
its scripts, resolved version metadata, validation policy and complete payload;
no Git checkout, source files, .NET SDK or NuGet cache is required. From any folder:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'C:\Build Folder\package.ps1'
```

After reviewing the script, this execution-policy option applies to that process
only; no permanent machine-wide change or elevation is required. Organizational
Group Policy still applies. See the installer guide and the ZIP's `README.txt`.

## License

UcxStats is licensed under the [MIT License](LICENSE), also identified as `MIT`
in the project metadata. Dependencies retain their own licenses; see
[third-party notices](THIRD-PARTY-NOTICES.txt). Application and dependency license
files are included in the Windows payload and installer. The application's MIT
license grants no redistribution rights for AmazingProducer's external
`dxcc.geojson`, which is excluded from the repository and installer.

## Build and run

```powershell
dotnet restore UcxStats.sln
dotnet build UcxStats.sln
dotnet test UcxStats.sln
dotnet run --project src/UcxStats.Wpf
```

The generated map JavaScript regression tests also require Node.js 18+ on PATH.
They execute with Leaflet/DOM test doubles; Windows WPF/WebView2 acceptance checks
are documented in [map documentation](docs/MAP_DXCC.md).

In VS Code, `F5` uses `.vscode/launch.json`; the default build task is `Ctrl+Shift+B`.

## Current UI

Use **UCX öffnen…** to load a log. The table shows:

- Date
- UTC
- Call
- Band
- Mode
- Frequency
- Locator
- Country
- QTH
- Name

The status line shows total QSOs, diagnostic count, and SQLite import counts:

```text
DB: +inserted / Δupdated / =unchanged
```

The **Diagnose…** button lists the first 100 warnings found during parsing/validation.

## Documentation

- `docs/UCX_FORMAT.md` - reverse-engineered UCX record map
- `docs/ARCHITECTURE.md` - project boundaries and read-only rule
- `docs/DATABASE.md` - SQLite schema/import behavior
- `docs/MAP_DXCC.md` - external DXCC overlay configuration, behavior and Windows checks
- `docs/PROJECT_STRUCTURE.md` - source tree
- `DONE.md` / `TODO.md` - development state

### Geo precision
UcxStats supports Maidenhead locators with 2, 4, 6 or 8 characters. Coordinates are calculated from the full available precision; grid2/grid4/grid6/grid8 are persisted for later filtering and map use.


## v0.6.2
Stationsprofile can be created with Maidenhead locator and optional elevation/antenna metadata. UCX sources can be assigned to a station profile. Distance and initial bearing are cached per QSO/profile in SQLite. QSO-level profile override is reserved in the schema.


## Geo-Neuberechnung (v0.6.2)

Die Zuordnung eines Stationsprofils zu vielen Quellen läuft als abbrechbarer Background-Job mit Fortschrittsanzeige. Mehrere Quellen werden in einer Transaktion zugeordnet und der Geo-Cache des Profils wird nur einmal neu aufgebaut. Die Cache-Einträge werden innerhalb einer Transaktion mit einem wiederverwendeten SQLite-Statement geschrieben.


## v0.7 Map View
Leaflet map in WebView2 with SQLite-backed QSO points, station-QTH markers, short-path great-circle lines, selected-source scope and F11 fullscreen.


## v0.7.1
Hotfix: MapWindow imports System.IO and uses the existing UcxDisplayFormat helpers from UcxStats.Core for band/mode display.


## v0.7.2
Map controls are now entirely inside Leaflet. Bands can be toggled individually and use consistent colors for QSO markers and short-path lines.


## v0.8
- Station profiles: Maidenhead 2/4/6/8/10 and direct Lat/Lon; manual coordinates are authoritative.
- `Ein Kartenfenster`: reuse the existing map while preserving position, size, monitor and fullscreen state.
- Multi-window comparison remains available when disabled.
- Permanent `$$$` raw-string fix for embedded Leaflet JavaScript.


## v0.9
- Optional pan/zoom synchronization between multiple open map windows.
- Longpath as an independent, dashed, band-colored map layer.
- Azimuth / Distance polar plot for the currently selected source(s).
- Maidenhead grid overlay intentionally postponed.


## v0.9.1
Longpath dateline handling was rewritten. The long great-circle arc is sampled completely, endpoints are pinned exactly to station/QSO coordinates, and crossings at +/-180 degrees are split with explicit boundary intersection points. This prevents final path segments from disappearing near the dateline.


## v0.9.2
- QSO table supports extended multi-selection.
- Map and azimuth/distance views prefer selected QSO rows; with no row selection they keep the existing source-selection behavior.
- This enables focused visualisation of arbitrary subsets such as three selected VK QSOs.


## v0.9.3
- Longpath rendering now uses continuous unwrapped longitudes and vector copies in adjacent Leaflet worlds, so paths remain visible on both sides of the International Date Line.
- Fixed the remaining nullable WebView2 initialization warning (CS8602).


## v0.9.4
- QSO and station markers are rendered in neighboring world copies, including working popups across the date line.
- Azimuth/distance view can switch between Shortpath, Longpath and Both. Longpath values are derived from the cached shortpath distance/bearing.
- Optional live Tag/Nacht overlay uses the current UTC solar position and refreshes every minute.
- Historical terminator rendering by QSO timestamp is intentionally deferred to a later major version.


### v0.9.4.1
- Fixes the day/night polygon closure: the terminator is now closed over the night-side geographic pole instead of with a false horizontal chord.


## v0.10

- UCX frequency is now named `RxFrequencyHz` throughout the parser/model. Reverse-engineering tests showed that Split TX frequency / split offset are not persisted in the 364-byte QSO record.
- Existing SQLite databases are migrated automatically from `frequency_hz` to `rx_frequency_hz`.
- New **Live-Map** workflow:
  - choose one already imported UCX source,
  - enter "QSOs ab" date/time in UTC or click **Jetzt**,
  - initial sync + map,
  - optional automatic `FileSystemWatcher` monitoring,
  - 500 ms debounce/retry while UcxLog is writing,
  - new mapped QSOs are appended to the existing Leaflet map without resetting zoom, position or active layers.
- UCX source files remain strictly read-only.

## v0.11
- Read-only SQL query/statistics window (`SELECT`, `WITH`, `EXPLAIN`) against the UcxStats SQLite database.
- Results are shown as a virtualized table; hard safety limit: 100,000 result rows.
- Ctrl+Enter executes a query.
- Queries can be saved/loaded as `.sql`.
- `Ergebnis → Karte` maps any result containing `qso.id AS qso_id`.
- Default query exposes the first award/count fields as a starting point for diploma analysis.


## v0.12 — WAE Mixed

- First dedicated award evaluation: Worked All Europe (Mixed Modes).
- Uses the supplied official current WAE entity list, including validity start dates where provided.
- WAE resolver primarily trusts UCX `country`, with explicit aliases (`4U_IT→4U1I`, `UA3→UA`, `ZB2→ZB`) and WAE-only distinctions for `TA1` and Sicily (`IT9`/`IB9`).
- Confirmation counts when UCX shows paper QSL received, LoTW confirmed, eQSL confirmed or ClubLog confirmed.
- Matrix shows every WAE entity against all UCX bands:
  - `✓` confirmed,
  - `○` worked but not confirmed.
- One confirmed entity/band combination = one point, maximum five band points per entity.
- Progress for WAE III / II / I / TOP.
- Double-click a band cell for the QSOs behind that entity/band result.
- DCL-internal confirmations remain unavailable because they are not persisted in UCX.

## v0.12.1 — DCL confirmation import
- Imports a DCL ADIF confirmation snapshot and matches it to existing UCX QSOs; no duplicate QSOs are created.
- Matching is deliberately strict: call + date + UTC minute + band + mode.
- `APP_DCL_STATUS` is persisted as the original status code.
- DCL-confirmed statuses for award evaluation: `c`, `i`, `m`, `n`, `o`; `w` and `x` do not count as confirmed.
- Re-import replaces the previous DCL snapshot, preventing stale DCL confirmations.
- WAE Mixed accepts DCL confirmation in addition to UCX paper/LoTW/eQSL/ClubLog confirmation.

### DCL entity information
The DCL importer also persists `DXCC` and `APP_DCL_ADIF2`. For DCL-confirmed QSOs, known WAE-specific DCL/DXCC assignments take precedence over UCX country/call fallbacks. Initial verified mappings from the supplied export include Sicily (`248`), Shetland (`279`), European Turkey (`390`) and the Vienna WAE entity used by `4U1A` (`206`).

## v0.12.2 — DCL import diagnostics
- Full diagnostic window after DCL import; no longer limited to the first 15/100 messages.
- Every unmatched or ambiguous ADIF record is shown with date, UTC, call, band, mode, DCL status, DXCC/ADIF2 and station information.
- Unmatched records are classified as: call absent; date absent; band mismatch; mode mismatch; or UTC mismatch.
- Up to 12 nearby UCX QSOs for the same call are shown for comparison.
- Ambiguous records show all exact UCX candidates.
- Diagnostic result can be exported as UTF-8 CSV.
- Diagnostics never weaken the strict matcher or automatically assign uncertain QSOs.

## v0.12.3
- DCL matching now accepts an official-style ±10 minute UTC tolerance, but only when call, band and mode still match.
- The tolerance works across UTC midnight because complete timestamps are compared.
- Mode differences are deliberately not auto-matched; SAT vs SSB/FM therefore remains a manual diagnostic case.
- Multiple matching QSOs remain manual-review cases rather than being guessed automatically.
- The main toolbar now has one `Diplome…` entry. A central award catalog opens the available award/variant; WAE Mixed is the first registered award.

## v0.12.3.1 — WAE confirmation/entity hotfix
- WAE confirmation is now evaluated against the entity of the same concrete QSO.
- A DCL confirmation with a DCL DXCC special mapping counts only for that mapped WAE entity.
- UCX paper/LoTW/eQSL/ClubLog confirmation cannot leak from another UCX entity into a DCL-special WAE entity.
- WAE QSO drill-down now shows UCX country, resolved WAE entity and whether the confirmation is actually counted for the selected WAE entity.

## v0.12.3.2 — DCL ADIF2/WAE hotfix
- Removed the incorrect WAE subdivision mapping based on DXCC.
- WAE subdivisions from DCL are now resolved from APP_DCL_ADIF2.
- Mappings verified against the complete 7,558-record DCL export:
  - 1206 -> 4U1V
  - 1248 -> IT9
  - 1279 -> GM/S
  - 1390 -> TA1
  - 1522 -> Z6
- Normal DCL records where APP_DCL_ADIF2 equals DXCC are not treated as subdivisions.


## v0.12.3.3 — Map tile policy fix

- Leaflet is no longer hosted by WebView2 through `NavigateToString` / an opaque origin.
- Each map is served from the local WebView2 virtual HTTPS origin `https://ucxstats.local`.
  Tile requests therefore carry a real web origin/referrer instead of originating from an anonymous string document.
- The WebView2 User-Agent keeps the Edge UA and appends `UcxStats/0.12.3.3` for stable application identification.
- OpenStreetMap remains the default tile provider, with the normal browser/WebView cache honoring HTTP cache headers.
- Tile provider URL, name, attribution and max zoom are no longer hard-coded. On first use UcxStats creates:
  `%LOCALAPPDATA%\UcxStats\map-tiles.json`
- A tile load failure is surfaced in the map status bar together with the provider name and configuration path.

## DXCC status overlay

The optional Leaflet DXCC overlay is implemented on top of the numeric DXCC foundation. The polygon data remains an external user-provided resource and is never bundled with UcxStats.

- DXCC and WAE remain separate entity models. A QSO has an optional numeric ADIF/DXCC identity, while `WaeEntityResolver` may independently resolve a more specific WAE-only entity such as `4U1V`, `GM/S`, `JW/B`, `TA1` or Sicily.
- SQLite stores the nullable numeric identity in `qso.dxcc_entity_id`. DCL code `999` and released ADIF code `0` are explicit non-DXCC evidence and suppress UCX fallback. Otherwise, a resolved UCX country identity is effective; a catalog-recognized normal DCL entity is used only when UCX is unresolved. UCX/DCL disagreements remain audit conflicts without replacing the UCX identity. `APP_DCL_ADIF2` remains WAE-specific evidence and is never treated as a DXCC ID.
- UCX-country mapping version 3 is generated from the authoritative entity-definition section of the official UcxLog 8.50 `COUNTRY.DAT` resource, independently of any user's log history. It inventories 452 distinct UCX classification labels: 340 map to Current ADIF entities, 62 historical `#...` labels map to Deleted entities, and 50 WAE/R150S-only subdivisions remain explicitly unresolved because UcxLog supplies no numeric DXCC ID for them. This is not a callsign-prefix resolver. The previous 188 audit-derived mappings remain numerically unchanged except `7Q`, corrected from Lesotho/432 to UcxLog's Malawi/440 assignment.
- Confirmation sources remain distinct: received paper QSL (`status151 & 0x20`), eQSL (`0x40`), LoTW (`0x80`), ClubLog (`status161 & 0x20`) and confirmed DCL status (`c/i/m/n/o`). Existing WAE policy still accepts any of those sources.
- DXCC display states are `Unknown`, `NotWorked`, `Worked`, `OtherConfirmed` (eQSL/ClubLog), and `DxccCredited` (paper/LoTW). The map uses gray/red/yellow/light-blue/green fill plus distinct outline patterns. DCL-only confirmation remains `Worked`; it does not promote DXCC display status.
- DXCC polygons remain an external, user-provided `dxcc.geojson`; no geometry is bundled or copied into SQLite. Missing or invalid geometry does not affect application startup or existing maps.
- The known AmazingProducer file with SHA-256 `e9b086eff7fb690e9990e10855f3fb494f9d1115e4af1928f6a8b545efef4568` has an explicit v1 normalization profile for three incorrect IDs, Kosovo, duplicate Conway Reef, and retained non-DXCC Somaliland geometry. Unknown hashes are validated without those source-specific corrections.
- Current/deleted/unknown classification is exposed through `IDxccEntityCatalog`. The offline catalog is generated from the Released ADIF 3.1.7 machine-readable DXCC Entity Code TSV dated 2026-03-22: 340 Current entities, 62 Deleted entities, plus code `0` as an explicit non-DXCC sentinel. Unknown numeric IDs remain `Unknown`. No ADIF or ARRL site is contacted at runtime.

Use **Kartenoptionen…** in the main window or an open map. Configuration is split
under `%LOCALAPPDATA%\UcxStats`:

| File | Responsibility |
| --- | --- |
| `map-settings.json` | General presentation: `ShortpathWidth` and `LongpathWidth` |
| `map-dxcc.json` | External `GeoJsonPath`, DXCC `EnabledByDefault` and other DXCC options |

**Shortpath (px)** and **Longpath (px)** independently set Leaflet line weights
from 0.5 to 8.0 px in 0.5 px steps. The original defaults remain **1.3 px** and
**1.0 px**; 1.3 is an additional Shortpath choice. **Linienbreiten zurücksetzen**
selects these defaults; **Speichern** saves and applies them to every open map.
The optional JSON fields `ShortpathWidth` and `LongpathWidth` also accept other
fractions in that range, retained by the dialog.

At startup, each valid value in `map-settings.json` takes precedence. Missing or
invalid values use valid legacy widths from `map-dxcc.json`, then the original
defaults. Migration commits the complete general settings atomically before
removing the two legacy fields from DXCC configuration. All other DXCC fields,
including unknown options, survive. Completed migration and unchanged settings
are not rewritten; a fresh installation creates no settings files until needed.
Malformed JSON is reported and left untouched. See [migration and recovery](docs/MAP_DXCC.md#configuration-migration-and-recovery)
for write failures and recovery.

Open maps restyle their existing path layers through `setStyle({weight:…})`.
Saving widths preserves pan/zoom, bands, layer visibility, fullscreen, DXCC and
Live Map monitoring. Live additions and replacements use the current widths.
DXCC borders, markers and day/night shading keep their existing styles. DXCC
path/default-visibility changes continue to take effect on newly loaded maps.

Each map validates and normalizes that source, stages only the normalized copy in its private temporary `MapWeb` folder, and loads it once through `https://ucxstats.local`. The overlay defaults to off. Its resident Leaflet layer is restyled when the existing band checkboxes change; geometry is not fetched again and never participates in `fitBounds`. Polygon popups show numeric ID, source prefix, current selected-band status, QSO count, and worked/confirmation bands. Complete compact aggregate snapshots are also refreshed by Live Map, including changes caused by QSOs without locators.

Missing, moved, invalid, or unknown-hash geometry never prevents the base map, markers, paths, day/night layer, map synchronization, or Live Map from loading. See `docs/MAP_DXCC.md` for behavior, diagnostics, the Windows acceptance checklist, and the primary-world vector limitation.

### Regenerating the ADIF DXCC catalog

Download the official `ADIF_317_resources_2026_03_22.zip`, extract
`317/exports/tsv/enumerations_dxcc_entity_code.tsv`, then run:

```bash
tools/generate-dxcc-catalog.sh /path/to/enumerations_dxcc_entity_code.tsv
```

The generator verifies the exact Released resource SHA-256 and row totals before replacing the compact generated C# catalog. For a future ADIF release, review and update the generator's version, source hash, release metadata and expected totals before regenerating. The UCX mapping, WAE resolver and external GeoJSON normalization are independent and are not generated from this catalog.

### Regenerating the UCX country mapping

The mapping source is the entity-definition section of `Country0.txt` from the official UcxLog 8.50 `UcxLog.zip` / `COUNTRY.DAT` resource dated 2026-06-11. The repository does not redistribute that source file and does not download it at runtime. After obtaining and locally extracting the authoritative `Country0.txt` payload, run:

```bash
tools/generate-ucx-dxcc-mapping.sh /path/to/UcxLog-8.50-Country0.txt
```

The generator verifies the exact decoded-resource SHA-256 and structural totals before replacing `UcxLog850CountryDxccMappings.Generated.cs`. When UcxLog changes its entity data, obtain the new official resource, review its new/changed/removed classifications against the offline ADIF catalog, update the pinned hashes/version/counts, regenerate, increment `MappingVersion`, and review every unresolved or duplicate source code. This process is independent of real-log audits, so a newly worked normal UCX entity does not require a prior database observation or a runtime callsign-prefix lookup.

The source-local UCX country audit is exposed as `IQsoRepository.GetDxccIdentityAuditAsync()`. Run it against the repository instance that points at the user's real `ucxstats.db`; its result contains totals, per-country counts/mappings, unresolved codes, evidence classes, and DCL/UCX conflicts. A command-line entry point is also available:

```text
dotnet run --project tests/UcxStats.SmokeTest -- dxcc-audit "/path/to/ucxstats.db" [--csv "/path/to/audit.csv"]
```
