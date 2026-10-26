using System.Text.Json;
using UcxStats.Map;

namespace UcxStats.Data.Tests;

public sealed class LiveThresholdUiTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ucxstats-live-threshold-{Guid.NewGuid():N}");
    private MapSettingsStore Store => new(Path.Combine(_directory, "map-settings.json"));
    public LiveThresholdUiTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void SliderContractHasFixedLimitsAndRetainsDefaults()
    {
        using var json = JsonDocument.Parse(LiveThresholdUi.SerializeLimits());
        var limits = json.RootElement;
        Assert.Equal(1, limits.GetProperty("warningMinimum").GetInt32());
        Assert.Equal(179, limits.GetProperty("warningMaximum").GetInt32());
        Assert.Equal(2, limits.GetProperty("prolongedMinimum").GetInt32());
        Assert.Equal(180, limits.GetProperty("prolongedMaximum").GetInt32());
        var settings = Store.Load().Settings;
        Assert.Equal(10, settings.LiveWarningThresholdMinutes);
        Assert.Equal(15, settings.LiveProlongedGapThresholdMinutes);
        Assert.True(LiveThresholdUi.IsValidPair(settings.LiveWarningThresholdMinutes, settings.LiveProlongedGapThresholdMinutes));
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(180, 181)]
    [InlineData(1, 1)]
    [InlineData(10, 181)]
    [InlineData(15, 15)]
    [InlineData(16, 15)]
    [InlineData(1439, 1440)]
    [InlineData(int.MinValue, int.MaxValue)]
    public void UiMessageRejectsInvalidPairWithoutSaving(int warning, int prolonged)
    {
        var store = Store;
        store.Save(new MapSettings(ShortpathWidth: 2.5, LongpathWidth: 3.5));
        var original = File.ReadAllBytes(store.Path);
        Assert.False(LiveThresholdUi.IsValidPair(warning, prolonged));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(LiveThresholdUi.Apply(store.Load().Settings, warning, prolonged)));
        Assert.Equal(original, File.ReadAllBytes(store.Path));
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(10, 15)]
    [InlineData(179, 180)]
    public void UiEditPersistsPairAndPreservesUnrelatedSettings(int warning, int prolonged)
    {
        var store = Store;
        File.WriteAllText(store.Path, """{"Future":{"keep":true}}""");
        var original = new MapSettings(ShortpathWidth: 2.5, LongpathWidth: 3.5,
            LiveWarningThresholdMinutes: 1439, LiveProlongedGapThresholdMinutes: 1440)
        {
            LiveTimelineMinutes = 123, LiveTimelineBucketMinutes = 2, LiveTimelineHeightPx = 39,
            LiveTimelineSettingsCollapsed = true, TimelinePanelPosition = new(.2, .3)
        };
        store.Save(original);
        var service = new MapSettingsService(Store);
        var expected = LiveThresholdUi.Apply(service.Current, warning, prolonged);
        service.Save(expected);
        Assert.Equal(expected, new MapSettingsService(Store).Current);
        Assert.Equal(original with { LiveWarningThresholdMinutes = warning, LiveProlongedGapThresholdMinutes = prolonged }, expected);
        Assert.True(AtomicJsonFile.ReadObject(store.Path)!["Future"]!["keep"]!.GetValue<bool>());
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void WidePersistedConfigurationRemainsSupportedWithoutAutomaticRewrite()
    {
        var expected = new MapSettings(LiveWarningThresholdMinutes: 1439, LiveProlongedGapThresholdMinutes: 1440);
        Store.Save(expected);
        var bytes = File.ReadAllBytes(Store.Path);
        var restored = new MapSettingsService(Store);
        Assert.Equal(expected, restored.Current);
        Assert.Equal(bytes, File.ReadAllBytes(Store.Path));
        Assert.Equal(expected, expected.Normalize());
        Assert.False(LiveThresholdUi.IsValidPair(1439, 1440));
    }
}
