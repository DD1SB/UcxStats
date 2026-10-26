using System.Text.Json;
using UcxStats.Map;

namespace UcxStats.Data.Tests;

public sealed class TimelineHeightSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ucxstats-timeline-height-{Guid.NewGuid():N}");
    private MapSettingsStore Store => new(Path.Combine(_directory, "map-settings.json"));

    public TimelineHeightSettingsTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void MissingHeightRetainsResponsiveDefaultWithoutCreatingSettings()
    {
        Assert.Null(Store.Load().Settings.LiveTimelineHeightPx);
        Assert.False(File.Exists(Store.Path));
        File.WriteAllText(Store.Path, """{"ShortpathWidth":2.5,"LongpathWidth":3.5}""");
        Assert.Null(Store.Load().Settings.LiveTimelineHeightPx);
        Assert.Equal(new MapPathWidths(2.5, 3.5), Store.Load().Settings.PathWidths);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(39)]
    [InlineData(40)]
    [InlineData(79)]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(80)]
    [InlineData(148)]
    [InlineData(174)]
    [InlineData(260)]
    [InlineData(320)]
    public void HeightSurvivesPersistenceRestartAndBrowserContract(int height)
    {
        File.WriteAllText(Store.Path, """{"ShortpathWidth":2.5,"LongpathWidth":3.5,"Keep":{"value":42}}""");
        File.WriteAllText(Store.LegacyDxccPath, """{"EnabledByDefault":true}""");
        var dxcc = File.ReadAllBytes(Store.LegacyDxccPath);
        var original = Store.Load().Settings with { LiveTimelineHeightPx = height, LiveTimelineMinutes = 137, LiveTimelineBucketMinutes = 2,
            StatisticsPanelPosition = new(.3, .7) };
        var service = new MapSettingsService(Store);
        service.Save(original);
        var restored = new MapSettingsService(Store).Current;
        Assert.Equal(original, restored);
        Assert.Equal(restored, JsonSerializer.Deserialize<MapSettings>(MapStatisticsPayload.SerializeSettings(restored),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }));
        Assert.Equal(height, AtomicJsonFile.ReadObject(Store.Path)!["LiveTimelineHeightPx"]!.GetValue<int>());
        Assert.Equal(42, AtomicJsonFile.ReadObject(Store.Path)!["Keep"]!["value"]!.GetValue<int>());
        Assert.Equal(dxcc, File.ReadAllBytes(Store.LegacyDxccPath));
        // Opening or resizing another map keeps this window's preferred height.
        Assert.Equal(height, new MapSettings { LiveTimelineHeightPx = 100 }.WithPresentationFrom(restored).LiveTimelineHeightPx);
        Assert.Null(new MapSettings { LiveTimelineHeightPx = height }.WithPresentationFrom(new MapSettings()).LiveTimelineHeightPx);
    }

    [Theory]
    [InlineData(-1, 20)]
    [InlineData(19, 20)]
    [InlineData(321, 320)]
    [InlineData(int.MaxValue, 320)]
    public void OutOfRangePreferencesClampToSupportedLimits(int value, int expected)
    {
        Store.Save(new MapSettings { LiveTimelineHeightPx = value });
        Assert.Equal(expected, Store.Load().Settings.LiveTimelineHeightPx);
    }

    [Fact]
    public void ExistingHeightAndVisibilitySettingsMigrateWithoutNewStatesOrRewrites()
    {
        foreach (var height in new[] { 80, 148, 174, 260, 320 })
        {
            var original = "{\"ShortpathWidth\":2.5,\"LongpathWidth\":3.5,\"LiveTimelineVisible\":false,\"LiveTimelineHeightPx\":" + height + "}";
            File.WriteAllText(Store.Path, original);
            var settings = Store.Load().Settings;
            Assert.Equal(height, settings.LiveTimelineHeightPx);
            Assert.False(settings.LiveTimelineVisible);
            Assert.Equal(original, File.ReadAllText(Store.Path));
            Store.Save(settings with { LiveTimelineHeightPx = 20 });
            var reloaded = Store.Load().Settings;
            Assert.Equal(20, reloaded.LiveTimelineHeightPx);
            Assert.False(reloaded.LiveTimelineVisible);
            using var json = JsonDocument.Parse(File.ReadAllText(Store.Path));
            Assert.Single(json.RootElement.EnumerateObject(), x => x.Name == nameof(MapSettings.LiveTimelineVisible));
            Assert.DoesNotContain(json.RootElement.EnumerateObject(), x => x.Name.Contains("Mode", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"200\"")]
    [InlineData("1.5")]
    [InlineData("true")]
    [InlineData("{}")]
    public void InvalidHeightFallsBackToResponsiveDefault(string value)
    {
        File.WriteAllText(Store.Path, "{\"ShortpathWidth\":2.5,\"LongpathWidth\":3.5,\"LiveTimelineHeightPx\":" + value + "}");
        Assert.Null(Store.Load().Settings.LiveTimelineHeightPx);
    }
}
