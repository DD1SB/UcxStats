using UcxStats.Data.Dxcc;

namespace UcxStats.Data.Tests;

public sealed class DxccStatusTests
{
    private static readonly DxccEntityBandStatus Worked = new(281, 4, 1, 0, 0);
    private static readonly DxccEntityBandStatus Other = new(281, 6, 1, 1, 0);
    private static readonly DxccEntityBandStatus Credited = new(281, 8, 1, 0, 1);

    [Theory]
    [InlineData(DxccCatalogEntityStatus.Unknown)]
    [InlineData(DxccCatalogEntityStatus.Deleted)]
    public void NonCurrentEntitiesAreUnknown(DxccCatalogEntityStatus catalogStatus)
    {
        Assert.Equal(DxccOverlayStatus.Unknown,
            DxccOverlayStatusEvaluator.Evaluate(catalogStatus, [Credited]));
    }

    [Fact]
    public void CurrentEntityWithoutQsosIsNotWorked()
    {
        Assert.Equal(DxccOverlayStatus.NotWorked,
            DxccOverlayStatusEvaluator.Evaluate(DxccCatalogEntityStatus.Current, []));
    }

    [Fact]
    public void WorkedWithoutRelevantConfirmationIsWorked()
    {
        Assert.Equal(DxccOverlayStatus.Worked,
            DxccOverlayStatusEvaluator.Evaluate(DxccCatalogEntityStatus.Current, [Worked]));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    public void EqslOrClubLogIsOtherConfirmed(int otherCount, int creditedCount)
    {
        var aggregate = new DxccEntityBandStatus(281, 4, 3, otherCount, creditedCount);
        Assert.Equal(DxccOverlayStatus.OtherConfirmed,
            DxccOverlayStatusEvaluator.Evaluate(DxccCatalogEntityStatus.Current, [aggregate]));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(2, 2)]
    public void LotwOrPaperIsDxccCredited(int otherCount, int creditedCount)
    {
        var aggregate = new DxccEntityBandStatus(281, 4, 3, otherCount, creditedCount);
        Assert.Equal(DxccOverlayStatus.DxccCredited,
            DxccOverlayStatusEvaluator.Evaluate(DxccCatalogEntityStatus.Current, [aggregate]));
    }

    [Fact]
    public void HighestStatusWinsAcrossQsosAndSelectedBands()
    {
        var bands = new[] { Worked, Other, Credited };

        Assert.Equal(DxccOverlayStatus.OtherConfirmed,
            DxccOverlayStatusEvaluator.Evaluate(DxccCatalogEntityStatus.Current, bands, [4, 6]));
        Assert.Equal(DxccOverlayStatus.DxccCredited,
            DxccOverlayStatusEvaluator.Evaluate(DxccCatalogEntityStatus.Current, bands, [6, 8]));
        Assert.Equal(DxccOverlayStatus.NotWorked,
            DxccOverlayStatusEvaluator.Evaluate(DxccCatalogEntityStatus.Current, bands, [10]));
    }

    [Fact]
    public void DclIsNotRepresentedInOverlayPromotionCounts()
    {
        var dclOnlyAggregate = new DxccEntityBandStatus(281, 4, 1, 0, 0);
        Assert.Equal(DxccOverlayStatus.Worked,
            DxccOverlayStatusEvaluator.Evaluate(DxccCatalogEntityStatus.Current, [dclOnlyAggregate]));
    }

    [Fact]
    public void CatalogDistinguishesCurrentDeletedAndUnknown()
    {
        var catalog = DxccEntityCatalog.ReleasedAdif317;
        Assert.Equal(DxccCatalogEntityStatus.Current, catalog.Find(281)?.Status);
        Assert.Equal(DxccCatalogEntityStatus.Deleted, catalog.Find(229)?.Status);
        Assert.Equal(DxccCatalogEntityStatus.Unknown, catalog.GetStatus(9999));
        Assert.Null(catalog.Find(9999));
    }
}
