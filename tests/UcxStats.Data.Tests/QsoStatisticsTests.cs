using UcxStats.Data.Database;

namespace UcxStats.Data.Tests;

public class QsoStatisticsTests
{
    [Theory]
    [InlineData("jo", 1, 0)]
    [InlineData(" jo40 ", 1, 1)]
    [InlineData("JO40aa", 1, 1)]
    [InlineData("FN31pr12", 1, 1)]
    [InlineData("JO40AA12AB", 1, 1)]
    [InlineData("JO4", 0, 0)]
    [InlineData("JO40ZZ", 0, 0)]
    [InlineData("ZZ40", 0, 0)]
    [InlineData("JO4A", 0, 0)]
    [InlineData("", 0, 0)]
    public void LocatorCountsUseCompleteExistingValidation(string locator, int fields, int grids)
    {
        var a = new QsoStatisticsAccumulator();
        a.Add(1, "TEST", 4, locator, "2026-01-01", "12:00:00", null, false, null);
        Assert.Equal(fields, a.Build().Summarize().MajorFields);
        Assert.Equal(grids, a.Build().Summarize().Grids);
    }

    [Fact]
    public void BandCombinationsUseDistinctUnionsAndEmptyMeansNone()
    {
        var a = new QsoStatisticsAccumulator();
        foreach (var band in new[] { 2, 4, 16 })
        {
            a.Add(band, "EA1AAA", band, "jo40aa", "2026-01-01", "12:00:00", 281, true, band * 10);
            a.Add(band + 100, "EA1BBB", band, "JO40AB", "2026-01-01", "12:00:00", 281, false, null);
        }
        a.Add(200, "OTHER", 10, "FN31PR12", "2026-01-01", "13:00:00", 291, true, 100);
        var s = a.Build();
        Assert.Equal(new[] { 2, 4, 10, 16 }, s.Bands.Select(x => x.BandCode));
        var combined = s.Summarize([2, 4, 16]);
        Assert.Equal(6, combined.TotalQsos); Assert.Equal(3, combined.MappableQsos);
        Assert.Equal(3, combined.NonMappableQsos);
        Assert.Equal(1, combined.Dxccs); Assert.Equal(1, combined.MajorFields); Assert.Equal(1, combined.Grids);
        Assert.Equal(2, s.Summarize().Dxccs); Assert.Equal(2, s.Summarize().Grids);
        Assert.Equal(0, s.Summarize([]).TotalQsos); Assert.Null(s.Summarize([]).Odx);
        Assert.Equal(3, Assert.Single(s.DxccMemberships, x => x.BandCodes.Length == 3).BandCodes.Length);
        Assert.All(s.Bands, b => Assert.Equal(1, b.Summary.Dxccs));
    }

    [Fact]
    public void InvalidDxccAndNonFiniteDistancesAreExcludedAndTiesUseLowestId()
    {
        var a = new QsoStatisticsAccumulator();
        foreach (var id in new[] { 9999, 999, 0, -1 })
            a.Add(id, "TEST", 4, "", "2026-01-01", "12:00:00", id, false, double.NaN);
        a.Add(20, "SECOND", 4, "JO40", "2026-01-01", "12:00:00", 281, true, 1000);
        a.Add(10, "FIRST", 2, "FN31", "2026-01-01", "12:00:00", 291, true, 1000);
        a.Add(30, "SHORT", 10, "JO40", "2026-01-01", "11:00:00", null, true, 999);
        var s = a.Build();
        Assert.Equal(2, s.Summarize().Dxccs);
        Assert.Equal("FIRST", s.Summarize().Odx!.CallSign);
        Assert.Equal(10, s.Summarize().Odx!.QsoId);
        Assert.Equal("FN31", s.Summarize().Odx!.Locator);
        // Timestamp ties are deterministic as well, even for locator-less QSOs.
        Assert.Equal(-1, s.LatestQso!.QsoId);
        Assert.Null(s.Summarize([99]).Odx);
    }
}
