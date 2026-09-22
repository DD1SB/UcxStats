namespace UcxStats.Data.Confirmation;

public static class QsoConfirmationEvaluator
{
    public const int PaperStatus151Mask = 0x20;
    public const int EqslStatus151Mask = 0x40;
    public const int LotwStatus151Mask = 0x80;
    public const int ClubLogStatus161Mask = 0x20;

    public static QsoConfirmationFlags GetFlags(
        int status151Raw,
        int status161Raw,
        string? dclStatus)
    {
        var result = QsoConfirmationFlags.None;
        if ((status151Raw & PaperStatus151Mask) != 0) result |= QsoConfirmationFlags.Paper;
        if ((status151Raw & LotwStatus151Mask) != 0) result |= QsoConfirmationFlags.Lotw;
        if ((status151Raw & EqslStatus151Mask) != 0) result |= QsoConfirmationFlags.Eqsl;
        if ((status161Raw & ClubLogStatus161Mask) != 0) result |= QsoConfirmationFlags.ClubLog;
        if (IsDclConfirmedStatus(dclStatus)) result |= QsoConfirmationFlags.Dcl;
        return result;
    }

    public static bool IsDclConfirmedStatus(string? status) =>
        status?.Trim().ToLowerInvariant() is "c" or "i" or "m" or "n" or "o";

    public static bool IsConfirmedForExistingWaePolicy(QsoConfirmationFlags flags) =>
        flags != QsoConfirmationFlags.None;

    public static bool HasOtherConfirmation(QsoConfirmationFlags flags) =>
        (flags & (QsoConfirmationFlags.Eqsl | QsoConfirmationFlags.ClubLog)) != 0;

    public static bool HasDxccCredit(QsoConfirmationFlags flags) =>
        (flags & (QsoConfirmationFlags.Paper | QsoConfirmationFlags.Lotw)) != 0;
}
