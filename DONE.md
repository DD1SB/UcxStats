# Done

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
