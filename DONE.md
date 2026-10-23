# Done

## SDK compatibility and MIT license (2026-10-23)

- [x] Keep the central application/file/installer version at `0.12.3.4` for the
  map-settings update; preserve historical release records.
- [x] Replace exact SDK patch matching with stable `latestPatch` roll-forward
  from 10.0.112 within the 10.0.1xx feature band. The installed 10.0.113 resolves;
  older patches, other bands/major/minor versions and prereleases are rejected.
- [x] Record the resolved SDK in new manifests and validate stored SDK metadata
  independently of the validator machine's installed SDK.
- [x] Add seven focused SDK regressions, including publish-script acceptance and
  rejection before restore/output creation; all seven pass.
- [x] Inspect project history and existing notices: no project-level license or
  conflicting project copyright notice found; the existing Leaflet BSD license
  and all nine reviewed asset hashes remain unchanged.
- [x] Add the complete standard MIT `LICENSE`, copyright (c) 2026
  Stefan Biermann / DD1SB, using the copyright-holder name confirmed by the user.
  Set the project SPDX license expression to `MIT` and link the license from README.
- [x] Preserve exact supplied .NET/WPF, Microsoft.Data.Sqlite, SQLitePCLRaw and
  WebView2 licenses/notices in `third-party/`; include them in the
  Windows payload with explicit required files and asset hashes. Keep external
  DXCC data excluded and document that application licensing grants no dataset
  redistribution rights. Document the application/third-party license distinction
  in README, the installer guide and the standalone packaging instructions.
- [x] Preserve hash-pinned asset/notice bytes across Git checkouts with scoped
  attributes; retain original upstream whitespace without changing source checks.
- [x] SDK 10.0.113 final verification: `dotnet restore UcxStats.sln`,
  `dotnet build UcxStats.sln` and `dotnet test UcxStats.sln` pass; all 249 tests
  pass, including migration and generated JavaScript. The parser smoke executable
  also passes. Final build has zero warnings/errors. No NU1900 warnings were
  emitted in these checks; no fresh successful advisory scan is claimed.
- [x] Rebuild and validate a 435-file self-contained .NET 10.0.12 win-x64 payload at
  `artifacts/packaging/win-x64/0.12.3.4-map-settings-mit` and standalone ZIP
  `artifacts/UcxStats-Packaging-0.12.3.4-map-settings-mit-win-x64.zip` (+ SHA-256).
  The manifest records the actual SDK 10.0.113 and application version 0.12.3.4.
  Packaging regressions report OK (31 tests, 9 PowerShell-related skips), covering
  license/notice preservation and tampering, SDK metadata, privacy exclusions and
  deterministic ZIP/checksum rebuilding. No Windows installer compilation,
  installation or WPF/WebView2 runtime verification was performed for this build.

## Separate general map settings from DXCC configuration

- [x] Add `MapSettings`, `MapSettingsStore` and one shared `MapSettingsService`
  for `%LOCALAPPDATA%\UcxStats\map-settings.json`; retain original defaults/range.
- [x] Keep DXCC-only fields in `map-dxcc.json`; preserve known/unknown fields
  during migration and later saves, and stop serializing widths into DXCC options.
- [x] Resolve each width from valid new values, valid legacy values, then defaults;
  commit general settings before cleaning legacy fields and retry partial migration.
- [x] Use flushed same-directory temporary files and atomic replacement; skip
  unchanged saves/startups, preserve malformed files and ignore orphaned temp files.
- [x] Share general settings across MainWindow, normal/SQL maps, Live Map and
  the existing **Kartenoptionen…** dialog; retain the JavaScript/runtime contract.
- [x] Add migration/failure/preservation/idempotence regressions and update the
  existing persistence tests without removing runtime or Live Map coverage.
- [x] Document responsibilities, precedence, atomic writes and recovery limits.
  Windows migration/runtime acceptance remains in TODO. Previous Windows packaging
  artifacts below predate this refactor and have not been regenerated.
- [x] Refactor verification with SDK 10.0.112: `dotnet restore UcxStats.sln`,
  `dotnet build UcxStats.sln` and `dotnet test UcxStats.sln` passed; all 249 tests
  passed, including 41 new migration cases and the retained runtime/Live Map cases.
  The packaging privacy regression also passed with `map-settings.json` covered.
  NuGet security-feed access reported NU1900 warnings; there were no build errors.

## Configurable map path widths

- [x] Preserve the discovered Shortpath 1.3 px and Longpath 1.0 px defaults.
- [x] Add independently validated fractional `ShortpathWidth`/`LongpathWidth`
  values; now stored in `map-settings.json` with migration from the original
  `map-dxcc.json` implementation, as described above.
- [x] Extend **Kartenoptionen…** with 0.5–8.0 px choices in 0.5 px steps, the exact
  original Shortpath default and a width-only reset; expose it on open maps too.
