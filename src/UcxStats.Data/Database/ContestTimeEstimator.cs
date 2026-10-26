namespace UcxStats.Data.Database;

public sealed record QsoPause(DateTimeOffset FromUtc, DateTimeOffset ToUtc, double Seconds);
public sealed record ContestTimeEstimate(DateTimeOffset? FirstUtc, DateTimeOffset? LastUtc,
    double GrossSeconds, double OperatingSeconds, double PauseSeconds, int PauseCount,
    IReadOnlyList<QsoPause> Pauses, double? AllowanceSeconds, double? RemainingSeconds, int InvalidTimestampCount);

public static class ContestTimeEstimator
{
    public static ContestTimeEstimate Estimate(IEnumerable<DateTimeOffset> timestamps, int pauseThresholdMinutes = 60,
        int? allowanceMinutes = null, int invalidTimestampCount = 0)
    {
        ArgumentNullException.ThrowIfNull(timestamps);
        if (pauseThresholdMinutes is < 5 or > 180) throw new ArgumentOutOfRangeException(nameof(pauseThresholdMinutes));
        if (allowanceMinutes is <= 0) throw new ArgumentOutOfRangeException(nameof(allowanceMinutes));
        var sorted = timestamps.Select(x => x.ToUniversalTime()).Distinct().Order().ToArray();
        var pauses = new List<QsoPause>();
        for (var i = 1; i < sorted.Length; i++)
        {
            var gap = (sorted[i] - sorted[i - 1]).TotalSeconds;
            if (gap > pauseThresholdMinutes * 60) pauses.Add(new(sorted[i - 1], sorted[i], gap));
        }
        var gross = sorted.Length < 2 ? 0 : (sorted[^1] - sorted[0]).TotalSeconds;
        var pause = pauses.Sum(x => x.Seconds);
        var operating = gross - pause;
        double? allowance = allowanceMinutes * 60d;
        return new(sorted.Length == 0 ? null : sorted[0], sorted.Length == 0 ? null : sorted[^1],
            gross, operating, pause, pauses.Count, pauses, allowance, allowance - operating, invalidTimestampCount);
    }
}
