using System.Diagnostics;
using System.Text;
using UcxStats.Data.Database;
using UcxStats.Map;
using Xunit.Abstractions;

namespace UcxStats.Data.Tests;

public sealed class LiveTimelineDataTests(ITestOutputHelper output)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 0, 1, 30, TimeSpan.Zero);
    private static QsoStatisticsSnapshot Statistics(params DateTimeOffset[] times)
    {
        var a = new QsoStatisticsAccumulator();
        for (var i = 0; i < times.Length; i++)
        {
            var utc = times[i].ToUniversalTime();
            a.Add(i + 1, "TEST", 4, "", utc.ToString("yyyy-MM-dd"), utc.ToString("HH:mm:ss"), null, false, null);
        }
        return a.Build();
    }

    [Fact]
    public void EmptySnapshotHasBoundedCoverageAndNoSamples()
    {
        var data = LiveTimelinePayload.Create(Statistics(), Now);
        Assert.Empty(data.Samples); Assert.Null(data.LatestQso);
        Assert.Equal(250 * 60, data.GeneratedAtUnixSeconds - data.CoverageStartUnixSeconds);
        Assert.Equal(240 * 60, data.CoverageEndUnixSeconds - data.GeneratedAtUnixSeconds);
    }

    [Fact]
    public void MultiplicityAndSecondPrecisionSurviveWithoutChangingContestInstants()
    {
        var statistics = Statistics(Now, Now.AddSeconds(-1), Now, Now.AddSeconds(-60));
        Assert.Equal(3, statistics.TimestampsUtc.Count);
        Assert.Equal(4, statistics.TimestampCountsUtc.Sum(x => x.Count));
        var data = LiveTimelinePayload.Create(statistics, Now);
        Assert.Equal(new[] { 14940, 1, 14999, 1, 15000, 2 }, data.Samples);
        Assert.Equal(1, data.LatestQso!.QsoId);
        Assert.Equal(60, ContestTimeEstimator.Estimate(statistics.TimestampsUtc).GrossSeconds);
    }

    [Fact]
    public void OutOfOrderCorrectionsAndDeletionsReplaceTheWholeSeries()
    {
        var before = LiveTimelinePayload.Create(Statistics(Now.AddSeconds(-10), Now.AddSeconds(-20), Now), Now);
        var after = LiveTimelinePayload.Create(Statistics(Now.AddSeconds(-30)), Now);
        Assert.Equal(6, before.Samples.Length);
        Assert.Equal(new[] { 14970, 1 }, after.Samples);
        Assert.Equal(Now.AddSeconds(-30), after.LatestQso!.TimestampUtc);
    }

    [Fact]
    public void UtcMidnightAndDstOffsetsRepresentElapsedInstants()
    {
        var a = Now.AddMinutes(-10);
        var b = Now.ToOffset(TimeSpan.FromHours(2));
        var data = LiveTimelinePayload.Create(Statistics(b, a.ToOffset(TimeSpan.FromHours(1))), Now);
        Assert.Equal(new[] { 14400, 1, 15000, 1 }, data.Samples);
        Assert.Equal(600, ContestTimeEstimator.Estimate(Statistics(a, b).TimestampsUtc).OperatingSeconds);
    }

    [Fact]
    public void CoverageEdgesFutureBufferAndClockRecoveryAreExplicit()
    {
        var data = LiveTimelinePayload.Create(Statistics(Now.AddMinutes(-250), Now.AddMinutes(-250).AddSeconds(-1),
            Now.AddMinutes(240), Now.AddMinutes(240).AddSeconds(1), Now.AddMinutes(1)), Now);
        Assert.Equal(new[] { 0, 1, 15060, 1, 29400, 1 }, data.Samples);
        Assert.Equal(1, data.FutureBeyondCoverageCount);
        Assert.Equal(Now.AddMinutes(240).AddSeconds(1), data.LatestQso!.TimestampUtc);
    }

    [Theory]
    [InlineData(11000)]
    [InlineData(100000)]
    public void GenerationAndPayloadAreBoundedBySecondsRatherThanQsoCount(int count)
    {
        var statistics = Statistics(Enumerable.Range(0, count).Select(i => Now.AddSeconds(-(i % 14400))).ToArray());
        var watch = Stopwatch.StartNew();
        var data = LiveTimelinePayload.Create(statistics, Now);
        var generation = watch.Elapsed.TotalMilliseconds;
        watch.Restart();
        var json = LiveTimelinePayload.Serialize(data);
        output.WriteLine($"Timeline {count:N0} QSOs: generation {generation:F2} ms; serialization {watch.Elapsed.TotalMilliseconds:F2} ms; {Encoding.UTF8.GetByteCount(json):N0} bytes.");
        Assert.True(data.Samples.Length <= 58802);
        Assert.Equal(count, data.Samples.Where((_, i) => i % 2 == 1).Sum());
        Assert.True(Encoding.UTF8.GetByteCount(json) < 500000);
        Assert.DoesNotContain("sourceFile", json); Assert.DoesNotContain("timestampsUtc", json);
    }
}
