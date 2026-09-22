# DXCC map overlay

## External configuration

UcxStats does not distribute DXCC polygon data. Use **DXCC-Karte…** to select an absolute path to an external `dxcc.geojson`. The setting is stored in `%LOCALAPPDATA%\UcxStats\map-dxcc.json`; `EnabledByDefault` is optional and defaults to `false`.

Selection runs the existing `DxccGeoJsonLoader`. Missing or invalid data is reported in the settings dialog and map status line but never blocks the normal map. Unknown hashes receive general structural validation only; the AmazingProducer corrections are applied exclusively to the known SHA-256 profile.

For each generated page, the validated normalized document is written to that map window's temporary `MapWeb` folder and fetched through the existing `https://ucxstats.local` virtual host. It is not copied to SQLite or the application distribution.

## Status and interaction

Only ADIF-catalog `Current` entities participate in progress states:

| State | Fill | Outline | Meaning |
|---|---|---|---|
| Not worked | red | dotted | no QSO on an enabled band |
| Worked | yellow | dashed | QSO, no Paper/LoTW/eQSL/ClubLog confirmation |
| eQSL / ClubLog confirmed | light blue | dash-dot | eQSL and/or ClubLog, no Paper/LoTW |
| LoTW / Paper confirmed | green | stronger solid | LoTW and/or received paper QSL |
| Unknown / deleted | gray | long dash, lower opacity | deleted, unknown, invalid, non-DXCC, or status unavailable |

DCL-only confirmation does not promote a polygon. With multiple enabled bands, the highest status wins. With no enabled bands, current entities show Not worked and the legend/popup explicitly says **No bands selected**.

Geometry remains resident and is indexed by normalized numeric DXCC ID. Band changes call `setStyle()`; names and prefixes are display metadata only. Popup band lists reflect the current band selection.

The pane order is base tiles, DXCC polygons, day/night shading, paths, then QSO/station markers and controls. DXCC geometry never contributes to `fitBounds`. View synchronization does not synchronize overlay visibility.

## Live Map and errors

Live Map keeps its existing incremental marker/path behavior. After a source refresh it recomputes and sends one complete compact aggregate snapshot. This happens even if no `MapQsoPoint` was added, so a locator-less QSO can change DXCC status without creating a marker.

No configuration, an empty path, a missing/invalid file, unknown-hash warnings, browser geometry-load failure, or status-query failure leaves the base map and all existing layers operational. Status-query failure renders geometry as non-evaluable rather than claiming entities are unworked.

## World wrapping

The initial implementation renders one correct primary-world vector overlay. Tiles, QSO/station markers, night shading, and long paths retain their existing adjacent-world behavior. Leaflet may therefore show no duplicate DXCC vector copy after panning into a repeated world. This avoids duplicate interactive polygons and fragile antimeridian rewriting; Fiji, Asiatic Russia, Alaska/Aleutians, Pacific entities, and Antarctica should be checked in the primary world on Windows.

## Windows acceptance checklist

1. Open a normal map with no configured GeoJSON; verify the disabled DXCC entry and all existing layers.
2. Use **DXCC-Karte…**, select a valid `dxcc.geojson`, and verify the validation summary.
3. Open a map and enable **DXCC status**.
4. Verify the known normalized data displays 340 current numeric entities.
5. Compare red/yellow/light-blue/green entities with known worked and confirmed examples.
6. Toggle bands and verify immediate restyling without a page reload or view change.
7. Clear every band and verify **No bands selected** behavior.
8. Open an entity popup and verify ID, name/prefix, status, QSO count, and band lists.
9. Add or select an in-scope QSO without a locator and verify it can affect entity status without adding a marker.
10. Enable day/night shading and verify both layers remain readable.
11. Toggle short and long paths and verify their appearance and popups remain unchanged.
12. Open synchronized maps and verify only pan/zoom is synchronized.
13. Disable single-window mode, open multiple maps, and verify independent DXCC visibility.
14. Enable single-window mode and reuse the map; verify window position/size/fullscreen behavior.
15. Run Live Map, log a new QSO, and verify marker behavior plus status refresh; also test a locator-less QSO.
16. Inspect date-line/Pacific entities in the primary world, including Fiji and Alaska/Aleutians.
17. Restart UcxStats and verify the external path persists.
18. Rename the configured GeoJSON temporarily and verify graceful degradation with the normal map intact.