- [x] Restyle resident Leaflet paths in every open map with `setStyle({weight:…})`;
  preserve view, bands, layer visibility, fullscreen, DXCC and Live Map state.
- [x] Apply current widths to initial, appended and replaced Live Map paths,
  including new bands and all wrapped Longpath copies.
- [x] Add configuration, broadcast and generated-JavaScript regression tests
  for compatibility, fractions, invalid values, independence, runtime updates,
  Live additions/replacements/reset and untouched unrelated map layers.
- [x] Document persistence, the runtime mechanism and a Windows acceptance
  procedure. Windows runtime/visual verification remains in TODO.
- [x] Linux verification with pinned SDK 10.0.112: solution restore/build passed,
  all 208 tests passed (30 new width cases), and the existing smoke test passed.
  Restore/build reported NU1900 warnings because the NuGet vulnerability feed
  was unavailable; compilation had no errors.
- [x] Established Windows pipeline produced and validated a 425-file,
  self-contained .NET 10.0.12 win-x64 payload and standalone ZIP at
  `artifacts/UcxStats-Packaging-0.12.3.3-path-widths-win-x64.zip` (+ SHA-256).
  Packaging regression suite reported OK (22 tests, 9 PowerShell-related skips).
  No Windows application, WebView2 or installer runtime test was performed.

## Standalone Windows installer packaging

- [x] User-confirmed clean-Windows smoke test (2026-10-08): the standalone
  packaging workflow produced `UcxStats-Setup-0.12.3.3-win-x64.exe`; installation,
  application startup, Live Map with OSM tiles, QSO markers, short/long paths and
  self-contained .NET deployment worked. Upgrade, uninstall and user-data
  preservation still require acceptance tests listed in TODO.
- [x] Detect the actual Inno compiler through a temporary ISPP `Ver` probe;
  ignore zeroed executable version resources while enforcing the 6.7.3 pin.
- [x] Linux publish resolves the central project version and produces
  `artifacts/UcxStats-Packaging-<version>-win-x64.zip` plus SHA-256.
- [x] ZIP contains root `package.ps1`, Inno script, schema-2 version/hash manifest,
  README, payload policy, explicit installer file list and self-contained payload.
- [x] Windows packaging uses only extracted files and Inno Setup; no checkout,
  project source, .NET SDK or NuGet cache is needed. Output stays under `output/`.
- [x] Preserve per-user installation, shortcuts, WebView2 prerequisite checks,
  stable AppId, upgrade behavior and data-preserving uninstall.
- [x] Preserve payload allowlist/privacy checks; validate payload and packaging
  resource hashes before compiling; normalize ZIP order/timestamps/permissions.
- [x] Add regression coverage for isolated ZIP extraction, version/inventory,
  standalone script validation, Inno arguments, missing/changed/private payloads
  and stable installer identity. Additional Windows acceptance checks remain in TODO.
- [x] Document `Linux: publish-win-x64.sh` and
  `Windows: extract ZIP → package.ps1 → Setup EXE`, including process-only
  PowerShell execution-policy handling.

## UCX reverse engineering

- [x] Identified fixed record size: **364 bytes**.
- [x] Confirmed there is no per-file header in the examined normal and contest UCX logs.
- [x] Confirmed normal and contest QSOs use the same 364-byte record structure.
- [x] Identified date and start/end time fields.
- [x] Identified callsign, sent/received RST and sent/received exchange fields.
- [x] Identified band and mode enums.
- [x] Identified QTH, name and remarks.
- [x] Identified country/prefix, continent, exact frequency, locator, IOTA, district, CQ zone and ITU zone.
- [x] Corrected frequency storage to **uint64 little-endian Hz** (required for microwave/SAT QSOs).
- [x] Identified contest multiplier 1/2 slots and QSO points.
- [x] Identified QSL manager/via plus `CBA` and `Ok` booleans.
- [x] Identified paper-QSL print mark and sent/received status bits.
- [x] Identified eQSL, LoTW and ClubLog confirmation bits.
- [x] Identified the paper-QSL send timestamp as Delphi `TDateTime`/days since 1899-12-30; its validity is controlled by the paper-QSL-sent bit.
- [x] Identified two freely assignable award/count string pairs.
- [x] Kept unproven status bits explicitly unknown instead of guessing their meaning.

## Initial implementation

- [x] Created C#/.NET solution structure.
- [x] Implemented streaming UCX file reader.
- [x] Implemented defensive fixed-record parser.
- [x] Added known band/mode mappings while preserving raw numeric codes.
- [x] Added decoded QSL status helpers.
- [x] Added Maidenhead locator to center-coordinate conversion.
- [x] Added a minimal WPF shell.
- [x] Added a package-free smoke-test project.

