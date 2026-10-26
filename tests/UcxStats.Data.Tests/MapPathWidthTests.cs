using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using UcxStats.Data.Database;
using UcxStats.Data.Dxcc;
using UcxStats.Map;

namespace UcxStats.Data.Tests;

public sealed class MapPathWidthTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"EnabledByDefault\":true}")]
    public void MissingAndLegacyConfigurationPreserveOriginalWidths(string? json)
    {
        WithStore((store, path) =>
        {
            if (json is not null)
                File.WriteAllText(store.LegacyDxccPath, json);

            var loaded = store.Load();

            Assert.Null(loaded.Error);
            Assert.Equal(1.3, loaded.Settings.ShortpathWidth);
            Assert.Equal(1.0, loaded.Settings.LongpathWidth);
            Assert.Equal(MapPathWidths.Default, loaded.Settings.PathWidths);
            Assert.False(File.Exists(path));
        });
    }

    [Theory]
    [InlineData(0.5, 8.0)]
    [InlineData(1.3, 1.0)]
    [InlineData(2.5, 3.5)]
    [InlineData(1.75, 2.25)]
    public void WidthsRoundTripWithoutRoundingOrChangingDxcc(double shortpath, double longpath)
    {
        WithStore((store, path) =>
        {
            var geometryPath = Path.Combine(Path.GetDirectoryName(path)!, "external.geojson");
            var dxccStore = new DxccMapConfigurationStore(store.LegacyDxccPath);
            dxccStore.Save(new DxccMapConfiguration(geometryPath, true));
            var dxccBytes = File.ReadAllBytes(store.LegacyDxccPath);
            store.Save(new MapSettings(shortpath, longpath));

            var loaded = store.Load();

            Assert.Null(loaded.Error);
            Assert.Equal(new MapPathWidths(shortpath, longpath), loaded.Settings.PathWidths);
            Assert.Equal(geometryPath, dxccStore.Load().Configuration.GeoJsonPath);
            Assert.True(dxccStore.Load().Configuration.EnabledByDefault);
            Assert.Equal(dxccBytes, File.ReadAllBytes(store.LegacyDxccPath));
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(shortpath, json.RootElement.GetProperty("ShortpathWidth").GetDouble());
            Assert.Equal(longpath, json.RootElement.GetProperty("LongpathWidth").GetDouble());
            Assert.False(json.RootElement.TryGetProperty("GeoJsonPath", out _));
            Assert.False(json.RootElement.TryGetProperty("EnabledByDefault", out _));
            Assert.False(json.RootElement.GetProperty("StatisticsVisible").GetBoolean());
        });
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
    public void InvalidFieldFallsBackIndependentlyAndPreservesDxcc(string invalid)
    {
        WithStore((store, path) =>
        {
            var geometryPath = Path.Combine(Path.GetDirectoryName(path)!, "external.geojson");
            var geometryJson = JsonSerializer.Serialize(geometryPath);
            var dxccJson = $$"""{"GeoJsonPath":{{geometryJson}},"EnabledByDefault":true}""";
            File.WriteAllText(store.LegacyDxccPath, dxccJson);
            foreach (var invalidShortpath in new[] { true, false })
            {
                File.WriteAllText(path, $$"""
                    {"ShortpathWidth":{{(invalidShortpath ? invalid : "2.5")}},
                     "LongpathWidth":{{(invalidShortpath ? "3.5" : invalid)}}}
                    """);
                var loaded = store.Load();

                Assert.Null(loaded.Error);
                Assert.Equal(invalidShortpath ? 1.3 : 2.5, loaded.Settings.ShortpathWidth);
                Assert.Equal(invalidShortpath ? 3.5 : 1.0, loaded.Settings.LongpathWidth);
                var dxcc = new DxccMapConfigurationStore(store.LegacyDxccPath).Load();
                Assert.Equal(geometryPath, dxcc.Configuration.GeoJsonPath);
                Assert.True(dxcc.Configuration.EnabledByDefault);
                Assert.Equal(dxccJson, File.ReadAllText(store.LegacyDxccPath));
            }
        });
    }

    [Fact]
    public void MalformedConfigurationAndInvalidDxccPathRemainSafe()
    {
        WithStore((store, path) =>
        {
            File.WriteAllText(path, "not JSON");
            Assert.NotNull(store.Load().Error);
            Assert.Equal(MapPathWidths.Default, store.Load().Settings.PathWidths);

            File.Delete(path);
            File.WriteAllText(store.LegacyDxccPath, """{"GeoJsonPath":"relative.geojson","ShortpathWidth":2.5,"LongpathWidth":3.5}""");
            var loaded = store.Load();
            Assert.Null(loaded.Error);
            var dxcc = new DxccMapConfigurationStore(store.LegacyDxccPath).Load();
            Assert.NotNull(dxcc.Error);
            Assert.Null(dxcc.Configuration.GeoJsonPath);
            Assert.False(dxcc.Configuration.EnabledByDefault);
            Assert.Equal(new MapPathWidths(2.5, 3.5), loaded.Settings.PathWidths);
        });
    }

    [Fact]
    public void SavingNonFiniteOrOutOfRangeValuesWritesSafeDefaults()
    {
        WithStore((store, _) =>
        {
            foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1, 0, 1e20 })
            {
                store.Save(new MapSettings(ShortpathWidth: invalid, LongpathWidth: 2.5));
                Assert.Equal(new MapPathWidths(1.3, 2.5), store.Load().Settings.PathWidths);
                store.Save(new MapSettings(ShortpathWidth: 3.5, LongpathWidth: invalid));
                Assert.Equal(new MapPathWidths(3.5, 1), store.Load().Settings.PathWidths);
            }
        });
    }

    [Fact]
    public void RuntimeScriptUsesInvariantFractionalNumbersAndOnlyTheStyleHook()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal(
                "window.ucxSetPathWidths && window.ucxSetPathWidths({\"shortpathWidth\":2.5,\"longpathWidth\":3.5});",
                MapPageBuilder.BuildPathWidthsScript(new MapPathWidths(2.5, 3.5)));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void SettingsBroadcastReachesAllOpenSubscribersAndSupportsCleanup()
    {
        WithStore((store, _) =>
        {
            var service = new MapSettingsService(store);
            var first = new List<MapSettings>();
            var second = new List<MapSettings>();
            service.Changed += first.Add;
            service.Changed += second.Add;
            service.Save(new MapSettings(2.5, 3.5));
            Assert.Equal(new MapSettings(2.5, 3.5), Assert.Single(first));
            Assert.Equal(first, second);
            service.Changed -= second.Add;
            service.Save(new MapSettings());
            Assert.Equal(new MapSettings(), first[1]);
            Assert.Single(second);
            service.Save(new MapSettings());
            Assert.Equal(2, first.Count);
            Assert.Equal(new MapSettings(), new MapSettingsService(store).Current);
        });
    }

    [Theory]
    [InlineData("defaults")]
    [InlineData("configured")]
    [InlineData("shortpath")]
    [InlineData("longpath")]
    [InlineData("live")]
    [InlineData("invalid")]
    public async Task GeneratedJavascriptUpdatesResidentPathsAndPreservesMapState(string scenario)
    {
        // Execute the complete generated page script with Leaflet/DOM test doubles.
        // This exercises the actual creation/update/Live Map functions without Windows.
        var points = new[]
        {
            new MapQsoPoint(1, "/synthetic.ucx", "TEST", "2026-01-01", "12:00:00", 4, 0,
                "JN58", 48.5, 11, "Home", 48, 11.5, 50, 90),
            new MapQsoPoint(2, "/synthetic.ucx", "TEST2", "2026-01-01", "12:01:00", 2, 0,
                "JN58", 40, 170, "Home", 48, 11.5, 500, 90)
        };
        var dxcc = new MapDxccPageOptions(true, true, "https://ucxstats.local/dxcc.geojson",
            DxccMapPayloadFactory.Create(new DxccStatusSnapshot([], 0)), "synthetic geometry");
        var html = MapPageBuilder.Build(points,
            new MapTileDefinition("Test", "https://tiles.example/{z}/{x}/{y}.png", "Test", 18), dxcc,
            scenario == "configured" ? new MapPathWidths(1.75, 2.25) : null);
        var startInfo = new ProcessStartInfo("node")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Fixtures", "map-path-widths.cjs"));
        startInfo.ArgumentList.Add(scenario);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Node.js 18+ is required for map JavaScript regression tests.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new
        {
            html,
            updateScript = MapPageBuilder.BuildPathWidthsScript(new MapPathWidths(2.5, 3.5))
        }));
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }
        Assert.True(process.ExitCode == 0, await outputTask + await errorTask);
    }

    private static void WithStore(Action<MapSettingsStore, string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ucxstats-path-widths-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "map-settings.json");
            test(new MapSettingsStore(path), path);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
