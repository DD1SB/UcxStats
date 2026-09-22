using System.IO;
using UcxStats.Core.Ucx;

namespace UcxStats.Wpf;

public sealed record QsoGridRow(string SourceFile, UcxQsoRecord Qso)
{
    public string Source => Path.GetFileName(SourceFile);
    public DateOnly Date => Qso.Date;
    public TimeOnly StartTime => Qso.StartTime;
    public string CallSign => Qso.CallSign;
    public string BandDisplay => Qso.BandDisplay;
    public string ModeDisplay => Qso.ModeDisplay;
    public string FrequencyDisplay => Qso.FrequencyDisplay;
    public string Locator => Qso.Locator;
    public string Country => Qso.Country;
    public string Qth => Qso.Qth;
    public string Name => Qso.Name;
    public string Sent => string.Join(" ", new[] { Qso.SentRst, Qso.SentExchange }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string Received => string.Join(" ", new[] { Qso.ReceivedRst, Qso.ReceivedExchange }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public string Remarks => Qso.Remarks;
    public string Manager => Qso.Manager;
    public string District => Qso.District;
    public string Iota => UcxDisplayFormat.Iota(Qso.IotaContinentCode, Qso.IotaNumber);
    public string Zones => $"CQ {Qso.CqZone} / ITU {Qso.ItuZone}";
    public string QslStatus => string.Join(", ", new[]
    {
        Qso.PaperQslSent ? "QSL gesendet" : null,
        Qso.PaperQslReceived ? "QSL empfangen" : null,
        Qso.LotwUploaded ? "LoTW hochgeladen" : null,
        Qso.LotwConfirmed ? "LoTW bestätigt" : null,
        Qso.EqslConfirmed ? "eQSL bestätigt" : null,
        Qso.ClubLogUploaded ? "ClubLog hochgeladen" : null,
        Qso.ClubLogConfirmed ? "ClubLog bestätigt" : null
    }.Where(x => x is not null));
    public string Awards => string.Join(" · ", new[]
    {
        string.IsNullOrWhiteSpace(Qso.Award1) ? null : $"{Qso.Award1}: {Qso.Count1}",
        string.IsNullOrWhiteSpace(Qso.Award2) ? null : $"{Qso.Award2}: {Qso.Count2}"
    }.Where(x => x is not null));
}

