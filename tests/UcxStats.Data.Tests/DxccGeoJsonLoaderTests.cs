using System.Text;
using System.Text.Json;
using UcxStats.Data.Dxcc;

namespace UcxStats.Data.Tests;

public sealed class DxccGeoJsonLoaderTests
{
    [Fact]
    public void AcceptsPolygonAndMultiPolygon()
    {
        var json = Collection(
            Feature(1, "One", "ONE", Polygon(0)),
            Feature(2, "Two", "TWO", MultiPolygon(10)));

        var result = Load(json);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Features.Count);
        Assert.Equal(DxccGeometryType.Polygon, result.Features[0].Geometry.Type);
        Assert.Equal(DxccGeometryType.MultiPolygon, result.Features[1].Geometry.Type);
    }

    [Fact]
    public void RejectsNonFeatureCollectionAndUnsupportedGeometry()
    {
        var wrongRoot = Load("{\"type\":\"Feature\",\"features\":[]}");
        var wrongRootType = Load("{\"type\":42,\"features\":[]}");
        var empty = Load(Collection());
        var unsupported = Load(Collection(
            "{\"type\":\"Feature\",\"properties\":null,\"geometry\":{\"type\":\"Point\",\"coordinates\":[0,0]}}"));

        Assert.False(wrongRoot.IsValid);
        Assert.Contains(wrongRoot.Diagnostics, x => x.Code == "feature-collection");
        Assert.False(wrongRootType.IsValid);
        Assert.Contains(wrongRootType.Diagnostics, x => x.Code == "feature-collection");
        Assert.False(empty.IsValid);
        Assert.Contains(empty.Diagnostics, x => x.Code == "feature-count-empty");
        Assert.False(unsupported.IsValid);
        Assert.Contains(unsupported.Diagnostics, x => x.Code == "geometry-unsupported");
    }

    [Theory]
    [InlineData("[[[0,0],[1,0],[1,1]]]", "ring-invalid")]
    [InlineData("[[[0,0],[1,0],[1,1],[2,2]]]", "ring-open")]
    [InlineData("[[[181,0],[1,0],[1,1],[181,0]]]", "coordinate-range")]
    [InlineData("[[[0,91],[1,0],[1,1],[0,91]]]", "coordinate-range")]
    [InlineData("[[[\"zero\",0],[1,0],[1,1],[\"zero\",0]]]", "longitude-invalid")]
    public void RejectsMalformedGeometry(string coordinates, string diagnostic)
    {
        var geometry = $"{{\"type\":\"Polygon\",\"coordinates\":{coordinates}}}";
        var result = Load(Collection(Feature(1, "One", "ONE", geometry)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == diagnostic);
    }

    [Fact]
    public void ClampsTinyAntimeridianOverflowOnly()
    {
        var geometry = "{\"type\":\"Polygon\",\"coordinates\":[[[180.0000000005,0],[180,1],[179,0],[180.0000000005,0]]]}";
        var result = Load(Collection(Feature(1, "One", "ONE", geometry)));

        Assert.True(result.IsValid);
        Assert.Equal(180, result.Features[0].Geometry.Polygons[0].Rings[0].Positions[0].Longitude);
    }

    [Fact]
    public void RetainsNullPropertiesAsUnresolvedWithWarning()
    {
        var feature = $"{{\"type\":\"Feature\",\"properties\":null,\"geometry\":{Polygon(0)}}}";
        var result = Load(Collection(feature));

        Assert.True(result.IsValid);
        Assert.Null(result.Features[0].EntityId);
        Assert.Equal(DxccGeometryFeatureKind.Unresolved, result.Features[0].Kind);
        Assert.Contains(result.Diagnostics, x => x.Code == "properties-missing");
    }

    [Fact]
    public void ReportsDuplicateIdsAndDuplicateGeometry()
    {
        var sameGeometry = Polygon(0);
        var result = Load(Collection(
            Feature(1, "One", "ONE", sameGeometry),
            Feature(1, "Also One", "ONE2", sameGeometry)));

        Assert.True(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "duplicate-entity-id");
        Assert.Contains(result.Diagnostics, x => x.Code == "duplicate-geometry");
    }

    [Fact]
    public void ReportsSuspiciousButIntegerEntityIdWithoutInventingCorrection()
    {
        var result = Load(Collection(Feature(1_234, "Suspicious", "X", Polygon(0))));

        Assert.True(result.IsValid);
        Assert.Equal(1_234, result.Features[0].EntityId);
        Assert.Contains(result.Diagnostics, x => x.Code == "entity-id-suspicious");
    }

    [Fact]
    public void CrossChecksGeometryIdsAgainstReleasedCatalog()
    {
        var result = Load(Collection(
            Feature(281, "Spain", "EA", Polygon(0)),
            Feature(229, "German Democratic Republic", "Y2", Polygon(1)),
            Feature(998, "Unknown", "X", Polygon(2))));

        Assert.True(result.IsValid);
        Assert.Equal(3, result.UniqueDxccEntityIdCount);
        Assert.Equal(1, result.CurrentCatalogEntityIdCount);
        Assert.Equal(1, result.DeletedCatalogEntityIdCount);
        Assert.Equal(1, result.UnknownCatalogEntityIdCount);
        Assert.DoesNotContain(281, result.CurrentCatalogEntityIdsMissingGeometry);
        Assert.Contains(21, result.CurrentCatalogEntityIdsMissingGeometry);
        Assert.Equal([998], result.GeometryEntityIdsNotRecognizedByCatalog);
    }

    [Fact]
    public void EnforcesFileFeatureCoordinateAndDepthLimits()
    {
        var valid = Collection(Feature(1, "One", "ONE", Polygon(0)));
        var bytes = Encoding.UTF8.GetBytes(valid);

        Assert.Contains(
            new DxccGeoJsonLoader(limits: new DxccGeoJsonLimits(MaxFileBytes: bytes.Length - 1))
                .LoadBytes(bytes).Diagnostics,
            x => x.Code == "file-size-limit");
        Assert.Contains(
            new DxccGeoJsonLoader(limits: new DxccGeoJsonLimits(MaxFeatures: 0))
                .LoadBytes(bytes).Diagnostics,
            x => x.Code == "feature-count-limit");
        Assert.Contains(
            new DxccGeoJsonLoader(limits: new DxccGeoJsonLimits(MaxCoordinatePositions: 3))
                .LoadBytes(bytes).Diagnostics,
            x => x.Code == "coordinate-count-limit");
        Assert.Contains(
            new DxccGeoJsonLoader(limits: new DxccGeoJsonLimits(MaxJsonDepth: 2))
                .LoadBytes(bytes).Diagnostics,
            x => x.Code == "json-invalid");
    }

    [Fact]
    public void MissingFileDegradesGracefully()
    {
        var result = new DxccGeoJsonLoader().Load(
            Path.Combine(Path.GetTempPath(), $"missing-dxcc-{Guid.NewGuid():N}.geojson"));

        Assert.False(result.IsAvailable);
        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, x => x.Code == "file-missing");
    }

    [Fact]
    public void KnownProfileAppliesOnlyToExactKnownHash()
    {
        var json = KnownProfileFixture();
        var bytes = Encoding.UTF8.GetBytes(json);

        var known = new DxccGeoJsonLoader().LoadBytes(
            bytes, DxccGeoJsonLoader.KnownAmazingProducerSha256);
        var unknown = new DxccGeoJsonLoader().LoadBytes(bytes, new string('0', 64));

        Assert.True(known.IsKnownAmazingProducerSource);
        Assert.Contains(known.Features, x => x.Name == "Amsterdam & St. Paul Is." && x.EntityId == 10);
        Assert.Contains(known.Features, x => x.Name == "Monaco" && x.EntityId == 260);
        Assert.Contains(known.Features, x => x.Name == "West Malaysia" && x.EntityId == 299);
        Assert.Contains(known.Features, x => x.SourceIndex == 96 && x.EntityId == 522 && x.Prefix == "Z6");
        Assert.Contains(known.Features, x => x.SourceIndex == 153 &&
                                             x.EntityId is null &&
                                             x.Kind == DxccGeometryFeatureKind.NonDxcc);
        Assert.Single(known.Features, x => x.EntityId == 489);
        Assert.Contains(known.Diagnostics, x => x.Code == "known-duplicate-removed");

        Assert.False(unknown.IsKnownAmazingProducerSource);
        Assert.Contains(unknown.Features, x => x.Name == "Monaco" && x.EntityId == 206);
        Assert.DoesNotContain(unknown.Features, x => x.EntityId == 522);
        Assert.Equal(2, unknown.Features.Count(x => x.EntityId == 489));
        Assert.Contains(unknown.Diagnostics, x => x.Code == "source-unknown");
    }

    [Fact]
    public void DevelopmentFileNormalizesTo340UniqueNumericDxccIdsWhenPresent()
    {
        var path = FindDevelopmentGeoJson();
        if (path is null)
            return; // The external local resource is deliberately not a test prerequisite.

        var result = new DxccGeoJsonLoader().Load(path);

        Assert.True(result.IsValid);
        Assert.True(result.IsKnownAmazingProducerSource);
        Assert.Equal(DxccGeoJsonLoader.KnownAmazingProducerSha256, result.Sha256);
        Assert.Equal(341, result.Features.Count);
        Assert.Equal(340, result.UniqueDxccEntityIdCount);
        Assert.Equal(340, result.CurrentCatalogEntityIdCount);
        Assert.Equal(0, result.DeletedCatalogEntityIdCount);
        Assert.Equal(0, result.UnknownCatalogEntityIdCount);
        Assert.Empty(result.CurrentCatalogEntityIdsMissingGeometry);
        Assert.Empty(result.GeometryEntityIdsNotRecognizedByCatalog);
        Assert.Equal(16_626, result.CoordinateCount);
        var expectedSeparateEntities = new[]
        {
            5, 6, 9, 20, 21, 29, 32, 35, 38, 40, 43, 45, 103, 110, 117, 123,
            126, 138, 147, 166, 167, 171, 174, 180, 182, 189, 197, 202, 285, 289,
            297, 303, 515, 517, 518, 519, 520, 522
        };
        foreach (var entityId in expectedSeparateEntities)
            Assert.Single(result.Features, x => x.EntityId == entityId);
        Assert.Contains(result.Features, x => x.Kind == DxccGeometryFeatureKind.NonDxcc && x.EntityId is null);
    }

    private static DxccGeoJsonLoadResult Load(string json) =>
        new DxccGeoJsonLoader().LoadBytes(Encoding.UTF8.GetBytes(json), new string('1', 64));

    private static string Collection(params string[] features) =>
        $"{{\"type\":\"FeatureCollection\",\"features\":[{string.Join(',', features)}]}}";

    private static string Feature(int id, string name, string prefix, string geometry) =>
        $"{{\"type\":\"Feature\",\"properties\":{{\"dxcc_entity_code\":{id}," +
        $"\"dxcc_name\":{JsonSerializer.Serialize(name)},\"dxcc_prefix\":{JsonSerializer.Serialize(prefix)}}}," +
        $"\"geometry\":{geometry}}}";

    private static string NullFeature(string geometry) =>
        $"{{\"type\":\"Feature\",\"properties\":null,\"geometry\":{geometry}}}";

    private static string Polygon(double offset) =>
        $"{{\"type\":\"Polygon\",\"coordinates\":[[[{offset},0],[{offset + 0.5},0],[{offset + 0.5},0.5],[{offset},0]]]}}";

    private static string MultiPolygon(double offset) =>
        $"{{\"type\":\"MultiPolygon\",\"coordinates\":[[[[{offset},0],[{offset + 0.5},0],[{offset + 0.5},0.5],[{offset},0]]]]}}";

    private static string KnownProfileFixture()
    {
        var features = new string[154];
        for (var i = 0; i < features.Length; i++)
            features[i] = Feature(600 + i, $"Filler {i}", $"X{i}", Polygon(-100 + i * 0.01));

        features[0] = Feature(131, "Amsterdam & St. Paul Is.", "FT/Z", Polygon(-10));
        features[1] = Feature(206, "Monaco", "3A", Polygon(-9));
        features[2] = Feature(229, "West Malaysia", "9M2", Polygon(-8));
        features[3] = Feature(489, "Conway Reef", "3D2/C", Polygon(-7));
        features[4] = Feature(489, "Conway Reef", "3D2/C", Polygon(-7));
        features[96] = NullFeature(Polygon(-6));
        features[153] = NullFeature(Polygon(-5));
        return Collection(features);
    }

    private static string? FindDevelopmentGeoJson()
    {
        var configured = Environment.GetEnvironmentVariable("UCXSTATS_DXCC_GEOJSON");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data", "dxcc.geojson");
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        return null;
    }
}
