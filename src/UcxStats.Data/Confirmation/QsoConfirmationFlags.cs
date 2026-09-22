namespace UcxStats.Data.Confirmation;

[Flags]
public enum QsoConfirmationFlags
{
    None = 0,
    Paper = 1 << 0,
    Lotw = 1 << 1,
    Eqsl = 1 << 2,
    ClubLog = 1 << 3,
    Dcl = 1 << 4
}
