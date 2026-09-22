using System.Text.Json;
using UcxStats.Data.Database;
using UcxStats.Data.Dxcc;
using UcxStats.Map;

namespace UcxStats.Data.Tests;

public sealed class DxccMapIntegrationTests
{
    [Fact]
    public void MissingConfigurationIsOptionalAndDefaultsToDisabled()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "map-dxcc.json");
            var result = new DxccMapConfigurationStore(path).Load();

            Assert.Null(result.Error);
            Assert.Null(result.Configuration.GeoJsonPath);
            Assert.False(result.Configuration.EnabledByDefault);
            Assert.False(File.Exists(path));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ConfigurationPersistsAbsoluteExternalPathAndDefaultVisibility()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configPath = Path.Combine(directory, "map-dxcc.json");
            var geoJsonPath = Path.Combine(directory, "dxcc.geojson");
            var store = new DxccMapConfigurationStore(configPath);

            store.Save(new DxccMapConfiguration(geoJsonPath, true));
            var loaded = store.Load();

            Assert.Null(loaded.Error);
            Assert.Equal(geoJsonPath, loaded.Configuration.GeoJsonPath);
            Assert.True(loaded.Configuration.EnabledByDefault);
            Assert.Contains("GeoJsonPath", File.ReadAllText(configPath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void RelativeConfiguredPathIsRejectedWithoutOpeningOrCreatingGeometry()
    {
        var directory = CreateTempDirectory();
        try
        {
            var configPath = Path.Combine(directory, "map-dxcc.json");
            File.WriteAllText(configPath, "{\"GeoJsonPath\":\"dxcc.geojson\"}");

            var loaded = new DxccMapConfigurationStore(configPath).Load();

            Assert.NotNull(loaded.Error);
            Assert.Null(loaded.Configuration.GeoJsonPath);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void ConfiguredValidMissingAndInvalidGeoJsonRemainDistinctDiagnostics()
    {
        var directory = CreateTempDirectory();
        try
        {
            var validPath = Path.Combine(directory, "valid.geojson");
            var invalidPath = Path.Combine(directory, "invalid.geojson");
            var missingPath = Path.Combine(directory, "missing.geojson");
            File.WriteAllText(validPath, FeatureCollection(281, "Spain", "EA"));
            File.WriteAllText(invalidPath, "not json");

            var loader = new DxccGeoJsonLoader();
            Assert.True(loader.Load(validPath).IsValid);
            Assert.Contains(loader.Load(missingPath).Diagnostics, x => x.Code == "file-missing");
            Assert.Contains(loader.Load(invalidPath).Diagnostics, x => x.Code == "json-invalid");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void NormalizedGeoJsonUsesNumericIdsAndCatalogMetadata()
    {
        var directory = CreateTempDirectory();
        try
        {
            var path = Path.Combine(directory, "dxcc.geojson");
            File.WriteAllText(path, FeatureCollection(281, "Source name", "EA"));
            var loaded = new DxccGeoJsonLoader().Load(path);

            var normalized = DxccMapGeoJsonSerializer.Serialize(loaded);
            using var document = JsonDocument.Parse(normalized);
            var properties = document.RootElement.GetProperty("features")[0].GetProperty("properties");

            Assert.Equal(281, properties.GetProperty("entityId").GetInt32());
            Assert.Equal("EA", properties.GetProperty("prefix").GetString());
            Assert.Equal("Current", properties.GetProperty("catalogStatus").GetString());
            Assert.NotEqual("Source name", properties.GetProperty("name").GetString());
            Assert.DoesNotContain("dxcc_entity_code", normalized);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void StatusPayloadIsCompactNumericPerBandAggregation()
    {
        var snapshot = new DxccStatusSnapshot(
            [new DxccEntityBandStatus(522, 4, 19, 7, 3)],
            UnresolvedQsoCount: 2,
            ExplicitNonDxccQsoCount: 1);

        var json = DxccMapPayloadFactory.Serialize(DxccMapPayloadFactory.Create(snapshot));
        using var document = JsonDocument.Parse(json);
        var band = document.RootElement.GetProperty("bands")[0];

        Assert.Equal(522, band.GetProperty("entityId").GetInt32());
        Assert.Equal(4, band.GetProperty("bandCode").GetInt32());
        Assert.Equal(19, band.GetProperty("qsoCount").GetInt32());
        Assert.Equal(7, band.GetProperty("otherConfirmedCount").GetInt32());
        Assert.Equal(3, band.GetProperty("dxccCreditedCount").GetInt32());
        Assert.Equal("DxccCredited", band.GetProperty("status").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("unresolvedQsoCount").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("explicitNonDxccQsoCount").GetInt32());
        Assert.DoesNotContain("name", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GeneratedMapContainsResidentNumericDxccContractAndAllFiveStyles()
    {
        var html = BuildPage(geometryAvailable: true);

        Assert.Contains("id=\"ctl-dxcc\"", html);
        Assert.Contains("window.ucxSetDxccStatus", html);
        Assert.Contains("window.ucxSetDxccVisible", html);
        Assert.Contains("type:'dxccVisibility'", html);
        Assert.Contains("window.ucxRefreshDxccStyles", html);
        Assert.Contains("L.geoJSON", html);
        Assert.Contains("properties?.entityId", html);
        Assert.Contains("setStyle(dxccStyle", html);
        Assert.Contains("Unknown:{", html);
        Assert.Contains("NotWorked:{", html);
        Assert.Contains("Worked:{", html);
        Assert.Contains("OtherConfirmed:{", html);
        Assert.Contains("DxccCredited:{", html);
        Assert.Contains("No bands selected", html);
        Assert.Contains("DXCC status", html);
        Assert.Contains("eQSL / ClubLog confirmed", html);
        Assert.Contains("LoTW / Paper confirmed", html);
        Assert.Contains("DXCC ${Number(properties.entityId)}", html);
        Assert.Contains("Selected-band QSOs", html);
    }

    [Fact]
    public void GeneratedMapShowsUnavailableControlWithoutRemovingExistingFeatures()
    {
        var html = BuildPage(geometryAvailable: false);

        Assert.Contains("geometryAvailable:false", html);
        Assert.Contains("id=\"ctl-dxcc\" type=\"checkbox\"${dxccDisabled}", html);
        Assert.Contains("Shortpath", html);
        Assert.Contains("Longpath", html);
        Assert.Contains("Tag/Nacht", html);
        Assert.Contains("longGreatCircle", html);
        Assert.Contains("updateNightLayer", html);
        Assert.Contains("window.ucxAppendQsos", html);
        Assert.Contains("window.ucxReplaceQsos", html);
        Assert.Contains("window.ucxApplyRemoteView", html);
        Assert.Contains("https://tiles.example/{z}/{x}/{y}.png", html);
        Assert.Contains("map.fitBounds(bounds", html);
        Assert.DoesNotContain("dxccLayer.getBounds", html);
    }

    [Fact]
    public void GeneratedMapDefinesExplicitPaneOrderAndBandRestyleHook()
    {
        var html = BuildPage(geometryAvailable: true);

        Assert.Contains("ucxDxccPane').style.zIndex='320'", html);
        Assert.Contains("ucxDxccPane').style.pointerEvents='auto'", html);
        Assert.DoesNotContain("ucxDxccPane').style.pointerEvents='none'", html);
        Assert.Contains("ucxNightPane').style.zIndex='340'", html);
        Assert.Contains("ucxPathPane').style.zIndex='410'", html);
        Assert.Contains("ucxMarkerPane').style.zIndex='430'", html);
        Assert.Contains("b.enabled=e.target.checked;refreshBand(b.code);window.ucxRefreshDxccStyles()", html);
        Assert.Contains("fetch(dxccConfig.url)", html);
    }

    [Fact]
    public void GeneratedDxccFeaturesAreInteractiveAndHaveCurrentBandPopupHandling()
    {
        var html = BuildPage(geometryAvailable: true);

        Assert.Contains("interactive:true", html);
        Assert.Contains("onEachFeature:(feature,layer)=>", html);
        Assert.Contains("layer.options.interactive=true", html);
        Assert.Contains("layer.bindPopup(()=>buildDxccPopupSafely(feature)", html);
        Assert.Contains("const summary=summarizeDxcc(properties)", html);
        Assert.Contains("const selected=selectedBandCodes()", html);
        Assert.Contains("Number(feature?.properties?.entityId)", html);
    }

    [Fact]
    public void GeneratedDxccPageFallsBackToPolygonHitTestingBelowResidentCanvas()
    {
        var html = BuildPage(geometryAvailable: true);

        Assert.Contains("map.on('click',handleDxccMapClick)", html);
        Assert.Contains("event.sourceTarget&&event.sourceTarget!==map", html);
        Assert.Contains("layer._containsPoint(point)", html);
        Assert.Contains("layer.openPopup(latlng)", html);
        Assert.Contains("type:'dxccError',phase", html);
    }

    private static string BuildPage(bool geometryAvailable)
    {
        var points = new[]
        {
            new MapQsoPoint(
                1, "/logs/a.ucx", "Z60A", "2026-01-01", "12:00:00", 4, 0,
                "JN58", 48.5, 11.0, "Home", 48.0, 11.5, 50, 90)
        };
        var status = DxccMapPayloadFactory.Create(new DxccStatusSnapshot(
            [new DxccEntityBandStatus(522, 4, 1, 1, 0)], 0));
        var dxcc = geometryAvailable
            ? new MapDxccPageOptions(true, false, "https://ucxstats.local/dxcc.geojson", status, "ready")
            : MapDxccPageOptions.Unavailable("no GeoJSON configured");
        return MapPageBuilder.Build(
            points,
            new MapTileDefinition("Test", "https://tiles.example/{z}/{x}/{y}.png", "Test attribution", 18),
            dxcc);
    }

    private static string FeatureCollection(int id, string name, string prefix) =>
        JsonSerializer.Serialize(new
        {
            type = "FeatureCollection",
            features = new[]
            {
                new
                {
                    type = "Feature",
                    properties = new
                    {
                        dxcc_entity_code = id,
                        dxcc_name = name,
                        dxcc_prefix = prefix
                    },
                    geometry = new
                    {
                        type = "Polygon",
                        coordinates = new double[][][]
                        {
                            new double[][]
                            {
                                [0, 0], [1, 0], [1, 1], [0, 0]
                            }
                        }
                    }
                }
            }
        });

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ucxstats-map-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
