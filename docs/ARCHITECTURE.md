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
