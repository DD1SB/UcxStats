# UcxStats

UcxStats is a read-only statistics and presentation companion for UcxLog `.UCX` files.

It is **not** a logging program and never writes back to UcxLog files.

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
- .NET 10 SDK 10.0.400 (see `global.json`)
- VS Code + C# Dev Kit, or Visual Studio

v0.5.2 uses `Microsoft.Data.Sqlite` 10.0.11.

## Build and run

```powershell
dotnet restore UcxStats.sln
dotnet build UcxStats.sln
dotnet run --project src/UcxStats.Wpf
```

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
