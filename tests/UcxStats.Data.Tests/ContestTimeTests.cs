using UcxStats.Data.Database;

namespace UcxStats.Data.Tests;

public class ContestTimeTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 23, 30, 0, TimeSpan.Zero);
    private static ContestTimeEstimate Estimate(int[] minutes, int threshold = 60, int? allowance = null) =>
        ContestTimeEstimator.Estimate(minutes.Select(x => Start.AddMinutes(x)), threshold, allowance);

    [Fact]
    public void EmptyAndSingleQsoHaveZeroElapsedTime()
    {
        var empty = Estimate([]);
        Assert.Null(empty.FirstUtc); Assert.Null(empty.LastUtc);
        Assert.Equal(0, empty.GrossSeconds); Assert.Equal(0, empty.OperatingSeconds);
        var single = Estimate([0]);
        Assert.Equal(Start, single.FirstUtc); Assert.Equal(Start, single.LastUtc);
        Assert.Equal(0, single.GrossSeconds); Assert.Empty(single.Pauses);
    }

    [Fact]
    public void SortedDistinctUtcTimesUseStrictThresholdAcrossMidnight()
    {
        var result = Estimate([121, 60, 0, 60]);
        Assert.Equal(121 * 60, result.GrossSeconds);
        Assert.Equal(60 * 60, result.OperatingSeconds);
        Assert.Equal(61 * 60, result.PauseSeconds);
        Assert.Equal(1, result.PauseCount);
        Assert.Equal(Start.AddMinutes(60), Assert.Single(result.Pauses).FromUtc);
    }

    [Fact]
    public void MultiDayLogRecognizesMultipleEntirePausesAndAllowanceExceeded()
    {
        var result = Estimate([0, 30, 120, 150, 2880, 2940], allowance: 90);
        Assert.Equal(2940 * 60, result.GrossSeconds);
        Assert.Equal(120 * 60, result.OperatingSeconds);
        Assert.Equal(2820 * 60, result.PauseSeconds);
        Assert.Equal(2, result.PauseCount);
        Assert.Equal(-30 * 60, result.RemainingSeconds);
        Assert.Equal(90 * 60, result.AllowanceSeconds);
    }

    [Fact]
    public void ConfigurableThresholdAndAllowanceUseMinutes()
    {
        Assert.Equal(0, Estimate([0, 6], 5).OperatingSeconds);
        Assert.Equal(6 * 60, Estimate([0, 6], 6).OperatingSeconds);
        Assert.Equal(54 * 60, Estimate([0, 6], 6, 60).RemainingSeconds);
        Assert.Equal(180 * 60, Estimate([0, 180], 180).OperatingSeconds);
        Assert.Throws<ArgumentOutOfRangeException>(() => Estimate([0], 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => Estimate([0], 181));
        Assert.Throws<ArgumentOutOfRangeException>(() => Estimate([0], allowance: 0));
    }

    [Fact]
    public void OffsetChangesUseElapsedUtcAndDuplicateInstantsCollapse()
    {
        var result = ContestTimeEstimator.Estimate([Start, Start.ToOffset(TimeSpan.FromHours(2)),
            Start.AddMinutes(60).ToOffset(TimeSpan.FromHours(1))]);
        Assert.Equal(3600, result.OperatingSeconds);
        Assert.Equal(TimeSpan.Zero, result.FirstUtc!.Value.Offset);
        Assert.Equal(TimeSpan.Zero, result.LastUtc!.Value.Offset);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("2026-02-30", "12:00:00")]
    [InlineData("2026-01-01", "25:00:00")]
    [InlineData("nonsense", "12:00:00")]
    public void InvalidAndMissingTimestampsAreExcluded(string date, string time)
    {
        var accumulator = new QsoStatisticsAccumulator();
        accumulator.Add(1, "TEST", 4, "", date, time, null, false, null);
        var snapshot = accumulator.Build();
        Assert.Equal(1, snapshot.Summarize().TotalQsos);
        Assert.Empty(snapshot.TimestampsUtc); Assert.Null(snapshot.LatestQso);
        Assert.Equal(1, ContestTimeEstimator.Estimate(snapshot.TimestampsUtc,
            invalidTimestampCount: snapshot.InvalidTimestampCount).InvalidTimestampCount);
    }
}
