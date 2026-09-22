using UcxStats.Data.Dxcc;

namespace UcxStats.Data.Tests;

public sealed class UcxCompleteMappingTests
{
    [Fact]
    public void UcxLog850SourceMetadataAndCompleteMappingAuditAreStable()
    {
        var source = DxccIdentityResolver.CountryMappingSource;
        Assert.Equal("UcxLog", source.Product);
        Assert.Equal("8.50", source.Version);
        Assert.Equal(new DateOnly(2026, 6, 11), source.CountryDataDate);
        Assert.Equal("UcxLog.zip", source.ArchiveFileName);
        Assert.Equal(
            "6c972c70cf6284f784610394de2afe31039f51608d21f530a4e6c05015d1461f",
            source.ArchiveSha256);
        Assert.Equal(
            "4564deb87791947228984bc92d1181ab8574d517e1e9e038ecf91145effc2186",
            source.ResourceSha256);
        Assert.Equal(
            "05685def3be5880e8e09ace9fc87257f78ec27710d6cc3d73e0f414f80abce3f",
            source.DecodedResourceSha256);

        var audit = DxccIdentityResolver.AuditCountryMappings();
        Assert.Equal(455, audit.SourceEntityRowCount);
        Assert.Equal(452, audit.DistinctCountryCodeCount);
        Assert.Equal(340, audit.CurrentMappings);
        Assert.Equal(62, audit.DeletedMappings);
        Assert.Equal(0, audit.ExplicitNonDxccMappings);
        Assert.Equal(50, audit.UnresolvedMappings);
        Assert.Equal(["I", "TA", "Z6"], audit.DuplicateSourceCountryCodes);
        Assert.Empty(audit.DuplicateTargetEntityIds);
        Assert.Empty(audit.UnknownTargetCountryCodes);
        Assert.Equal(452, DxccIdentityResolver.CountryMappingMetadata.Count);
        Assert.Equal(402, DxccIdentityResolver.ExplicitCountryMappings.Count);
    }

    [Fact]
    public void EveryMappedTargetExistsWithMatchingCurrentOrDeletedClassification()
    {
        var catalog = DxccEntityCatalog.ReleasedAdif317;

        foreach (var mapping in DxccIdentityResolver.CountryMappingMetadata.Values.Where(x => x.IsResolved))
        {
            var expected = mapping.Kind == UcxDxccMappingKind.Historical
                ? DxccCatalogEntityStatus.Deleted
                : DxccCatalogEntityStatus.Current;
            Assert.Equal(expected, catalog.GetStatus(mapping.EntityId!.Value));
        }
    }

    [Fact]
    public void AllUnresolvedAuthoritativeSourceCodesAreExplicitAndStable()
    {
        var expected = new[]
        {
            "4U_V", "GM/S", "IT", "JW/B", "R0B/S", "R0B/U", "R0B/V", "R0C", "R0F",
            "R0K/A", "R0K/R", "R0K/W", "R0O", "R0Q/B", "R0Q/M", "R0Q/N", "R0Q/Y",
            "R0W", "R0X", "R0Z", "R1C", "R1O/M", "R1O/N", "R1O/S", "R1P/K", "R1P/V",
            "R1W", "R3Y", "R4P", "R4S", "R4U", "R4W", "R4Y", "R6E", "R6I", "R6J",
            "R6K", "R6P", "R6Q", "R6W", "R6X", "R6Y", "R8W", "R9K", "R9X", "R9Z",
            "TA1", "UF6O", "UF6V", "UN7"
        };

        var audit = DxccIdentityResolver.AuditCountryMappings();
        Assert.Equal(expected, audit.UnresolvedCountryCodes);
        Assert.All(expected, code => Assert.Null(DxccIdentityResolver.ResolveUcxCountry(code)));
    }

    [Fact]
    public void Version2AuditMappingsRemainStableExceptDocumented7qCorrection()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "ucx-mapping-v2-baseline.tsv");
        var baseline = File.ReadLines(path)
            .Skip(1)
            .Select(line => line.Split('\t'))
            .ToDictionary(parts => parts[0], parts => int.Parse(parts[1]), StringComparer.OrdinalIgnoreCase);

        Assert.Equal(188, baseline.Count);
        foreach (var (country, previousEntityId) in baseline)
        {
            var currentEntityId = DxccIdentityResolver.ResolveUcxCountry(country);
            if (country == "7Q")
            {
                Assert.Equal(432, previousEntityId);
                Assert.Equal(440, currentEntityId);
            }
            else
            {
                Assert.Equal(previousEntityId, currentEntityId);
            }
        }
    }

    [Fact]
    public void Authoritative7qCorrectionSelectsMalawiRatherThanLesotho()
    {
        Assert.Equal(440, DxccIdentityResolver.ResolveUcxCountry("7Q"));
        Assert.Equal("MALAWI", DxccEntityCatalog.ReleasedAdif317.Find(440)?.Name);
        Assert.Equal("LESOTHO", DxccEntityCatalog.ReleasedAdif317.Find(432)?.Name);
    }

    [Theory]
    [InlineData("#229", 229)]
    [InlineData("#151", 151)]
    public void HistoricalUcxCodesRetainDeletedDxccIdentity(string country, int expectedEntityId)
    {
        var mapping = DxccIdentityResolver.ClassifyUcxCountry(country);
        Assert.Equal(expectedEntityId, mapping.EntityId);
        Assert.Equal(UcxDxccMappingKind.Historical, mapping.Kind);
        Assert.Equal(DxccCatalogEntityStatus.Deleted,
            DxccEntityCatalog.ReleasedAdif317.GetStatus(expectedEntityId));
    }
}
