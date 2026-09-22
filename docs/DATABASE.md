# SQLite persistence (v0.3)

UcxStats uses `Microsoft.Data.Sqlite` directly, without Entity Framework.

The current `qso` table stores the decoded UCX fields needed for statistics plus source metadata and locator-derived coordinates.

## Source identity

Each imported record is identified by:

- `source_file` - absolute UCX path
- `source_record_index` - zero-based 364-byte record index
- `source_record_hash` - SHA-256 of the raw 364 bytes

`UNIQUE(source_file, source_record_index)` makes re-import idempotent. If the hash is unchanged the row is skipped; if the hash changed the row is updated.

## Derived values

When a valid locator is present, v0.3 persists:

- locator-center `latitude`
- locator-center `longitude`
- `grid2`
- `grid4`
- `grid6`

Distance, bearing and short/long-path geometry are intentionally deferred until the station/home locator becomes configurable.

## Query safety

Future free-form user queries must be read-only. The planned expert query surface will only accept/read through `SELECT`/`WITH` statements and will not expose UCX files to SQL at all.

### Geo columns
`locator`, `latitude`, `longitude`, `grid2`, `grid4`, `grid6`, and `grid8` are stored with each QSO. Latitude/longitude use the center of the full available 2/4/6/8-character Maidenhead locator. Existing databases are migrated automatically to add `grid8`.
