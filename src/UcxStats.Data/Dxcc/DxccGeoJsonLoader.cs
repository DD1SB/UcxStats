using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UcxStats.Data.Dxcc;

public sealed class DxccGeoJsonLoader
{
    public const string KnownAmazingProducerSha256 =
        "e9b086eff7fb690e9990e10855f3fb494f9d1115e4af1928f6a8b545efef4568";
    public const int KnownAmazingProducerNormalizationProfileVersion = 1;

    private const double CoordinateEpsilon = 1e-9;
    private readonly DxccGeoJsonLimits _limits;
    private readonly IDxccEntityCatalog _catalog;

    public DxccGeoJsonLoader(
        IDxccEntityCatalog? catalog = null,
        DxccGeoJsonLimits? limits = null)
    {
        _catalog = catalog ?? DxccEntityCatalog.ReleasedAdif317;
        _limits = limits ?? new DxccGeoJsonLimits();
    }

    public DxccGeoJsonLoadResult Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return Unavailable("file-missing", "DXCC GeoJSON file was not found.");

        try
        {
            var info = new FileInfo(path);
            if (info.Length > _limits.MaxFileBytes)
                return Invalid(null, "file-size-limit",
                    $"DXCC GeoJSON is {info.Length:N0} bytes; limit is {_limits.MaxFileBytes:N0} bytes.");

            return LoadBytes(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Unavailable("file-read", $"DXCC GeoJSON could not be read: {ex.Message}");
        }
    }