## V0.2 baseline / decisions
- [x] Real-world validation: `LOG_2025.UCX` loads all **1,177 QSOs** successfully in the WPF table.
- [x] German date (`dd.MM.yyyy`), 24-hour UTC (`HH:mm`), human-readable band labels and scaled frequency display defined/implemented.
- [x] Compact table columns fixed as Date, UTC, Call, Band, Mode, Frequency, Locator, Country, QTH, Name; remaining fields belong in a selected-QSO detail panel.
- [x] Map direction fixed: separate Leaflet presentation surface, usable via WPF/WebView2 and browser.
- [x] Event-map filter defined as all QSOs from a selected UTC date/time, optionally to an end time; all-QSO demo mode planned.
- [x] Fullscreen/projector and later OBS browser-source/overlay are explicit presentation targets.
- [x] Locator/geodesic calculations belong outside the Leaflet redraw loop and should be cached/precomputed.

## v0.4 persistence / diagnostics
- [x] Adopted the user's known-good v0.2 code including the `recordData` identifier fix as the development baseline.
- [x] UCX source files remain strictly read-only (`FileAccess.Read`, shared with UcxLog).
- [x] Added SHA-256 hash per raw 364-byte record.
- [x] Added import diagnostics for unknown band/mode codes, suspicious zones/locators and selected unknown status bits.
- [x] Added SQLite persistence using `Microsoft.Data.Sqlite` directly (no EF Core).
- [x] Added idempotent upsert keyed by absolute source path + record index; unchanged hashes are skipped.
- [x] Added database indices for date/time, callsign, band, mode, country, locator and QSL status.
- [x] Persist locator center latitude/longitude and Grid2/Grid4/Grid6 during import.
- [x] Added database/import summary to the WPF status line and a simple diagnostics viewer.
- [x] Added VS Code build/run/debug configuration.
- [x] Documented explicit non-goal: UcxStats is not intended to replace UcxLog and never writes UCX data.


## V0.4
- Mehrfachauswahl von UCX-Dateien beim Import
- Anzeige der aktiven/zuletzt geladenen Quelle(n)
- Anzeige der Gesamtzahl aller QSOs in SQLite
- Quellenübersicht mit Dateiname und QSO-Anzahl
- optionale/sichtbare Quellen-Spalte im QSO-Grid
- Mode-Anzeige fachgerecht als CW/SSB/RTTY/AM/FM/PSK31/PKT/SAT/FT8
- Microsoft.Data.Sqlite auf 10.0.11 aktualisiert
- LoTW-Upload (`151 & 0x10`) und ClubLog-Upload (`161 & 0x80`) im Datenmodell dokumentiert

## v0.5.1
- [x] Fixed IOTA display: UCX continent enum is rendered as EU/AS/AF/NA/SA/OC/AN and numeric part as three digits.
- [x] Verified locator-derived latitude/longitude and Grid2/Grid4/Grid6 were already persisted in SQLite; no schema change required.
- [x] Microsoft.Data.Sqlite remains pinned to 10.0.11.

## v0.5.2
- Maidenhead locator handling confirmed for 2/4/6/8 characters.
- Latitude/longitude are calculated from the full locator precision.
- Added persistent `grid8` with automatic migration of existing SQLite databases.
- UCX locator storage confirmed as max. 8 characters.


## v0.6
- Station profile persistence and editor
- UCX source -> station profile assignment
- Optional elevation, antenna and antenna height metadata
- Cached great-circle distance and initial bearing per QSO/profile
- Automatic schema migration; existing database remains usable
- QSO-level station_profile_id reserved for later overrides


## v0.6.2

- Geo-Neuberechnung aus dem WPF-UI-Thread herausgelöst.
- Fortschrittsanzeige und Abbrechen für QTH-Zuordnung/Geo-Rebuild.
- Mehrfachzuordnung von Quellen erfolgt in einer DB-Transaktion.
- Geo-Cache wird bei Mehrfachzuordnung nur noch einmal pro Stationsprofil aufgebaut.
- Geo-Cache-INSERTs laufen in einer Transaktion mit wiederverwendetem Prepared Command.
- Fortschritt wird gebündelt statt pro QSO gemeldet.

- v0.7: Map view with QSO points, station QTHs, short-path great-circle lines, selected-source filtering and fullscreen.

- v0.7.1: Fixed MapWindow build errors (System.IO / band+mode formatting).

- v0.7.2: Map crash fix, Leaflet-only controls, per-band visibility and consistent band colors for markers and short-paths.

- v0.8: 10-character station locators + direct Lat/Lon.
- v0.8: single-map-window refresh mode preserving window state; multi-window mode retained.

- v0.9: synchronized map windows, longpath layer, azimuth/distance polar analysis.

- v0.9.1: fixed Longpath dateline splitting; long arcs now always terminate exactly at the QSO target.

