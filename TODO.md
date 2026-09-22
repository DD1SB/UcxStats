# UcxStats – TODO

Open development tasks and ideas only. Completed work belongs in `DONE.md`.

## Near term

### DXCC map overlay

- [ ] Complete Windows/WebView2 acceptance testing with real log data after
  the polygon-popup interaction fix:
  - normal map and Live Map;
  - band switching;
  - polygon popups over empty entity areas and after band changes;
  - locator-less QSOs;
  - day/night interaction;
  - short/long paths;
  - synchronized and independent map windows;
  - single-window reuse;
  - Pacific/date-line behavior.
- [ ] If repeated-world DXCC vectors become important, design and test an
  antimeridian/world-copy strategy. The current implementation deliberately
  renders one correct primary-world vector copy.
- [ ] Review DXCC overlay styling after practical use:
  - reduce polygon fill opacity;
  - use thinner / less dominant borders;
  - make `NotWorked` more prominent than already credited entities;
  - retain color + line-style distinctions for accessibility;
  - reduce the salmon/pink appearance of transparent red areas;
  - consider `Subtle` / `Balanced` / `Strong` style presets;
  - review legend size and visual weight.
- [ ] Consider QSO/result drill-down from DXCC entity popups after the basic
  overlay UX has settled.

### Map

- [ ] Add optional zoom-dependent Maidenhead grid overlay.
- [ ] Add mode filtering to the map.
- [ ] Review optional date/time filtering for normal map views beyond the
  existing Live Map start-time scope.
- [ ] Add optional aggregation/clustering if dense QSO displays require it.
- [ ] Add a small UI for selecting/editing tile providers instead of requiring
  manual `map-tiles.json` editing.
- [ ] Consider an optional API-key-based tile provider as a user-configured
  fallback, never as a hard-coded dependency.
- [ ] Consider persisting the single-map-window preference.
- [ ] Future 2.x: optional historical day/night/terminator rendering at an
  individual QSO timestamp.
- [ ] Add OBS/browser-source-friendly presentation, ideally with optional
  transparent background.
- [ ] Consider optional HTTP/WebSocket live updates for external
  browser/projector/stream clients.

## Awards / diplomas

### WAE

- [ ] Add WAE Trophy variant to the existing WAE engine.
- [ ] Add WAE Classic variant.
- [ ] Visually distinguish entities that have reached the full five-band
  score; do not rely on color alone.
- [ ] Extend Sicily special-call handling if a reliable UcxLog/DCL source for
  non-IT9/IB9 WAE assignments is identified.

### Generic award infrastructure

- [ ] Continue evolving the reusable award-rule framework after additional
  real award evaluators have been implemented.
- [ ] Add reusable award/query templates where they simplify real diploma
  requirements.
- [ ] Add generic MEMBER-list support for member-based awards such as W-DIG-M.
- [ ] Integrate award results with query/result/map workflows.
- [ ] Avoid adding one main-window button per award; use the existing award
  selection/list concept.
- [ ] Highlight actionable award progress, e.g. missing entities or remaining
  band points, without relying solely on color.

## DCL

- [ ] Move large DCL matching/import operations to a background or set-based
  operation with progress reporting so the UI remains responsive.
- [ ] Continue reviewing unmatched and ambiguous DCL diagnostics against real
  exports; add matching fallbacks only when justified by evidence.
- [ ] Add an explicit manual assignment workflow for genuine multiple
  candidates or mode differences.
- [ ] Reuse the general progress UI for large DCL/import operations.

## Queries / statistics

- [ ] Add a database-backed QSO table/filtering workflow where useful beyond
  the current file/source-oriented views.
- [ ] Add selected-QSO detail view for:
  - RST/exchange;
  - QSL/confirmation sources;
  - IOTA;
  - district;
  - QSL manager;
  - award fields;
  - contest-specific fields.
- [ ] Add additional fixed statistics views based on actual usage.
- [ ] Add read-only expert SQL mode (`SELECT` / `WITH` only).
- [ ] Add saved queries.
- [ ] Add schema/help panel for the query system.
- [ ] Later derive a visual query builder from recurring real-world queries.
- [ ] Allow query/filter results to feed the common
  QSO-selection-to-visualisation pipeline.
- [ ] Add polar/azimuth statistics and further map integration.
- [ ] Add band toggles/legend to the azimuth-distance view.

## Live synchronization

- [ ] Watch active UCX log files for size/content changes where the current
  Live Map/import workflow does not already cover the requirement.
- [ ] Add fast-path handling for appended 364-byte records if measurements
  show it is useful.
- [ ] Detect modified/deleted UCX records using stable per-record identity or
  hashes.
- [ ] Synchronize UCX file truncation/deletion with the database safely.
- [ ] Publish newly imported/changed QSOs through a reusable internal update
  mechanism for UI/map consumers.

## Geo / station context

- [ ] Add UI for per-QSO station-profile override.
- [ ] Surface cached distance/bearing more broadly in QSO details and
  statistics.
- [ ] Review whether short-/long-path geometry should be persisted/cached
  outside the render path.
- [ ] Measure Geo rebuild runtime and storage load with substantially more
  than 10,000 QSOs.
- [ ] If necessary, tune chunking/batch sizes and SQLite WAL/PRAGMA settings.
- [ ] Reuse background-job/progress infrastructure for large imports,
  migrations and Geo rebuilds.

## UCX parser / reverse engineering

Keep unknown fields unknown until independently proven.

- [ ] Add small 364-byte regression fixtures for:
  - normal log records;
  - contest records;
  - CW;
  - SSB;
  - FT8;
  - microwave;
  - SAT.
- [ ] Determine whether record byte `0` should be exposed as
  `IsNormalLogRecord`, `IsContestRecord`, or remain raw.
- [ ] Determine the meaning of status bits:
  - `151 & 0x10`;
  - `151 & 0x04`;
  - `161 & 0x80`.
- [ ] Determine the semantic purpose of offset `243`.
- [ ] Investigate the structurally recognized unknown strings around offsets
  `162`, `255`, and `261`.
- [ ] Confirm remaining band IDs with real or controlled records:
  - 30 m;
  - 70 cm;
  - 23 cm;
  - 13 cm;
  - 3.4 GHz;
  - 5.6 GHz.
- [ ] Identify UcxLog codes for `unbestimmt` and `Rest` if they become useful.

## Contest-specific features

- [ ] Add contest-aware views without excluding contest QSOs from normal
  statistics.
- [ ] Add optional QTC parser/importer for contest-specific statistics.

## Longer-term ideas

- [ ] Historical per-QSO propagation/context views.
- [ ] Additional map overlays where they provide practical operating value.
- [ ] Consider a future WAE-specific geometry overlay while preserving the
  strict separation between DXCC entities and WAE-only subdivisions.
- [ ] If additional interactive polygon overlays are introduced, replace
  per-overlay click fallbacks with a shared vector interaction/hit-test
  architecture.
