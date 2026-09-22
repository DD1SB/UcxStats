using UcxStats.Data.Dxcc;

namespace UcxStats.Data.Tests;

public sealed class DxccIdentityTests
{
    [Fact]
    public void MappingVersion3UsesTheCompleteUcxLogEntitySource()
    {
        Assert.Equal(3, DxccIdentityResolver.MappingVersion);
        Assert.Equal(402, DxccIdentityResolver.ExplicitCountryMappings.Count);
        Assert.Equal(452, DxccIdentityResolver.CountryMappingMetadata.Count);
    }

    [Theory]
    [InlineData("EA", 281)]
    [InlineData("ea6", 21)]
    [InlineData(" EA8 ", 29)]
    [InlineData("OH0", 5)]
    [InlineData("OJ0", 167)]
    [InlineData("SV5", 45)]
    [InlineData("SV9", 40)]
    [InlineData("UA2", 126)]
    [InlineData("UA3", 54)]
    [InlineData("UA9", 15)]
    [InlineData("R1FJ", 61)]
    [InlineData("Z6", 522)]
    public void ExplicitKnownUcxCountryResolves(string country, int expected)
    {
        Assert.Equal(expected, DxccIdentityResolver.ResolveUcxCountry(country));
    }

    [Theory]
    [InlineData("DL", 230)]
    [InlineData("I", 248)]
    [InlineData("W", 291)]
    [InlineData("VK", 150)]
    [InlineData("S5", 499)]
    [InlineData("E7", 501)]
    [InlineData("KP4", 202)]
    public void RepresentativeDirectMappingsResolve(string country, int expected)
    {
        var mapping = DxccIdentityResolver.ClassifyUcxCountry(country);

        Assert.Equal(expected, mapping.EntityId);
        Assert.Equal(UcxDxccMappingKind.Direct, mapping.Kind);
    }

    [Theory]
    [InlineData("4U_IT", 117)]
    [InlineData("PJ5/6", 519)]
    [InlineData("UA3", 54)]
    [InlineData("UA9", 15)]
    [InlineData("VP8/F", 141)]
    [InlineData("VP8/H", 241)]
    public void UcxAliasesRetainExplicitMappingMetadata(string country, int expected)
    {
        var mapping = DxccIdentityResolver.ClassifyUcxCountry(country);

        Assert.Equal(expected, mapping.EntityId);
        Assert.Equal(UcxDxccMappingKind.Alias, mapping.Kind);
        Assert.False(string.IsNullOrWhiteSpace(mapping.Rationale));
    }

    [Theory]
    [InlineData("4U_IT", 117)]
    [InlineData("CE9", 13)]
    [InlineData("CT3", 256)]
    [InlineData("EA6", 21)]
    [InlineData("EA8", 29)]
    [InlineData("EA9", 32)]
    [InlineData("HK0", 216)]
    [InlineData("IS", 225)]
    [InlineData("PJ2", 517)]
    [InlineData("PJ4", 520)]
    [InlineData("PJ5/6", 519)]
    [InlineData("PJ7", 518)]
    [InlineData("PY0F", 56)]
    [InlineData("TA", 390)]
    [InlineData("TK", 214)]
    [InlineData("UA2", 126)]
    [InlineData("UA3", 54)]
    [InlineData("UA9", 15)]
    [InlineData("VK9N", 189)]
    [InlineData("VP8/F", 141)]
    [InlineData("VP8/H", 241)]
    [InlineData("ZB2", 233)]
    [InlineData("Z6", 522)]
    [InlineData("R1FJ", 61)]
    public void AuditedSpecialMappingsResolveToTheirAdifEntity(string country, int expected)
    {
        Assert.Equal(expected, DxccIdentityResolver.ResolveUcxCountry(country));
    }

    [Theory]
    [InlineData("4U1V")]
    [InlineData("4U_V")]
    [InlineData("GM/S")]
    [InlineData("IT")]
    [InlineData("IT9")]
    [InlineData("JW/B")]
    [InlineData("TA1")]
    [InlineData("XYZ")]
    public void UnobservedOrWaeOnlyLabelsAreNotInventedAsDxccMappings(string country)
    {
        var mapping = DxccIdentityResolver.ClassifyUcxCountry(country);

        Assert.Null(mapping.EntityId);
        Assert.Equal(UcxDxccMappingKind.Unknown, mapping.Kind);
        Assert.Null(DxccIdentityResolver.ResolveUcxCountry(country));
    }

    [Fact]
    public void UnknownUcxCountryRemainsUnresolved()
    {
        var resolution = DxccIdentityResolver.Resolve("XYZ", null);
        Assert.Null(resolution.EffectiveEntityId);
        Assert.Equal(DxccIdentityEvidence.Unresolved, resolution.Evidence);
        Assert.Equal(DxccIdentityState.Unresolved, resolution.State);
    }

