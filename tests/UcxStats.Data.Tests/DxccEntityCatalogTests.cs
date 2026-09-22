using UcxStats.Data.Dxcc;

namespace UcxStats.Data.Tests;

public sealed class DxccEntityCatalogTests
{
    private static readonly DxccEntityCatalog Catalog = DxccEntityCatalog.ReleasedAdif317;

    [Fact]
    public void ReleasedAdif317MetadataAndCountsAreStable()
    {
        Assert.Equal("ADIF", Catalog.Source.Standard);
        Assert.Equal("3.1.7", Catalog.Source.Version);
        Assert.Equal(new DateOnly(2026, 3, 22), Catalog.Source.ReleasedOn);
        Assert.Equal("ADIF_317_resources_2026_03_22.zip", Catalog.Source.ArchiveFileName);
        Assert.Equal("317/exports/tsv/enumerations_dxcc_entity_code.tsv", Catalog.Source.ResourcePath);
        Assert.Equal("569e88410f5c69291e74a6b5f0d420647d4d109446c1596b4e3a38b73979dc15",
            Catalog.Source.ResourceSha256);

        Assert.Equal(403, Catalog.Entries.Count);
        Assert.Equal(340, Catalog.Entries.Count(x => x.Status == DxccCatalogEntityStatus.Current));
        Assert.Equal(62, Catalog.Entries.Count(x => x.Status == DxccCatalogEntityStatus.Deleted));
        Assert.Single(Catalog.Entries, x => x.Status == DxccCatalogEntityStatus.Unknown);
    }

    [Fact]
    public void CurrentDeletedUnknownAndNonDxccSentinelAreDistinct()
    {
        Assert.Equal(DxccCatalogEntityStatus.Current, Catalog.GetStatus(230));
        Assert.Equal("FEDERAL REPUBLIC OF GERMANY", Catalog.Find(230)?.Name);
        Assert.Equal(DxccCatalogEntityStatus.Deleted, Catalog.GetStatus(229));
        Assert.Equal("GERMAN DEMOCRATIC REPUBLIC", Catalog.Find(229)?.Name);
        Assert.Equal(DxccCatalogEntityStatus.Unknown, Catalog.GetStatus(9999));
        Assert.Null(Catalog.Find(9999));

        var none = Assert.IsType<DxccCatalogEntry>(Catalog.Find(0));
        Assert.Equal(DxccCatalogEntityStatus.Unknown, none.Status);
        Assert.True(none.IsExplicitNonDxcc);
    }

    [Theory]
    [InlineData(21, "BALEARIC IS.")]
    [InlineData(29, "CANARY IS.")]
    [InlineData(32, "CEUTA & MELILLA")]
    [InlineData(5, "ALAND IS.")]
    [InlineData(167, "MARKET REEF")]
    [InlineData(180, "MOUNT ATHOS")]
    [InlineData(45, "DODECANESE")]
    [InlineData(40, "CRETE")]
    [InlineData(126, "KALININGRAD")]
    [InlineData(522, "REPUBLIC OF KOSOVO")]
    [InlineData(117, "ITU HQ")]
    [InlineData(289, "UNITED NATIONS HQ")]
    public void SpecialDxccEntitiesAreCurrent(int entityId, string name)
    {
        var entity = Assert.IsType<DxccCatalogEntry>(Catalog.Find(entityId));
        Assert.Equal(DxccCatalogEntityStatus.Current, entity.Status);
        Assert.Equal(name, entity.Name);
    }

    [Fact]
    public void WaeOnlyEntityLabelsAreNotInventedAsDxccCatalogEntries()
    {
        var names = Catalog.Entries.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("4U1V", names);
        Assert.DoesNotContain("GM/S", names);
        Assert.DoesNotContain("JW/B", names);
        Assert.DoesNotContain("TA1", names);
        Assert.DoesNotContain("Sicily", names);
    }

    [Fact]
    public void MappingVersion3IdsAreRecognizedWithoutCouplingTheModels()
    {
        Assert.Equal(3, DxccIdentityResolver.MappingVersion);
        Assert.All(
            DxccIdentityResolver.CountryMappingMetadata.Values.Where(x => x.IsResolved),
            mapping => Assert.Equal(
                mapping.Kind == UcxDxccMappingKind.Historical
                    ? DxccCatalogEntityStatus.Deleted
                    : DxccCatalogEntityStatus.Current,
                Catalog.GetStatus(mapping.EntityId!.Value)));
    }
}
