using UcxStats.Data.Dxcc;
using UcxStats.SmokeTest;

namespace UcxStats.Data.Tests;

public sealed class DxccAuditCliTests
{
    [Fact]
    public void ParsesDatabaseAndOptionalCsvPaths()
    {
        Assert.True(DxccAuditCommandLine.TryParse(
            ["dxcc-audit", "/data/ucxstats.db"], out var plain, out var plainError));
        Assert.Null(plainError);
        Assert.Equal("/data/ucxstats.db", plain!.DatabasePath);
        Assert.Null(plain.CsvPath);

        Assert.True(DxccAuditCommandLine.TryParse(
            ["dxcc-audit", "C:\\Data\\ucxstats.db", "--csv", "audit.csv"],
            out var csv,
            out var csvError));
        Assert.Null(csvError);
        Assert.Equal("C:\\Data\\ucxstats.db", csv!.DatabasePath);
        Assert.Equal("audit.csv", csv.CsvPath);
    }

    [Theory]
    [InlineData()]
    [InlineData("unknown")]
    [InlineData("dxcc-audit")]
    [InlineData("dxcc-audit", "db", "--csv")]
    [InlineData("dxcc-audit", "db", "--wrong", "file")]
    public void RejectsInvalidArguments(params string[] args)
    {
        Assert.False(DxccAuditCommandLine.TryParse(args, out var options, out var error));
        Assert.Null(options);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public async Task MissingDatabaseReturnsNonZeroWithoutCreatingIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-ucxstats-{Guid.NewGuid():N}.db");
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await DxccAuditCli.RunAsync(["dxcc-audit", path], output, error);

        Assert.NotEqual(0, exitCode);
        Assert.False(File.Exists(path));
        Assert.Contains("does not exist", error.ToString());
    }

    [Fact]
    public async Task TextOutputContainsSummaryAllCountriesAndConflictsInRequiredOrder()
    {
        var audit = AuditFixture();
        var writer = new StringWriter();

        await DxccAuditFormatter.WriteTextAsync(writer, "/data/ucxstats.db", audit);
        var text = writer.ToString();

        Assert.Contains("DXCC identity audit", text);
        Assert.Contains("Database: /data/ucxstats.db", text);
        Assert.Contains("Total QSOs:", text);
        Assert.Contains("75.00 %", text);
        Assert.Contains("Explicit non-DXCC QSOs:", text);
        Assert.Contains("Explicit non-DXCC evidence", text);
        Assert.Contains("explicit-non-dxcc", text);
        Assert.Contains("DCL / UCX conflicts", text);
        Assert.Contains("Effective  Source", text);
        Assert.Contains("29         281  UCX", text);
        Assert.Contains("42", text);
        Assert.DoesNotContain("Call", text);
        Assert.True(text.IndexOf("DL", StringComparison.Ordinal) < text.IndexOf("EA", StringComparison.Ordinal));
        Assert.True(text.IndexOf("EA", StringComparison.Ordinal) < text.IndexOf("XYZ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CsvContainsCompleteSortedCountryTableAndEscapesValues()
    {
        var audit = AuditFixture() with
        {
            Countries =
            [
                new DxccCountryAuditRow("X,1", null, 2, DxccCountryMappingStatus.Unresolved),
                .. AuditFixture().Countries
            ]
        };
        var writer = new StringWriter();

        await DxccAuditFormatter.WriteCsvAsync(writer, audit);
        var lines = writer.ToString().Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("UCX,QSOs,DXCC,Status,ExplicitNonDxccQSOs", lines[0]);
        Assert.Equal("DL,8,230,mapped,0", lines[1]);
        Assert.Equal("EA,3,281,mapped,1", lines[2]);
        Assert.Equal("\"X,1\",2,,unresolved,0", lines[3]);
        Assert.Equal("XYZ,1,,unresolved,0", lines[4]);
        Assert.Equal(5, lines.Length);
    }

    private static DxccIdentityAuditResult AuditFixture() => new(
        TotalQsos: 12,
        ResolvedQsos: 9,
        UnresolvedQsos: 2,
        DistinctUnresolvedCountryCodes: ["XYZ"],
        Conflicts: [new DxccIdentityConflict(42, "EA", 281, 29, 281)],
        Countries:
        [
            new DxccCountryAuditRow("XYZ", null, 1, DxccCountryMappingStatus.Unresolved),
            new DxccCountryAuditRow("EA", 281, 3, DxccCountryMappingStatus.Mapped),
            new DxccCountryAuditRow("DL", 230, 8, DxccCountryMappingStatus.Mapped)
        ],
        EvidenceCounts: new Dictionary<DxccIdentityEvidence, long>())
    {
        ExplicitNonDxccEvidence =
        [
            new DxccExplicitNonDxccEvidence(43, "EA", 281, 999)
        ]
    };
}