    [Fact]
    public void ValidDclDxccProvidesFallbackWhenUcxIsUnresolved()
    {
        var resolution = DxccIdentityResolver.Resolve("XYZ", 123);
        Assert.Null(resolution.UcxEntityId);
        Assert.Equal(123, resolution.DclEntityId);
        Assert.Equal(123, resolution.EffectiveEntityId);
        Assert.Equal(DxccIdentityEvidence.DclFallback, resolution.Evidence);
        Assert.Equal(DxccIdentityState.Resolved, resolution.State);
    }

    [Fact]
    public void Dcl999IsExplicitNonDxccAndSuppressesMappedUcxFallback()
    {
        var resolution = DxccIdentityResolver.Resolve("9A", 999);

        Assert.Equal(497, resolution.UcxEntityId);
        Assert.Equal(999, resolution.DclEntityId);
        Assert.Null(resolution.EffectiveEntityId);
        Assert.Equal(DxccIdentityEvidence.ExplicitNonDxcc, resolution.Evidence);
        Assert.Equal(DxccIdentityState.ExplicitNonDxcc, resolution.State);
        Assert.False(resolution.HasConflict);
        Assert.False(DxccIdentityResolver.IsPlausibleDclEntityId(999));
        Assert.True(DxccIdentityResolver.IsRecognizedDclIdentityCode(999));
    }

    [Fact]
    public void AdifDxccZeroIsExplicitNonDxccAndSuppressesMappedUcxFallback()
    {
        var resolution = DxccIdentityResolver.Resolve("EA", 0);

        Assert.Equal(281, resolution.UcxEntityId);
        Assert.Equal(0, resolution.DclEntityId);
        Assert.Null(resolution.EffectiveEntityId);
        Assert.Equal(DxccIdentityEvidence.ExplicitNonDxcc, resolution.Evidence);
        Assert.Equal(DxccIdentityState.ExplicitNonDxcc, resolution.State);
        Assert.False(resolution.HasConflict);
        Assert.False(DxccIdentityResolver.IsPlausibleDclEntityId(0));
        Assert.True(DxccIdentityResolver.IsRecognizedDclIdentityCode(0));
    }

    [Fact]
    public void CatalogRecognizedDeletedEntityCanRemainDclFallback()
    {
        var resolution = DxccIdentityResolver.Resolve("XYZ", 229);

        Assert.Equal(229, resolution.EffectiveEntityId);
        Assert.Equal(DxccIdentityEvidence.DclFallback, resolution.Evidence);
        Assert.Equal(DxccCatalogEntityStatus.Deleted,
            DxccEntityCatalog.ReleasedAdif317.GetStatus(resolution.EffectiveEntityId!.Value));
    }

    [Fact]
    public void UnrecognizedNumericDclValueDoesNotBecomeFallback()
    {
        var resolution = DxccIdentityResolver.Resolve("XYZ", 600);

        Assert.Null(resolution.DclEntityId);
        Assert.Null(resolution.EffectiveEntityId);
        Assert.Equal(DxccIdentityEvidence.Unresolved, resolution.Evidence);
    }

    [Fact]
    public void ResolvedUcxWinsNormalDclConflictAndBothRemainDiagnostic()
    {
        var resolution = DxccIdentityResolver.Resolve("EA", 29);
        Assert.Equal(281, resolution.UcxEntityId);
        Assert.Equal(29, resolution.DclEntityId);
        Assert.Equal(281, resolution.EffectiveEntityId);
        Assert.Equal(DxccIdentityEvidence.Conflict, resolution.Evidence);
        Assert.Equal(DxccIdentityState.Resolved, resolution.State);
        Assert.True(resolution.HasConflict);
    }

    [Fact]
    public void ScotlandUcxWinsEnglandDclConflict()
    {
        var resolution = DxccIdentityResolver.Resolve("GM", 223);

        Assert.Equal(279, resolution.UcxEntityId);
        Assert.Equal(223, resolution.DclEntityId);
        Assert.Equal(279, resolution.EffectiveEntityId);
        Assert.Equal(DxccIdentityEvidence.Conflict, resolution.Evidence);
    }

    [Fact]
    public void AgreementAndUcxOnlyAreClassified()
    {
        var agreement = DxccIdentityResolver.Resolve("DL", 230);
        Assert.Equal(230, agreement.EffectiveEntityId);
        Assert.Equal(DxccIdentityEvidence.Agreement, agreement.Evidence);

        var ucxOnly = DxccIdentityResolver.Resolve("EA", null);
        Assert.Equal(281, ucxOnly.EffectiveEntityId);
        Assert.Equal(DxccIdentityEvidence.UcxOnly, ucxOnly.Evidence);
        Assert.Equal(DxccIdentityState.Resolved,
            ucxOnly.State);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(600)]
    [InlineData(1000)]
    public void ImplausibleDclEntityIdIsIgnored(int dclDxcc)
    {
        Assert.Equal(281, DxccIdentityResolver.Resolve("EA", dclDxcc).EffectiveEntityId);
    }
}
