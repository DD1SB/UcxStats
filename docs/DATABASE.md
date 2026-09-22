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

## Numeric DXCC identity

`qso.dxcc_entity_id INTEGER NULL` stores the effective numeric ADIF/DXCC entity identity. It is intentionally independent of the existing WAE entity resolver and may remain `NULL` when no unambiguous mapping is known.

Initialization performs an idempotent migration:

1. add the nullable column if absent;
2. create `idx_qso_dxcc_band` on `(dxcc_entity_id, band_code)` if absent;
3. run the versioned country-code backfill when the stored `dxcc_country_mapping_version` differs;
4. leave unknown country codes unresolved rather than guessing.

The backfill is repeatable and does not rewrite `country`, DCL data, or WAE subdivision data. Identity priority is: DCL code `999` or ADIF code `0` as explicit non-DXCC evidence; then a resolved UCX country identity; then a catalog-recognized normal DCL entity as fallback when UCX is unresolved. Both Current and Deleted catalog entities retain their numeric identity; deleted entities are not remapped and remain non-evaluable for the current-entity overlay. A normal UCX/DCL disagreement is reported by the audit API but retains UCX as the effective identity. Explicit non-DXCC evidence is reported separately and clears `dxcc_entity_id`. `APP_DCL_ADIF2` is not used by DXCC backfill; it remains available to the independent WAE resolver. Identity-resolution policy version 2 is unchanged; UCX mapping version 3 triggers only the normal safe identity backfill.

UCX-country mapping version 3 is generated from the entity-definition section of the official UcxLog 8.50 country resource, not from a user's QSO history. The source contains 455 entity-definition rows and 452 distinct case-insensitive classification labels. The generated inventory maps 340 labels to Current ADIF 3.1.7 entities and 62 `#...` labels to Deleted entities. Fifty UcxLog WAE/R150S-only subdivisions have no numeric DXCC ID in the source and remain `NULL`; no parent identity is inferred. Source duplicates `I`, `TA`, and `Z6` reconcile consistently. Every numeric target is independently validated by the ADIF catalog. The earlier 188-code mapping is stable except for authoritative correction `7Q: 432 -> 440` (Malawi). No callsign-prefix inference is performed.

When a stored database has mapping version 2, initialization reruns the same idempotent set-based identity calculation and then records version 3. A previously unresolved row such as `country='R1FJ'` therefore receives DXCC entity 61 without re-importing the UCX file. DCL 999/ADIF 0 suppression, UCX-first conflicts, catalog-validated DCL fallback, confirmation semantics, and WAE resolution are unchanged.

`IQsoRepository.GetDxccIdentityAuditAsync()` is the callable audit for a real database. It returns total/resolved/unresolved QSO counts, per-UCX-country mapping and QSO count, unresolved codes, evidence counts (`agreement`, `DCL fallback`, `UCX only`, `conflict`, `explicit non-DXCC`, `unresolved`), and exact conflict rows. No representative user database is committed to this repository, so the post-migration audit must be run against the user's database with `dotnet run --project tests/UcxStats.SmokeTest -- dxcc-audit "/path/to/ucxstats.db"`.

```csharp
var repository = new SqliteQsoRepository(@"C:\path\to\ucxstats.db");
DxccIdentityAuditResult audit = await repository.GetDxccIdentityAuditAsync();
```

`IQsoRepository.GetDxccStatusAsync(QsoScope)` aggregates all in-scope QSOs by effective entity and band. It does not join locator columns, so QSOs without a usable locator count. The aggregate exposes QSO count, eQSL/ClubLog count, and paper/LoTW count; DCL does not contribute to the latter two counts.

Map marker retrieval also accepts the same typed `QsoScope`. This keeps source, explicit-QSO-ID, source-record, and UTC-lower-bound selection consistent between marker/path data and DXCC status while deliberately retaining the marker query's locator/station-coordinate requirements. No polygon or calculated overlay state is persisted in SQLite.
