using System.Text.Json;
using System.Text.Json.Nodes;
using UcxStats.Map;

namespace UcxStats.Data.Tests;

public sealed class MapSettingsMigrationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ucxstats-map-migration-{Guid.NewGuid():N}");
    private string GeneralPath => Path.Combine(_directory, "map-settings.json");
    private string DxccPath => Path.Combine(_directory, "map-dxcc.json");
    private MapSettingsStore Store => new(GeneralPath, DxccPath);

    public MapSettingsMigrationTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void FreshInstallationDoesNotCreateFilesOrDirectoriesOnLoad()
    {
        var path = Path.Combine(_directory, "not-created", "map-settings.json");
        var result = new MapSettingsStore(path).Load();

        Assert.Null(result.Error);
        Assert.Equal(new MapSettings(1.3, 1), result.Settings);
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
        Assert.Empty(Directory.GetFiles(_directory));
        Assert.Equal("map-settings.json", Path.GetFileName(MapSettingsStore.DefaultPath));
        Assert.Equal(Path.GetDirectoryName(DxccMapConfigurationStore.DefaultPath),
            Path.GetDirectoryName(MapSettingsStore.DefaultPath));
    }

    [Fact]
    public void DxccWithoutLegacyWidthsIsUntouchedAndDoesNotCreateGeneralSettings()
    {
        var source = "{\n  \"EnabledByDefault\": true,\n  \"FutureDxcc\": {\"enabled\": true}\n}";
        File.WriteAllText(DxccPath, source);
        var timestamp = SetOldTimestamp(DxccPath);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(new MapSettings(), Store.Load().Settings);
            Assert.False(File.Exists(GeneralPath));
            Assert.Equal(source, File.ReadAllText(DxccPath));
            Assert.Equal(timestamp, File.GetLastWriteTimeUtc(DxccPath));
        }
    }

    [Fact]
    public void ValidGeneralSettingsLoadWithoutRewritingOrCreatingDxcc()
    {
        var source = "{ \"LongpathWidth\": 2.25, \"FuturePresentation\": [1,2], \"ShortpathWidth\": 1.75 }";
        File.WriteAllText(GeneralPath, source);
        var timestamp = SetOldTimestamp(GeneralPath);

        for (var i = 0; i < 3; i++)
        {
            var result = Store.Load();
            Assert.Null(result.Error);
            Assert.Equal(new MapSettings(1.75, 2.25), result.Settings);
            Assert.Equal(source, File.ReadAllText(GeneralPath));
            Assert.Equal(timestamp, File.GetLastWriteTimeUtc(GeneralPath));
            Assert.False(File.Exists(DxccPath));
        }
    }

    [Fact]
    public void MigrationCommitsGeneralWidthsBeforeCleaningDxccAndPreservesAllOtherFields()
    {
        var geometry = Path.Combine(_directory, "external.geojson");
        var original = new JsonObject
        {
            ["GeoJsonPath"] = geometry,
            ["EnabledByDefault"] = true,
            ["ShortpathWidth"] = 2.5,
            ["LongpathWidth"] = 3.5,
            ["UnknownObject"] = new JsonObject { ["nested"] = new JsonArray(1, "x", false, null) },
            ["UnknownNull"] = null,
            ["UnknownDxccWidth"] = 7.75
        };
        File.WriteAllText(DxccPath, original.ToJsonString());
        var writes = new List<string>();
        var store = new MapSettingsStore(GeneralPath, DxccPath, (path, document) =>
        {
            writes.Add(path);
            if (path == DxccPath)
            {
                Assert.Equal(new MapSettings(2.5, 3.5), ReadGeneral());
                Assert.NotNull(AtomicJsonFile.ReadObject(DxccPath)!["ShortpathWidth"]);
            }
            AtomicJsonFile.WriteIfChanged(path, document);
        });

        var result = store.Load();

        Assert.Null(result.Error);
        Assert.Equal(new MapSettings(2.5, 3.5), result.Settings);
        Assert.Equal(new[] { GeneralPath, DxccPath }, writes);
        Assert.Equal(result.Settings, ReadGeneral());
        original.Remove("ShortpathWidth");
        original.Remove("LongpathWidth");
        Assert.True(JsonNode.DeepEquals(original, AtomicJsonFile.ReadObject(DxccPath)));
        var dxcc = new DxccMapConfigurationStore(DxccPath).Load();
        Assert.Null(dxcc.Error);
        Assert.Equal(geometry, dxcc.Configuration.GeoJsonPath);
        Assert.True(dxcc.Configuration.EnabledByDefault);
        Assert.False(File.Exists(geometry));
    }

    [Fact]
    public void MigrationIsIdempotentAcrossNewStoreAndServiceInstances()
    {
        File.WriteAllText(DxccPath, """{"ShortpathWidth":1.75,"LongpathWidth":2.25,"EnabledByDefault":true}""");
        Assert.Null(Store.Load().Error);
        var generalBytes = File.ReadAllBytes(GeneralPath);
        var dxccBytes = File.ReadAllBytes(DxccPath);
        var generalTime = SetOldTimestamp(GeneralPath);
        var dxccTime = SetOldTimestamp(DxccPath);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(new MapSettings(1.75, 2.25), new MapSettingsService(Store).Current);
            Assert.Equal(generalBytes, File.ReadAllBytes(GeneralPath));
            Assert.Equal(dxccBytes, File.ReadAllBytes(DxccPath));
            Assert.Equal(generalTime, File.GetLastWriteTimeUtc(GeneralPath));
            Assert.Equal(dxccTime, File.GetLastWriteTimeUtc(DxccPath));
        }
    }

    [Theory]
    [InlineData("{}", "{}", 1.3, 1.0)]
    [InlineData("{}", "{\"ShortpathWidth\":2.5,\"LongpathWidth\":3.5}", 2.5, 3.5)]
    [InlineData("{\"ShortpathWidth\":4.5,\"LongpathWidth\":5.5}", "{\"ShortpathWidth\":2.5,\"LongpathWidth\":3.5}", 4.5, 5.5)]
    [InlineData("{\"ShortpathWidth\":4.5}", "{\"ShortpathWidth\":2.5,\"LongpathWidth\":3.5}", 4.5, 3.5)]
    [InlineData("{\"LongpathWidth\":5.5}", "{\"ShortpathWidth\":2.5,\"LongpathWidth\":3.5}", 2.5, 5.5)]
    [InlineData("{\"ShortpathWidth\":0,\"LongpathWidth\":5.5}", "{\"ShortpathWidth\":2.5}", 2.5, 5.5)]
    [InlineData("{\"ShortpathWidth\":4.5,\"LongpathWidth\":\"bad\"}", "{\"LongpathWidth\":3.5}", 4.5, 3.5)]
    [InlineData("{\"ShortpathWidth\":null,\"LongpathWidth\":9}", "{\"ShortpathWidth\":-1,\"LongpathWidth\":3.5}", 1.3, 3.5)]
    [InlineData("{\"ShortpathWidth\":8,\"LongpathWidth\":0.5}", "{\"ShortpathWidth\":1.75,\"LongpathWidth\":2.25}", 8.0, 0.5)]
    [InlineData("{\"ShortpathWidth\":\"2.5\",\"LongpathWidth\":1e9999}", "{\"ShortpathWidth\":\"3.5\",\"LongpathWidth\":false}", 1.3, 1.0)]
    public void PrecedenceIsResolvedPerFieldAndSavedOnce(string general, string legacy, double shortpath, double longpath)
    {
        File.WriteAllText(GeneralPath, general);
        File.WriteAllText(DxccPath, legacy);
        var expected = new MapSettings(shortpath, longpath);

        var loaded = Store.Load();

        Assert.Null(loaded.Error);
        Assert.Equal(expected, loaded.Settings);
        Assert.Equal(expected, ReadGeneral());
        var generalTime = SetOldTimestamp(GeneralPath);
        var dxccTime = SetOldTimestamp(DxccPath);
        Assert.Equal(expected, Store.Load().Settings);
        Assert.Equal(generalTime, File.GetLastWriteTimeUtc(GeneralPath));
        Assert.Equal(dxccTime, File.GetLastWriteTimeUtc(DxccPath));
    }

    [Fact]
    public void CompleteGeneralValuesTakePrecedenceWithoutRewritingGeneralFile()
    {
        var general = "{ \"ShortpathWidth\": 4.5, \"LongpathWidth\": 5.5, \"future\": null }";
        File.WriteAllText(GeneralPath, general);
        File.WriteAllText(DxccPath, """{"ShortpathWidth":2.5,"LongpathWidth":3.5,"unknown":true}""");
        var timestamp = SetOldTimestamp(GeneralPath);

        Assert.Equal(new MapSettings(4.5, 5.5), Store.Load().Settings);
        Assert.Equal(general, File.ReadAllText(GeneralPath));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(GeneralPath));
        Assert.Equal("true", AtomicJsonFile.ReadObject(DxccPath)!["unknown"]!.ToJsonString());
        Assert.False(AtomicJsonFile.ReadObject(DxccPath)!.ContainsKey("ShortpathWidth"));
    }

    [Fact]
    public void PartiallyValidLegacyWidthsMigrateWithIndependentDefaults()
    {
        File.WriteAllText(DxccPath, """{"ShortpathWidth":"invalid","LongpathWidth":2.25,"future":{"keep":true}}""");

        var result = Store.Load();

        Assert.Null(result.Error);
        Assert.Equal(new MapSettings(1.3, 2.25), result.Settings);
        Assert.Equal(result.Settings, ReadGeneral());
        Assert.Equal("{\"future\":{\"keep\":true}}", AtomicJsonFile.ReadObject(DxccPath)!.ToJsonString());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.49")]
    [InlineData("8.01")]
    [InlineData("1000000")]
    [InlineData("1e9999")]
    [InlineData("\"wide\"")]
    [InlineData("\"2.5\"")]
    [InlineData("\"NaN\"")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[1,2]")]
    public void InvalidLegacyFieldDefaultsIndependentlyAndPreservesDxcc(string invalid)
    {
        var geometry = Path.Combine(_directory, "external.geojson");
        foreach (var invalidShortpath in new[] { true, false })
        {
            File.Delete(GeneralPath);
            File.WriteAllText(DxccPath, $$"""
                {"GeoJsonPath":{{JsonSerializer.Serialize(geometry)}},"EnabledByDefault":true,"Unknown":42,
                 "ShortpathWidth":{{(invalidShortpath ? invalid : "2.5")}},
                 "LongpathWidth":{{(invalidShortpath ? "3.5" : invalid)}}}
                """);
            var result = Store.Load();

            Assert.Null(result.Error);
            Assert.Equal(new MapSettings(invalidShortpath ? 1.3 : 2.5, invalidShortpath ? 3.5 : 1), result.Settings);
            Assert.Equal(result.Settings, ReadGeneral());
            var dxcc = new DxccMapConfigurationStore(DxccPath).Load();
            Assert.Equal(geometry, dxcc.Configuration.GeoJsonPath);
            Assert.True(dxcc.Configuration.EnabledByDefault);
            Assert.Equal(42, AtomicJsonFile.ReadObject(DxccPath)!["Unknown"]!.GetValue<int>());
        }
    }

    [Fact]
    public void DxccSaveRetainsUnknownFieldsAndNeverSerializesGeneralWidths()
    {
        File.WriteAllText(DxccPath, """{"ShortpathWidth":2.5,"LongpathWidth":3.5,"Extra":{"borderWidth":0.7},"NullOption":null}""");
        Store.Load();
        var path = Path.Combine(_directory, "new.geojson");
        var dxccStore = new DxccMapConfigurationStore(DxccPath);
        dxccStore.Save(new DxccMapConfiguration(path, true));
        var document = AtomicJsonFile.ReadObject(DxccPath)!;

        Assert.Equal(0.7, document["Extra"]!["borderWidth"]!.GetValue<double>());
        Assert.True(document.ContainsKey("NullOption"));
        Assert.Equal(path, document["GeoJsonPath"]!.GetValue<string>());
        Assert.True(document["EnabledByDefault"]!.GetValue<bool>());
        Assert.False(document.ContainsKey("ShortpathWidth"));
        Assert.False(document.ContainsKey("LongpathWidth"));
        Assert.Equal(2, JsonSerializer.SerializeToElement(new DxccMapConfiguration()).EnumerateObject().Count());
        Assert.Equal(new MapSettings(2.5, 3.5), ReadGeneral());

        var bytes = File.ReadAllBytes(DxccPath);
        var timestamp = SetOldTimestamp(DxccPath);
        dxccStore.Save(dxccStore.Load().Configuration);
        Assert.Equal(bytes, File.ReadAllBytes(DxccPath));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(DxccPath));
    }

    [Fact]
    public void GeneralSaveRetainsUnknownFieldsAndNoOpSaveDoesNotRewrite()
    {
        File.WriteAllText(GeneralPath, """{"ShortpathWidth":2.5,"LongpathWidth":3.5,"Future":{"labels":["a",null]}}""");
        Store.Save(new MapSettings(4.5, 5.5));
        Assert.Equal(new MapSettings(4.5, 5.5), ReadGeneral());
        Assert.Equal("{\"labels\":[\"a\",null]}", AtomicJsonFile.ReadObject(GeneralPath)!["Future"]!.ToJsonString());
        var bytes = File.ReadAllBytes(GeneralPath);
        var timestamp = SetOldTimestamp(GeneralPath);

        new MapSettingsService(Store).Save(new MapSettings(4.5, 5.5));

        Assert.Equal(bytes, File.ReadAllBytes(GeneralPath));
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(GeneralPath));
        Assert.False(File.Exists(DxccPath));
    }

    [Fact]
    public void InterruptedGeneralMigrationRetainsBothPreviousFilesAndRetriesSafely()
    {
        var legacy = """{"ShortpathWidth":2.5,"LongpathWidth":3.5,"unknown":42}""";
        File.WriteAllText(DxccPath, legacy);
        File.WriteAllText(GeneralPath, """{"ShortpathWidth":4.5,"Future":true}""");
        var original = File.ReadAllBytes(GeneralPath);
        var store = new MapSettingsStore(GeneralPath, DxccPath, (path, document) =>
            AtomicJsonFile.Write(path, document, () => throw new IOException("simulated interruption")));

        var failed = store.Load();

        Assert.NotNull(failed.Error);
        Assert.Equal(new MapSettings(4.5, 3.5), failed.Settings);
        Assert.Equal(original, File.ReadAllBytes(GeneralPath));
        Assert.Equal(legacy, File.ReadAllText(DxccPath));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        Assert.Null(Store.Load().Error);
        Assert.Equal(failed.Settings, ReadGeneral());
    }

    [Fact]
    public void InterruptedFirstMigrationDoesNotRemoveLegacyOrLeaveTruncatedTarget()
    {
        var legacy = """{"ShortpathWidth":2.5,"LongpathWidth":3.5}""";
        File.WriteAllText(DxccPath, legacy);
        var store = new MapSettingsStore(GeneralPath, DxccPath, (path, document) =>
            AtomicJsonFile.Write(path, document, () => throw new IOException("simulated interruption")));

        Assert.NotNull(store.Load().Error);
        Assert.False(File.Exists(GeneralPath));
        Assert.Equal(legacy, File.ReadAllText(DxccPath));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        Assert.Null(Store.Load().Error);
        Assert.Equal(new MapSettings(2.5, 3.5), ReadGeneral());
    }

    [Fact]
    public void InterruptedDxccCleanupKeepsCommittedGeneralValuesAndRetriesOnlyCleanup()
    {
        var legacy = """{"ShortpathWidth":2.5,"LongpathWidth":3.5,"unknown":42}""";
        File.WriteAllText(DxccPath, legacy);
        var store = new MapSettingsStore(GeneralPath, DxccPath, (path, document) =>
        {
            if (path == DxccPath)
                AtomicJsonFile.Write(path, document, () => throw new IOException("simulated cleanup interruption"));
            else
                AtomicJsonFile.WriteIfChanged(path, document);
        });

        Assert.NotNull(store.Load().Error);
        Assert.Equal(new MapSettings(2.5, 3.5), ReadGeneral());
        Assert.Equal(legacy, File.ReadAllText(DxccPath));
        var timestamp = SetOldTimestamp(GeneralPath);

        Assert.Null(Store.Load().Error);
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(GeneralPath));
        Assert.Equal("{\"unknown\":42}", AtomicJsonFile.ReadObject(DxccPath)!.ToJsonString());
    }

    [Fact]
    public void StaleTemporaryFilesAreNeverLoadedOrPromoted()
    {
        var orphan = Path.Combine(_directory, ".map-settings.json.interrupted.tmp");
        File.WriteAllText(orphan, "{truncated");
        File.WriteAllText(DxccPath, """{"ShortpathWidth":2.5,"LongpathWidth":3.5}""");

        Assert.Null(Store.Load().Error);
        Assert.Equal(new MapSettings(2.5, 3.5), ReadGeneral());
        Assert.Equal("{truncated", File.ReadAllText(orphan));
        var timestamp = SetOldTimestamp(GeneralPath);
        Assert.Null(Store.Load().Error);
        Assert.Equal(timestamp, File.GetLastWriteTimeUtc(GeneralPath));
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("null")]
    [InlineData("[]")]
    public void UnparseableGeneralSettingsAreReportedWithoutDestroyingEitherFile(string malformed)
    {
        File.WriteAllText(GeneralPath, malformed);
        var legacy = """{"ShortpathWidth":2.5,"LongpathWidth":3.5,"unknown":42}""";
        File.WriteAllText(DxccPath, legacy);

        var result = Store.Load();

        Assert.NotNull(result.Error);
        Assert.Equal(new MapSettings(2.5, 3.5), result.Settings);
        Assert.Equal(malformed, File.ReadAllText(GeneralPath));
        Assert.Equal(legacy, File.ReadAllText(DxccPath));
        Assert.ThrowsAny<JsonException>(() => Store.Save(new MapSettings()));
        Assert.Equal(malformed, File.ReadAllText(GeneralPath));
    }

    [Fact]
    public void MalformedDxccIsNeverOverwrittenAndDoesNotDiscardValidGeneralSettings()
    {
        var source = "{truncated dxcc";
        File.WriteAllText(DxccPath, source);
        Store.Save(new MapSettings(1.75, 2.25));

        var result = Store.Load();

        Assert.NotNull(result.Error);
        Assert.Equal(new MapSettings(1.75, 2.25), result.Settings);
        Assert.Equal(source, File.ReadAllText(DxccPath));
        Assert.ThrowsAny<JsonException>(() => new DxccMapConfigurationStore(DxccPath).Save(new DxccMapConfiguration()));
        Assert.Equal(source, File.ReadAllText(DxccPath));
    }

    [Fact]
    public void FailedGeneralSaveDoesNotPublishOrChangeCurrentSettings()
    {
        Store.Save(new MapSettings(2.5, 3.5));
        var store = new MapSettingsStore(GeneralPath, DxccPath, (path, document) =>
            AtomicJsonFile.Write(path, document, () => throw new IOException("simulated interruption")));
        var service = new MapSettingsService(store);
        var changes = new List<MapSettings>();
        service.Changed += changes.Add;

        Assert.Throws<IOException>(() => service.Save(new MapSettings(4.5, 5.5)));
        Assert.Equal(new MapSettings(2.5, 3.5), service.Current);
        Assert.Equal(service.Current, ReadGeneral());
        Assert.Empty(changes);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    private MapSettings ReadGeneral()
    {
        var document = AtomicJsonFile.ReadObject(GeneralPath)!;
        return new(document["ShortpathWidth"]!.GetValue<double>(), document["LongpathWidth"]!.GetValue<double>());
    }

    private static DateTime SetOldTimestamp(string path)
    {
        File.SetLastWriteTimeUtc(path, new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc));
        return File.GetLastWriteTimeUtc(path);
    }
}
