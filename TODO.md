# Todo

## Parser / reverse engineering

- [ ] Add small 364-byte regression fixtures for normal, contest, CW/SSB/FT8 and microwave/SAT records.
- [ ] Determine whether record byte 0 should be exposed as `IsNormalLogRecord`, `IsContestRecord`, or kept raw.
- [ ] Keep status `151 & 0x10`, `151 & 0x04`, and `161 & 0x80` unknown until independently proven.
- [ ] Determine the semantic purpose of offset 243 and the structurally recognized unknown strings around 162, 255 and 261.
- [ ] Confirm remaining band IDs (30 m / 70 cm / 23 cm / 13 cm / 3.4 GHz / 5.6 GHz) with real or controlled records.
- [ ] Identify UcxLog's codes for `unbestimmt` and `Rest` if useful.

## v0.4 - statistics / UI

- [ ] Add selected-QSO detail panel for RST/exchange, QSL, IOTA, district, manager, awards and contest fields.
- [ ] Add database-backed QSO table/filtering instead of displaying only the just-opened file.
- [ ] Add first fixed statistics views.
- [ ] Add expert free-form read-only SQL query view (`SELECT` / `WITH` only).
- [ ] Add saved queries.
- [ ] Later derive a visual query builder from real recurring queries.
- [ ] Add configurable own station locator/location.
- [ ] Persist distance and initial bearing after station location is configured.

## v0.5 - live synchronization

- [ ] Watch active UCX log files for size/content changes.
- [ ] Fast-path appended 364-byte records.
- [ ] Detect modified/deleted records via per-record hashes.
- [ ] Publish newly imported/changed QSOs to UI/map clients.

## Map / presentation

- [ ] Implement Leaflet as a small web application hosted by UcxStats and display it through WebView2/browser.
- [ ] Add event filter from UTC date/time, optional end time, and all-QSO demo mode.
- [ ] Cache locator coordinates and short-/long-path geometry outside the render loop.
- [ ] Generate geodetic short path and long path in the .NET geo layer.
- [ ] Add true fullscreen presentation mode for second monitor/projector.
- [ ] Add OBS/browser-source-friendly overlay, ideally with optional transparent background.
- [ ] Add optional HTTP/WebSocket live updates for browser/projector/stream clients.

## Later

- [ ] Award-rule/query system.
- [ ] Contest-aware views without excluding contest QSOs from normal statistics.
- [ ] Optional QTC parser/importer for contest-specific statistics.


## Nach V0.4
- Quellen gezielt aus Datenbank laden/filtern, nicht nur beim aktuellen Dateiimport anzeigen
- Dateiverkleinerung/Löschen von UCX-Records synchronisieren
- Live-Watcher für neue/geänderte Records
- QSO-Detailansicht
- freie read-only SQL-Abfrage nach stabilem Schema

## v0.6 Geo / station context
- Add station profiles (home / portable / event locations).
- Allow a default station profile per UCX source.
- Allow an optional per-QSO station-profile override.
- Cache distance, bearing and later short/long-path geometry by QSO + station profile.


## After v0.6
- UI for per-QSO station profile override
- Surface cached distance/bearing in details/statistics
- Polar/azimuth statistics and map integration
- Short-/long-path geometry cache


## Nach v0.6.2 beobachten

- Laufzeit und Datenträgerlast beim Geo-Rebuild mit >10.000 QSOs messen.
- Falls nötig: Chunking/Batch-Größe und WAL/PRAGMA-Tuning weiter optimieren.
- Background-Job-Infrastruktur später für große Imports/Migrationen wiederverwenden.

- Map: date/time filter, long-path geometry, graticule, band/mode filters, live refresh and optional aggregation/clustering.
- Reuse background progress UI for large multi-file imports.

- Map follow-up: date/time filter, long-path, graticule, live refresh, stream/overlay view.

- Map: date/time filter, long-path, graticule, mode filters, live refresh, aggregation/clustering.
- Consider persisting the single-map-window preference.

- Maidenhead grid overlay (zoom-dependent).
- Map date/time and mode filters; live refresh.
- Add band toggles/legend to the azimuth-distance view.

- Maidenhead grid overlay remains optional for a later release.

- Later: query/filter builder can feed the same QSO-selection-to-visualisation pipeline.

- Maidenhead grid overlay remains optional for a later release.

- Future (2.x): optional day/night/terminator rendering at an individual QSO's timestamp.
- Map later: Maidenhead grid overlay.


- Reuse the general progress UI for large multi-file imports.
- Queries/statistics: read-only SQL expert mode, saved queries, result table and result-to-map pipeline.
- Awards/diploma evaluation on top of the query layer.
- v2.x candidate: historical day/night terminator calculated for individual QSO timestamps.

- v0.12: first diploma/award views and reusable query templates based on real award requirements.
- Add friendly schema/help panel and later a visual query builder after recurring query patterns are known.


- WAE: Trophy and Classic variants on the same engine.
- WAE: extend Sicily special-call mapping when a reliable UcxLog/DCL source for non-IT9/IB9 special calls is identified.
- Awards: generic MEMBER-list provider for member-based awards such as W-DIG-M.
- Awards: map/result integration and reusable award-rule framework after the first real award evaluators settle.

- DCL: review unmatched/ambiguous import diagnostics against real exports and add safe matching fallbacks only where justified.

- DCL import: move the 7k+ matching run to a background/set-based operation with progress reporting.

- Awards/WAE: visually highlight entities that have reached the full 5 band points (not by color alone).
- DCL: add explicit manual assignment workflow for mode differences and genuine multiple candidates.
- DCL: background/set-based import with progress so the UI remains responsive.


- Map: add a small UI for selecting/editing tile providers instead of requiring manual `map-tiles.json` editing.
- Map: consider an optional provider requiring an API key only as a user-configured fallback, not a hard-coded dependency.

## DXCC overlay next phase

- [x] Audit the exported real-database country counts and add explicit mapping version 2 for all 188 observed, unambiguous UCX country classifications. Rerun `dxcc-audit` against the database to refresh exact DCL/UCX conflict totals after migration.
- [x] Replace snapshot-derived mapping version 2 with complete generated mapping version 3 from the official UcxLog 8.50 country resource, including current/deleted validation, unresolved WAE/R150S inventory, and R1FJ regression coverage.
- [x] Generate the complete offline Current/Deleted catalog from the Released ADIF 3.1.7 machine-readable DXCC enumeration; unlisted IDs remain `Unknown`.
- [x] Add configuration/file selection for the external user-provided `dxcc.geojson`; do not bundle the AmazingProducer data without redistribution clearance.
- [x] Add the optional Leaflet `DXCC status` layer below paths/markers, load normalized geometry once per page, and keep it outside `fitBounds`.
- [x] Add the `ucxSetDxccStatus`, `ucxSetDxccVisible`, and `ucxRefreshDxccStyles` page contracts and restyle resident polygons when selected bands change.
- [x] Add non-color visual distinctions and entity popups with ID, name, status, QSO count, worked bands, and confirmed bands.
- [x] Integrate complete aggregate snapshots with normal map refresh and Live Map without changing existing incremental marker/path behavior.
- [ ] Runtime-test the eventual WebView2/Leaflet overlay on Windows; Linux validation covers build/data tests only.
- [ ] If repeated-world DXCC vectors become important, design a tested antimeridian/world-copy strategy; the first implementation deliberately renders one correct primary-world vector copy.