    internal DxccGeoJsonLoadResult LoadBytes(byte[] bytes, string? hashOverride = null)
    {
        if (bytes.LongLength > _limits.MaxFileBytes)
            return Invalid(null, "file-size-limit",
                $"DXCC GeoJSON is {bytes.LongLength:N0} bytes; limit is {_limits.MaxFileBytes:N0} bytes.");

        var hash = hashOverride ?? Convert.ToHexStringLower(SHA256.HashData(bytes));
        var knownSource = string.Equals(hash, KnownAmazingProducerSha256, StringComparison.OrdinalIgnoreCase);
        var diagnostics = new List<DxccGeoJsonDiagnostic>();
        var features = new List<DxccGeometryFeature>();
        var coordinateCount = 0;

        try
        {
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = _limits.MaxJsonDepth
            });

            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("type", out var rootType) ||
                rootType.ValueKind != JsonValueKind.String ||
                rootType.GetString() != "FeatureCollection" ||
                !root.TryGetProperty("features", out var featureArray) ||
                featureArray.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add(Error("feature-collection", "Root must be a GeoJSON FeatureCollection."));
                return Finish(false, knownSource, hash, [], diagnostics, coordinateCount);
            }

            if (featureArray.GetArrayLength() > _limits.MaxFeatures)
            {
                diagnostics.Add(Error("feature-count-limit",
                    $"Feature count {featureArray.GetArrayLength():N0} exceeds limit {_limits.MaxFeatures:N0}."));
                return Finish(false, knownSource, hash, [], diagnostics, coordinateCount);
            }
            if (featureArray.GetArrayLength() == 0)
            {
                diagnostics.Add(Error("feature-count-empty", "FeatureCollection contains no features."));
                return Finish(false, knownSource, hash, [], diagnostics, coordinateCount);
            }

            var index = 0;
            foreach (var featureElement in featureArray.EnumerateArray())
            {
                ParseFeature(featureElement, index, diagnostics, features, ref coordinateCount);
                if (coordinateCount > _limits.MaxCoordinatePositions)
                {
                    diagnostics.Add(Error("coordinate-count-limit",
                        $"Coordinate count exceeds limit {_limits.MaxCoordinatePositions:N0}."));
                    break;
                }
                index++;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            diagnostics.Add(Error("json-invalid", $"Invalid GeoJSON: {ex.Message}"));
        }

        if (diagnostics.Any(x => x.Severity == DxccGeoJsonDiagnosticSeverity.Error))
            return Finish(false, knownSource, hash, [], diagnostics, coordinateCount);

        if (knownSource)
        {
            features = ApplyKnownAmazingProducerProfile(features, diagnostics);
            var uniqueIds = features.Where(x => x.EntityId.HasValue)
                .Select(x => x.EntityId!.Value)
                .Distinct()
                .Count();
            if (uniqueIds != 340)
                diagnostics.Add(Warning("known-entity-count",
                    $"Known normalization profile v{KnownAmazingProducerNormalizationProfileVersion} " +
                    $"expected 340 unique DXCC IDs but found {uniqueIds}."));
        }

        AddDuplicateDiagnostics(features, diagnostics);

        if (!knownSource)
        {
            diagnostics.Add(new DxccGeoJsonDiagnostic(
                DxccGeoJsonDiagnosticSeverity.Information,
                "source-unknown",
                "GeoJSON hash is not the known AmazingProducer version; no source-specific corrections were applied."));
        }

        return Finish(true, knownSource, hash, features, diagnostics, coordinateCount);
    }

    private void ParseFeature(
        JsonElement element,
        int index,
        List<DxccGeoJsonDiagnostic> diagnostics,
        List<DxccGeometryFeature> features,
        ref int coordinateCount)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty("type", out var featureType) ||
            featureType.ValueKind != JsonValueKind.String ||
            featureType.GetString() != "Feature")
        {
            diagnostics.Add(Error("feature-invalid", "Feature must have type 'Feature'.", index));
            return;
        }

        int? entityId = null;
        string? name = null;
        string? prefix = null;
        string propertiesFingerprint;
        if (!element.TryGetProperty("properties", out var properties) ||
            properties.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            propertiesFingerprint = Fingerprint(properties.ValueKind == JsonValueKind.Undefined
                ? "<missing>"
                : "null");
            diagnostics.Add(Warning("properties-missing", "Feature properties are missing or null.", index));
        }
        else if (properties.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Error("properties-invalid", "Feature properties must be an object or null.", index));
            return;
        }
        else
        {
            propertiesFingerprint = Fingerprint(properties.GetRawText());
            entityId = ReadEntityId(properties, index, diagnostics);
            name = ReadNullableString(properties, "dxcc_name", index, diagnostics);
            prefix = ReadNullableString(properties, "dxcc_prefix", index, diagnostics);
            if (entityId is null || name is null || prefix is null)
                diagnostics.Add(Warning("properties-incomplete", "DXCC ID, name, or prefix is null/missing.", index));
        }

        if (!element.TryGetProperty("geometry", out var geometryElement) ||
            geometryElement.ValueKind != JsonValueKind.Object)
        {
            diagnostics.Add(Error("geometry-missing", "Feature geometry is missing or invalid.", index));
            return;
        }

        var geometry = ParseGeometry(geometryElement, index, diagnostics, ref coordinateCount);
        if (geometry is null)
            return;

        var fingerprint = GeometryFingerprint(geometry);
        features.Add(new DxccGeometryFeature(
            index,
            entityId,
            name,
            prefix,
            entityId.HasValue ? DxccGeometryFeatureKind.DxccEntity : DxccGeometryFeatureKind.Unresolved,
            geometry,
            fingerprint,
            propertiesFingerprint));
    }

    private static int? ReadEntityId(
        JsonElement properties,
        int index,
        List<DxccGeoJsonDiagnostic> diagnostics)
    {
        if (!properties.TryGetProperty("dxcc_entity_code", out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var entityId) || entityId <= 0)
        {
            diagnostics.Add(Error("entity-id-invalid", "DXCC entity ID must be a positive integer or null.", index));
            return null;
        }
        if (entityId > 999)
            diagnostics.Add(Warning("entity-id-suspicious",
                $"DXCC entity ID {entityId} is outside the expected ADIF range.", index));
        return entityId;
    }

    private static string? ReadNullableString(
        JsonElement properties,
        string propertyName,
        int index,
        List<DxccGeoJsonDiagnostic> diagnostics)
    {
        if (!properties.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;
        if (value.ValueKind != JsonValueKind.String)
        {
            diagnostics.Add(Error("property-invalid", $"Property '{propertyName}' must be a string or null.", index));
            return null;
        }
        return value.GetString();
    }

    private DxccGeometry? ParseGeometry(
        JsonElement element,
        int featureIndex,
        List<DxccGeoJsonDiagnostic> diagnostics,
        ref int coordinateCount)
    {
        if (!element.TryGetProperty("type", out var typeElement) ||
            typeElement.ValueKind != JsonValueKind.String ||
            !element.TryGetProperty("coordinates", out var coordinates) ||
            coordinates.ValueKind != JsonValueKind.Array)
        {
            diagnostics.Add(Error("geometry-invalid", "Geometry type or coordinates are missing.", featureIndex));
            return null;
        }

        var type = typeElement.GetString();
        if (type == "Polygon")
        {
            var polygon = ParsePolygon(coordinates, featureIndex, diagnostics, ref coordinateCount);
            return polygon is null
                ? null
                : new DxccGeometry(DxccGeometryType.Polygon, [polygon]);
        }

        if (type == "MultiPolygon")
        {
            if (coordinates.GetArrayLength() == 0)
            {
                diagnostics.Add(Error("geometry-empty", "MultiPolygon contains no polygons.", featureIndex));
                return null;
            }

            var polygons = new List<DxccPolygonGeometry>();
            foreach (var polygonElement in coordinates.EnumerateArray())
            {
                var polygon = ParsePolygon(polygonElement, featureIndex, diagnostics, ref coordinateCount);
                if (polygon is null) return null;
                polygons.Add(polygon);
            }
            return new DxccGeometry(DxccGeometryType.MultiPolygon, polygons);
        }

        diagnostics.Add(Error("geometry-unsupported",
            $"Geometry type '{type ?? "<null>"}' is not supported; expected Polygon or MultiPolygon.",
            featureIndex));
        return null;
    }

    private DxccPolygonGeometry? ParsePolygon(
        JsonElement element,
        int featureIndex,
        List<DxccGeoJsonDiagnostic> diagnostics,
        ref int coordinateCount)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() == 0)
        {
            diagnostics.Add(Error("polygon-empty", "Polygon contains no rings.", featureIndex));
            return null;
        }

        var rings = new List<DxccLinearRing>();
        foreach (var ringElement in element.EnumerateArray())
        {
            if (ringElement.ValueKind != JsonValueKind.Array || ringElement.GetArrayLength() < 4)
            {
                diagnostics.Add(Error("ring-invalid", "A linear ring requires at least four positions.", featureIndex));
                return null;
            }

            var positions = new List<DxccPosition>();
            foreach (var positionElement in ringElement.EnumerateArray())
            {
                var position = ParsePosition(positionElement, featureIndex, diagnostics);
                if (position is null) return null;
                positions.Add(position.Value);
                coordinateCount++;
                if (coordinateCount > _limits.MaxCoordinatePositions)
                    return null;
            }

            if (positions[0] != positions[^1])
            {
                diagnostics.Add(Error("ring-open", "Linear ring is not closed.", featureIndex));
                return null;
            }
            rings.Add(new DxccLinearRing(positions));
        }
        return new DxccPolygonGeometry(rings);
    }

    private static DxccPosition? ParsePosition(
        JsonElement element,
        int featureIndex,
        List<DxccGeoJsonDiagnostic> diagnostics)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 2)
        {
            diagnostics.Add(Error("position-invalid", "Coordinate position requires longitude and latitude.", featureIndex));
            return null;
        }

        using var enumerator = element.EnumerateArray();
        enumerator.MoveNext();
        if (enumerator.Current.ValueKind != JsonValueKind.Number ||
            !enumerator.Current.TryGetDouble(out var longitude))
        {
            diagnostics.Add(Error("longitude-invalid", "Longitude is not numeric.", featureIndex));
            return null;
        }
        enumerator.MoveNext();
        if (enumerator.Current.ValueKind != JsonValueKind.Number ||
            !enumerator.Current.TryGetDouble(out var latitude) ||
            !double.IsFinite(longitude) || !double.IsFinite(latitude))
        {
            diagnostics.Add(Error("coordinate-invalid", "Coordinate is not a finite number.", featureIndex));
            return null;
        }

        if (longitude is > 180 and <= 180 + CoordinateEpsilon) longitude = 180;
        if (longitude is < -180 and >= -180 - CoordinateEpsilon) longitude = -180;
        if (latitude is > 90 and <= 90 + CoordinateEpsilon) latitude = 90;
        if (latitude is < -90 and >= -90 - CoordinateEpsilon) latitude = -90;

        if (longitude is < -180 or > 180 || latitude is < -90 or > 90)
        {
            diagnostics.Add(Error("coordinate-range",
                $"Coordinate [{longitude}, {latitude}] is outside GeoJSON longitude/latitude ranges.",
                featureIndex));
            return null;
        }
        return new DxccPosition(longitude, latitude);
    }

    private static List<DxccGeometryFeature> ApplyKnownAmazingProducerProfile(
        IReadOnlyList<DxccGeometryFeature> input,
        List<DxccGeoJsonDiagnostic> diagnostics)
    {
        var result = new List<DxccGeometryFeature>(input.Count);
        var conwayFingerprints = new HashSet<(string Geometry, string Properties)>();

        foreach (var original in input)
        {
            var feature = original;
            if (feature.EntityId == 131 && Equal(feature.Name, "Amsterdam & St. Paul Is."))
                feature = Correct(feature, 10, diagnostics, "Amsterdam & St. Paul Is.: 131 -> 10");
            else if (feature.EntityId == 206 && Equal(feature.Name, "Monaco"))
                feature = Correct(feature, 260, diagnostics, "Monaco: 206 -> 260");
            else if (feature.EntityId == 229 && Equal(feature.Name, "West Malaysia"))
                feature = Correct(feature, 299, diagnostics, "West Malaysia: 229 -> 299");
            else if (feature.SourceIndex == 96 && feature.EntityId is null)
            {
                feature = feature with
                {
                    EntityId = 522,
                    Name = "Republic of Kosovo",
                    Prefix = "Z6",
                    Kind = DxccGeometryFeatureKind.DxccEntity
                };
                diagnostics.Add(Info("known-correction", "Kosovo null properties -> DXCC 522.", feature.SourceIndex));
            }
            else if (feature.SourceIndex == 153 && feature.EntityId is null)
            {
                feature = feature with
                {
                    Name = feature.Name ?? "Somaliland",
                    Kind = DxccGeometryFeatureKind.NonDxcc
                };
                diagnostics.Add(Info("known-non-dxcc", "Somaliland retained as non-DXCC geometry.", feature.SourceIndex));
            }

            if (feature.EntityId == 489 && Equal(feature.Name, "Conway Reef") &&
                !conwayFingerprints.Add((feature.GeometryFingerprint, feature.PropertiesFingerprint)))
            {
                diagnostics.Add(Info("known-duplicate-removed",
                    "Removed exact duplicate Conway Reef feature.", feature.SourceIndex));
                continue;
            }
            result.Add(feature);
        }
        return result;
    }

    private static DxccGeometryFeature Correct(
        DxccGeometryFeature feature,
        int entityId,
        List<DxccGeoJsonDiagnostic> diagnostics,
        string message)
    {
        diagnostics.Add(Info("known-correction", message, feature.SourceIndex));
        return feature with { EntityId = entityId, Kind = DxccGeometryFeatureKind.DxccEntity };
    }

    private static void AddDuplicateDiagnostics(
        IReadOnlyList<DxccGeometryFeature> features,
        List<DxccGeoJsonDiagnostic> diagnostics)
    {
        foreach (var group in features.Where(x => x.EntityId.HasValue).GroupBy(x => x.EntityId!.Value))
        {
            if (group.Count() > 1)
                diagnostics.Add(Warning("duplicate-entity-id",
                    $"DXCC entity ID {group.Key} occurs in {group.Count()} features."));
        }

        foreach (var group in features.GroupBy(x => x.GeometryFingerprint, StringComparer.Ordinal))
        {
            if (group.Count() > 1)
                diagnostics.Add(Warning("duplicate-geometry",
                    $"Identical geometry occurs in {group.Count()} features."));
        }
    }

    private DxccGeoJsonLoadResult Finish(
        bool valid,
        bool knownSource,
        string? hash,
        IReadOnlyList<DxccGeometryFeature> features,
        IReadOnlyList<DxccGeoJsonDiagnostic> diagnostics,
        int coordinateCount)
    {
        var ids = features.Where(x => x.EntityId.HasValue).Select(x => x.EntityId!.Value).Distinct().ToArray();
        var current = ids.Count(x => _catalog.Find(x)?.Status == DxccCatalogEntityStatus.Current);
        var deleted = ids.Count(x => _catalog.Find(x)?.Status == DxccCatalogEntityStatus.Deleted);
        var unknown = ids.Count(x => _catalog.Find(x)?.Status is null or DxccCatalogEntityStatus.Unknown);
        var geometryIds = ids.ToHashSet();
        var missingCurrent = _catalog.Entries
            .Where(x => x.Status == DxccCatalogEntityStatus.Current && !geometryIds.Contains(x.EntityId))
            .Select(x => x.EntityId)
            .Order()
            .ToArray();
        var unrecognizedGeometry = ids
            .Where(x => _catalog.Find(x) is null)
            .Order()
            .ToArray();
        return new DxccGeoJsonLoadResult(
            true, valid, knownSource, hash, features, diagnostics, coordinateCount,
            ids.Length, current, deleted, unknown)
        {
            CurrentCatalogEntityIdsMissingGeometry = missingCurrent,
            GeometryEntityIdsNotRecognizedByCatalog = unrecognizedGeometry
        };
    }

    private static string GeometryFingerprint(DxccGeometry geometry)
    {
        var json = JsonSerializer.Serialize(geometry);
        return Fingerprint(json);
    }

    private static string Fingerprint(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static bool Equal(string? left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static DxccGeoJsonLoadResult Unavailable(string code, string message) =>
        new(false, false, false, null, [], [Error(code, message)], 0, 0, 0, 0, 0);

    private static DxccGeoJsonLoadResult Invalid(string? hash, string code, string message) =>
        new(true, false, false, hash, [], [Error(code, message)], 0, 0, 0, 0, 0);

    private static DxccGeoJsonDiagnostic Error(string code, string message, int? index = null) =>
        new(DxccGeoJsonDiagnosticSeverity.Error, code, message, index);

    private static DxccGeoJsonDiagnostic Warning(string code, string message, int? index = null) =>
        new(DxccGeoJsonDiagnosticSeverity.Warning, code, message, index);

    private static DxccGeoJsonDiagnostic Info(string code, string message, int? index = null) =>
        new(DxccGeoJsonDiagnosticSeverity.Information, code, message, index);
}
