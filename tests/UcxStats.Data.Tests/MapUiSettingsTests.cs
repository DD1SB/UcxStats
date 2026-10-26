using System.Text.Json;
using UcxStats.Map;

namespace UcxStats.Data.Tests;

public sealed class MapUiSettingsTests : IDisposable
{
    private readonly string _directory=Path.Combine(Path.GetTempPath(),$"ucxstats-ui-settings-{Guid.NewGuid():N}");
    private MapSettingsStore Store=>new(Path.Combine(_directory,"map-settings.json"));
    public MapUiSettingsTests()=>Directory.CreateDirectory(_directory);
    public void Dispose()=>Directory.Delete(_directory,true);

    [Fact]
    public void FreshDefaultsAndPresentationRoundTripPreserveWidthsAndDxcc()
    {
        Assert.Equal(new MapSettings(),Store.Load().Settings);
        Assert.False(File.Exists(Store.Path));
        File.WriteAllText(Store.LegacyDxccPath,"""{"EnabledByDefault":false,"Keep":"dxcc"}""");
        var bytes=File.ReadAllBytes(Store.LegacyDxccPath);
        var expected=new MapSettings(2.5,3.5)
        {
            LiveTimelineMinutes=137,LiveTimelineSnap=false,LiveTimelineBucketMinutes=2,
            LiveTimelineVisible=false,LiveTimelineSettingsVisible=false,LiveTimelineSettingsCollapsed=false,
            ContentsPanelCollapsed=true,StatisticsPanelCollapsed=true,
            ContentsPanelPosition=new(.3,.8),StatisticsPanelPosition=new(1,0),TimelinePanelPosition=new(.4,.6)
        };
        Store.Save(expected);
        Assert.Equal(expected,Store.Load().Settings);
        var browserRoundTrip=JsonSerializer.Deserialize<MapSettings>(MapStatisticsPayload.SerializeSettings(expected),
            new JsonSerializerOptions {PropertyNameCaseInsensitive=true});
        Assert.Equal(expected,browserRoundTrip);
        Assert.Equal(bytes,File.ReadAllBytes(Store.LegacyDxccPath));
        Assert.Empty(Directory.GetFiles(_directory,"*.tmp"));
        var currentBytes=File.ReadAllBytes(Store.Path);
        Store.Save(expected);
        Assert.Equal(currentBytes,File.ReadAllBytes(Store.Path));
        using var json=JsonDocument.Parse(File.ReadAllText(Store.Path));
        Assert.Equal(.3,json.RootElement.GetProperty("ContentsPanelPosition").GetProperty("X").GetDouble());
    }

    [Theory]
    [InlineData("null")][InlineData("\"60\"")][InlineData("1.5")][InlineData("-1")]
    [InlineData("0")][InlineData("1000000")][InlineData("[]")][InlineData("{}")]
    public void InvalidTimelineFieldsAndPositionsFallBack(string invalid)
    {
        File.WriteAllText(Store.Path,$$$"""
        {"ShortpathWidth":2.5,"LongpathWidth":3.5,"LiveTimelineMinutes":{{{invalid}}},
        "LiveTimelineSnap":{{{invalid}}},"LiveTimelineVisible":{{{invalid}}},"LiveTimelineBucketMinutes":{{{invalid}}},
        "LiveTimelineSettingsVisible":{{{invalid}}},"LiveTimelineSettingsCollapsed":{{{invalid}}},
        "ContentsPanelPosition":{{{invalid}}},"StatisticsPanelPosition":{"X":"0.5","Y":0.5}}
        """);
        Assert.Equal(new MapSettings(2.5,3.5),Store.Load().Settings);
    }

    [Theory]
    [InlineData(15,1)][InlineData(60,2)][InlineData(180,5)][InlineData(240,10)]
    public void RangeLimitsAndIntegerPrecisionPersist(int minutes,int bucket)
    {
        var settings=new MapSettings {LiveTimelineMinutes=minutes,LiveTimelineBucketMinutes=bucket};
        Store.Save(settings);Assert.Equal(settings,Store.Load().Settings);
    }

    [Fact]
    public void PanelCoordinatesClampAndNonFiniteValuesRestoreDefaults()
    {
        var settings=new MapSettings {ContentsPanelPosition=new(-3,7),StatisticsPanelPosition=new(double.NaN,.5),TimelinePanelPosition=new(.5,double.PositiveInfinity)}.Normalize();
        Assert.Equal(new(0,1),settings.ContentsPanelPosition);Assert.Null(settings.StatisticsPanelPosition);Assert.Null(settings.TimelinePanelPosition);
    }

    [Fact]
    public void IndependentWindowPresentationSurvivesGlobalCalculationChanges()
    {
        var first=new MapSettings {ContentsPanelPosition=new(.1,.2),LiveTimelineMinutes=30,LiveTimelineVisible=false};
        var second=new MapSettings {ContentsPanelPosition=new(.8,.9),LiveTimelineMinutes=180};
        var global=new MapSettings(ShortpathWidth:3,ContestPauseThresholdMinutes:31,LiveWarningThresholdMinutes:7,LiveProlongedGapThresholdMinutes:12);
        var firstApplied=global.WithPresentationFrom(first);
        var secondApplied=global.WithPresentationFrom(second);
        Assert.Equal(first.ContentsPanelPosition,firstApplied.ContentsPanelPosition);
        Assert.Equal(second.ContentsPanelPosition,secondApplied.ContentsPanelPosition);
        Assert.False(firstApplied.LiveTimelineVisible);Assert.True(secondApplied.LiveTimelineVisible);
        Assert.Equal(31,firstApplied.ContestPauseThresholdMinutes);Assert.Equal(3,secondApplied.ShortpathWidth);
        Assert.Equal(7,firstApplied.LiveWarningThresholdMinutes);
    }

    [Fact]
    public void InterruptedSaveRetainsPreviousPresentationAndUnknownProperties()
    {
        File.WriteAllText(Store.Path,"""{"ShortpathWidth":2.5,"LongpathWidth":3.5,"Unknown":{"value":42}}""");
        var original=new MapSettings(2.5,3.5){TimelinePanelPosition=new(.2,.3),LiveTimelineMinutes=120};
        Store.Save(original);var bytes=File.ReadAllBytes(Store.Path);
        var failing=new MapSettingsStore(Store.Path,null,(path,json)=>AtomicJsonFile.Write(path,json,()=>throw new IOException("interrupted")));
        Assert.Throws<IOException>(()=>failing.Save(original with {LiveTimelineMinutes=180}));
        Assert.Equal(bytes,File.ReadAllBytes(Store.Path));Assert.Equal(original,Store.Load().Settings);
        Assert.Equal(42,AtomicJsonFile.ReadObject(Store.Path)!["Unknown"]!["value"]!.GetValue<int>());
        Assert.Empty(Directory.GetFiles(_directory,"*.tmp"));
    }
}
