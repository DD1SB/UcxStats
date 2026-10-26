using System.Diagnostics;
using System.Text.Json;
using UcxStats.Data.Database;
using UcxStats.Data.Dxcc;
using UcxStats.Map;

namespace UcxStats.Data.Tests;

public sealed class MapStatisticsIntegrationTests
{
    private static QsoStatisticsSnapshot Snapshot()
    {
        var a = new QsoStatisticsAccumulator();
        a.Add(1, "FIRST", 2, "JO40AA", "2026-01-01", "12:00:00", 281, true, 100);
        a.Add(2, "ODX", 4, "JO40AB", "2026-01-01", "12:30:00", 281, true, 200);
        a.Add(3, "NOLOC", 4, "", "2026-01-01", "12:30:00", 281, false, null);
        a.Add(4, "INVALID", 4, "JO40ZZ", "2026-01-01", "13:30:01", null, false, null);
        a.Add(5, "MICROWAVE", 16, "FN31PR12", "2026-01-01", "14:00:00", 291, false, null);
        return a.Build();
    }

    [Fact]
    public void CompactSnapshotExcludesQsoRowsAndTimestampCollectionAndEscapesText()
    {
        var json = MapStatisticsPayload.Serialize(Snapshot(), new MapSettings());
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("timestampsUtc", out _));
        Assert.False(document.RootElement.TryGetProperty("latestQso", out _));
        Assert.DoesNotContain("NOLOC", json);
        Assert.Equal(3, document.RootElement.GetProperty("bands").GetArrayLength());
        Assert.Equal(1, document.RootElement.GetProperty("contest").GetProperty("pauseCount").GetInt32());
        var script = MapStatisticsPayload.BuildActivityScript(new(1, "</script>", 4, DateTimeOffset.UtcNow));
        Assert.DoesNotContain("</script>", script);
        Assert.DoesNotContain("Navigate", MapStatisticsPayload.BuildSnapshotScript(json));
    }

    [Fact]
    public void AnnualLogPauseDetailsAreBoundedWithoutChangingTotals()
    {
        var a = new QsoStatisticsAccumulator();
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 301; i++)
        {
            var time = start.AddMinutes(i * 61);
            a.Add(i, "TEST", 4, "", time.ToString("yyyy-MM-dd"), time.ToString("HH:mm:ss"), null, false, null);
        }
        using var json = JsonDocument.Parse(MapStatisticsPayload.Serialize(a.Build(), new MapSettings()));
        var contest = json.RootElement.GetProperty("contest");
        Assert.Equal(300, contest.GetProperty("pauseCount").GetInt32());
        Assert.Equal(100, contest.GetProperty("pauses").GetArrayLength());
        Assert.Equal(300 * 61 * 60, contest.GetProperty("pauseSeconds").GetDouble());
        Assert.Equal(0, contest.GetProperty("operatingSeconds").GetDouble());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GeneratedPanelSupportsExactUnionsVisibilityRuntimeUpdatesAndIndependentClock(bool isLive)
    {
        var points = new[]
        {
            new MapQsoPoint(1, "a.ucx", "FIRST", "2026-01-01", "12:00:00", 2, 0, "JO40AA", 50, 8, "Home", 48, 11, 100, 90),
            new MapQsoPoint(2, "a.ucx", "ODX", "2026-01-01", "12:30:00", 4, 0, "JO40AB", 50, 8, "Home", 48, 11, 200, 90)
        };
        var status = DxccMapPayloadFactory.Create(new DxccStatusSnapshot([new(281, 4, 2, 0, 0)], 0));
        var html = MapPageBuilder.Build(points, new("Test", "https://tiles.example/{z}/{x}/{y}.png", "Test", 18),
            new(true, true, "https://ucxstats.local/dxcc.geojson", status, "ready"), isLiveMap: isLive);
        var info = new ProcessStartInfo("node")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false
        };
        info.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Fixtures", "map-statistics.cjs"));
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new
        {
            html, isLive,
            updateScript = MapStatisticsPayload.BuildSnapshotScript(MapStatisticsPayload.Serialize(Snapshot(), new MapSettings())),
            activityScript = MapStatisticsPayload.BuildActivityScript(new(1, "FIRST", 2, new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)))
        }));
        process.StandardInput.Close();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        Assert.True(process.ExitCode == 0, await output + await error);
    }
}