- v0.9.2: arbitrary selected QSO rows can be sent to map and azimuth/distance views; source selection remains the fallback.

- v0.9.3: fixed Longpath world-wrap/date-line rendering by using unwrapped longitudes plus adjacent-world vector copies.
- v0.9.3: fixed CS8602 in MapWindow WebView2 initialization.

- v0.9.4: wrapped QSO/station markers and popups.
- v0.9.4: Shortpath/Longpath/Both polar view.
- v0.9.4: live current-time day/night terminator overlay with one-minute refresh.

- v0.9.4.1: fixed polar closure and world wrapping of the live night overlay.


- v0.10: renamed the persisted UCX frequency semantics to RxFrequencyHz / rx_frequency_hz with automatic SQLite migration.
- v0.10: Live-Map source monitoring with UTC start time, FileSystemWatcher debounce and incremental map updates.

- v0.11: expert SQL/statistics foundation with read-only execution, result grid, saved SQL files and result-to-map.

- v0.11.1: fixed malformed literal \\n sequences in IQsoRepository.cs generated by the v0.11 packaging step.


- v0.12: WAE Mixed award engine, entity resolver, confirmation evaluation, country×band matrix, class progress and QSO drill-down.

- v0.12.1: DCL ADIF confirmation snapshot import with strict QSO matching and WAE integration.

- v0.12.1: DCL DXCC/APP_DCL_ADIF2 persisted and used as preferred WAE entity evidence for verified special entities.

- v0.12.2: complete DCL unmatched/ambiguous diagnostics with mismatch classification, candidate view and CSV export.

- v0.12.3: DCL ±10-minute time tolerance with strict call/band/mode matching; central Diplome selection and AwardCatalog.

- v0.12.3.1: fixed WAE entity/confirmation coupling; added WAE drill-down diagnostics (UCX country, resolved WAE, counted status).

- v0.12.3.2: WAE DCL subdivisions now use APP_DCL_ADIF2 instead of DXCC; added Z6 mapping.


- v0.12.3.3: fixed WebView2/OSM tile-policy integration with a local HTTPS virtual host, identifiable UA and configurable tile provider.

## DXCC overlay foundation (data/model phase)

- [x] Added shared, source-preserving QSO confirmation flags while retaining existing WAE `Paper OR LoTW OR eQSL OR ClubLog OR DCL` behavior.
- [x] Added nullable `qso.dxcc_entity_id`, versioned safe backfill, and `(dxcc_entity_id, band_code)` index.
- [x] Added conservative explicit UCX-country mapping plus unresolved-code and DCL/UCX conflict audit API.
- [x] Extended the explicit UCX-country mapping to version 2 using the 188-code real-database audit, with direct/alias/historical classification metadata and no callsign-prefix inference.
- [x] Replaced the snapshot-derived table with generated UCX mapping version 3 from the official UcxLog 8.50 country resource: 340 Current, 62 Deleted and 50 explicitly unresolved WAE/R150S-only labels; added deterministic source/audit metadata and R1FJ import/backfill/aggregation coverage.
- [x] Kept DXCC and WAE identity separate; `WaeEntityResolver` and `APP_DCL_ADIF2` subdivision handling are unchanged.
- [x] Added `QsoScope` and locator-independent entity/band DXCC aggregate repository API.
- [x] Added five-state DXCC overlay model; DCL-only confirmation does not promote overlay status.
- [x] Added external GeoJSON validation and exact-hash AmazingProducer normalization without bundling or SQLite geometry storage.
- [x] Replaced the minimal catalog with a deterministic complete catalog generated from Released ADIF 3.1.7 machine-readable resources (340 Current, 62 Deleted, plus explicit non-DXCC code 0).
- [x] Added focused automated tests for confirmation, WAE compatibility, identity, scopes, aggregation, migration, catalog behavior, and GeoJSON validation/normalization.

## DXCC Leaflet overlay

- [x] Added persistent external-GeoJSON selection and validation at `%LOCALAPPDATA%\UcxStats\map-dxcc.json`; no geometry is bundled.
- [x] Added exact `QsoScope` propagation for source, selected-row, SQL-result, single-window, multi-window, and Live Map entry points.
- [x] Added normalized GeoJSON staging through the existing WebView2 virtual HTTPS host and a resident numeric-ID-indexed Leaflet layer outside `fitBounds`.
- [x] Added an optional custom-control checkbox, dedicated pane, five color/outline states, compact legend, selected-band restyling, and entity popups.
- [x] Added complete compact aggregate snapshot transfer and Live Map status refresh independent of locator-backed marker changes.
- [x] Preserved tiles, markers, short/long paths, day/night shading, map synchronization, fullscreen, and independent per-window overlay visibility.
- [x] Added browser-neutral configuration, normalized-geometry, payload, generated-page, pane/order, scope, and regression tests.
