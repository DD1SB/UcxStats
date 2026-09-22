using UcxStats.Data.Confirmation;
using UcxStats.Data.Database;

namespace UcxStats.Data.Tests;

public sealed class ConfirmationTests
{
    public static TheoryData<int, int, string?, QsoConfirmationFlags> Sources => new()
    {
        { 0x20, 0, null, QsoConfirmationFlags.Paper },
        { 0x80, 0, null, QsoConfirmationFlags.Lotw },
        { 0x40, 0, null, QsoConfirmationFlags.Eqsl },
        { 0, 0x20, null, QsoConfirmationFlags.ClubLog },
        { 0, 0, "c", QsoConfirmationFlags.Dcl },
        { 0, 0, "I", QsoConfirmationFlags.Dcl },
        { 0, 0, "m", QsoConfirmationFlags.Dcl },
        { 0, 0, "n", QsoConfirmationFlags.Dcl },
        { 0, 0, "o", QsoConfirmationFlags.Dcl },
        { 0, 0, "x", QsoConfirmationFlags.None },
        { 0, 0, null, QsoConfirmationFlags.None },
        { 0xE0, 0x20, "c", QsoConfirmationFlags.Paper | QsoConfirmationFlags.Lotw |
                                      QsoConfirmationFlags.Eqsl | QsoConfirmationFlags.ClubLog |
                                      QsoConfirmationFlags.Dcl }
    };

    [Theory]
    [MemberData(nameof(Sources))]
    public void UcxAndDclSourcesProduceExpectedFlags(
        int status151,
        int status161,
        string? dclStatus,
        QsoConfirmationFlags expected)
    {
        Assert.Equal(expected, QsoConfirmationEvaluator.GetFlags(status151, status161, dclStatus));
    }

    [Fact]
    public void ExistingWaePolicyAcceptsEveryIndividualConfirmationSource()
    {
        foreach (var flag in new[]
                 {
                     QsoConfirmationFlags.Paper,
                     QsoConfirmationFlags.Lotw,
                     QsoConfirmationFlags.Eqsl,
                     QsoConfirmationFlags.ClubLog,
                     QsoConfirmationFlags.Dcl
                 })
            Assert.True(QsoConfirmationEvaluator.IsConfirmedForExistingWaePolicy(flag));

        Assert.False(QsoConfirmationEvaluator.IsConfirmedForExistingWaePolicy(QsoConfirmationFlags.None));
    }

    [Theory]
    [InlineData(0x20, 0, "", true)]
    [InlineData(0x80, 0, "", true)]
    [InlineData(0x40, 0, "", true)]
    [InlineData(0, 0x20, "", true)]
    [InlineData(0, 0, "c", true)]
    [InlineData(0, 0, "", false)]
    public void WaeCandidateConfirmationBehaviorIsUnchanged(
        int status151,
        int status161,
        string dclStatus,
        bool expected)
    {
        var candidate = new WaeQsoCandidate(
            1, "source.ucx", "2026-01-01", "12:00:00", "DL1ABC", 4, 0,
            "DL", "", status151, status161, dclStatus, null, 999);

        Assert.Equal(expected, candidate.Confirmed);
        Assert.Equal(999, candidate.DclAdif2);
    }
}
