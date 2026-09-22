namespace UcxStats.Core.Ucx;

public static class UcxDateTime
{
    private static readonly DateTime DelphiEpoch = new(1899, 12, 30, 0, 0, 0, DateTimeKind.Unspecified);

    public static DateTime Decode(double value) => DelphiEpoch.AddDays(value);
}
