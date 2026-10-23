# Architecture

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
- `UcxStats.Data`: private SQLite persistence and later read-only statistics/query API.
- `UcxStats.Geo`: deterministic geographic calculations and later short/long-path geometry.
- `UcxStats.Map`: browser-neutral map-page generation, map path-width/external DXCC configuration, normalized GeoJSON serialization, and compact status payloads.
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
