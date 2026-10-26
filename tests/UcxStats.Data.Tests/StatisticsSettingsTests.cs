using System.Text.Json.Nodes;
using UcxStats.Map;

namespace UcxStats.Data.Tests;

public sealed class StatisticsSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"ucxstats-stat-settings-{Guid.NewGuid():N}");
    private MapSettingsStore Store => new(Path.Combine(_directory, "map-settings.json"));
    public StatisticsSettingsTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public void DefaultsAreOptionalAndContestHidden()
    {
        var settings = Store.Load().Settings;
        Assert.False(settings.StatisticsVisible); Assert.False(settings.ContestTimeVisible);
        Assert.Equal(60, settings.ContestPauseThresholdMinutes);
        Assert.Null(settings.OperatingTimeAllowanceMinutes);
        Assert.Equal(10, settings.LiveWarningThresholdMinutes);
        Assert.Equal(15, settings.LiveProlongedGapThresholdMinutes);
        Assert.False(File.Exists(Store.Path));
    }

    [Fact]
    public void PersistenceRetainsPathWidthsUnknownFieldsAndDxccBytes()
    {
        var store = Store;
        File.WriteAllText(store.Path, """{"ShortpathWidth":1.75,"LongpathWidth":2.25,"Future":{"keep":true}}""");
        File.WriteAllText(store.LegacyDxccPath, """{"EnabledByDefault":true}""");
        var dxcc = File.ReadAllBytes(store.LegacyDxccPath);
        var service = new MapSettingsService(store);
        service.Save(service.Current with { StatisticsVisible = true, ContestTimeVisible = true,
            ContestPauseThresholdMinutes = 31, OperatingTimeAllowanceMinutes = 2160,
            LiveWarningThresholdMinutes = 7, LiveProlongedGapThresholdMinutes = 12 });
        var restored = new MapSettingsService(Store).Current;
        Assert.Equal(service.Current, restored);
        Assert.Equal(new MapPathWidths(1.75, 2.25), restored.PathWidths);
        Assert.Equal(dxcc, File.ReadAllBytes(store.LegacyDxccPath));
        Assert.True(AtomicJsonFile.ReadObject(store.Path)!["Future"]!["keep"]!.GetValue<bool>());
        // The existing widths editor must retain the statistics settings when saving.
        Store.Save(restored with { ShortpathWidth = 3.5 });
        Assert.True(Store.Load().Settings.ContestTimeVisible);
        Assert.Equal(31, Store.Load().Settings.ContestPauseThresholdMinutes);
        Assert.Equal(2160, Store.Load().Settings.OperatingTimeAllowanceMinutes);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"60\"")]
    [InlineData("true")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("10000000")]
    [InlineData("5.5")]
    public void InvalidValuesFallBackIndependently(string invalid)
    {
        var store = Store;
        File.WriteAllText(store.Path, $$"""
            {"ShortpathWidth":2.5,"LongpathWidth":3.5,"StatisticsVisible":{{invalid}},
            "ContestTimeVisible":{{invalid}},"ContestPauseThresholdMinutes":{{invalid}},
            "OperatingTimeAllowanceMinutes":{{invalid}},"LiveWarningThresholdMinutes":{{invalid}},
            "LiveProlongedGapThresholdMinutes":{{invalid}}}
            """);
        Assert.Equal(new MapSettings(2.5, 3.5, StatisticsVisible: invalid == "true",
            ContestTimeVisible: invalid == "true"), store.Load().Settings);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(15, 10)]
    [InlineData(1439, 2)]
    public void InvalidThresholdOrderRestoresSafePair(int warning, int prolonged)
    {
        var settings = new MapSettings(LiveWarningThresholdMinutes: warning,
            LiveProlongedGapThresholdMinutes: prolonged).Normalize();
        Assert.Equal(10, settings.LiveWarningThresholdMinutes);
        Assert.Equal(15, settings.LiveProlongedGapThresholdMinutes);
    }

    [Fact]
    public void ValidLimitsAndInterruptedAtomicSave()
    {
        var expected = new MapSettings(1.75, 2.25, true, true, 5, 525600, 1439, 1440);
        Store.Save(expected);
        Assert.Equal(expected, Store.Load().Settings);
        var bytes = File.ReadAllBytes(Store.Path);
        var failing = new MapSettingsStore(Store.Path, null, (path, document) =>
            AtomicJsonFile.Write(path, document, () => throw new IOException("interrupted")));
        Assert.Throws<IOException>(() => failing.Save(expected with { ContestPauseThresholdMinutes = 180 }));
        Assert.Equal(bytes, File.ReadAllBytes(Store.Path));
        Assert.Equal(expected, Store.Load().Settings);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }
}
