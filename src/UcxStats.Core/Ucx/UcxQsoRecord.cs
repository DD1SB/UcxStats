namespace UcxStats.Core.Ucx;

public sealed record UcxQsoRecord
{
    public required long RecordIndex { get; init; }
    public required byte RecordTypeRaw { get; init; }

    public required DateOnly Date { get; init; }
    public required TimeOnly StartTime { get; init; }
    public required TimeOnly EndTime { get; init; }

    public required string CallSign { get; init; }
    public required string SentRst { get; init; }
    public required string SentExchange { get; init; }
    public required string ReceivedRst { get; init; }
    public required string ReceivedExchange { get; init; }

    public required byte BandCode { get; init; }
    public UcxBand? Band => Enum.IsDefined(typeof(UcxBand), BandCode) ? (UcxBand)BandCode : null;
    public string BandDisplay => UcxDisplayFormat.Band(BandCode);

    public required byte ModeCode { get; init; }
    public UcxMode? Mode => Enum.IsDefined(typeof(UcxMode), ModeCode) ? (UcxMode)ModeCode : null;
    public string ModeDisplay => UcxDisplayFormat.Mode(ModeCode);

    public required string Qth { get; init; }
    public required string Name { get; init; }
    public required string Remarks { get; init; }

    public required byte Status151Raw { get; init; }
    public UcxPrintMark PrintMark => (UcxPrintMark)(Status151Raw & 0x03);
    public bool PaperQslSent => (Status151Raw & 0x08) != 0;
    public bool LotwUploaded => (Status151Raw & 0x10) != 0;
    public bool PaperQslReceived => (Status151Raw & 0x20) != 0;
    public bool EqslConfirmed => (Status151Raw & 0x40) != 0;
    public bool LotwConfirmed => (Status151Raw & 0x80) != 0;

    public required string Country { get; init; }
    public required byte ContinentCode { get; init; }
    public required byte Status161Raw { get; init; }
    public bool ClubLogConfirmed => (Status161Raw & 0x20) != 0;
    public bool ClubLogUploaded => (Status161Raw & 0x80) != 0;

    public required string UnknownString162 { get; init; }
    public required uint Unknown171 { get; init; }

    public required ulong RxFrequencyHz { get; init; }
    public string FrequencyDisplay => UcxDisplayFormat.Frequency(RxFrequencyHz);
    public required string Multiplier1 { get; init; }
    public required string Multiplier2 { get; init; }
    public required uint Points { get; init; }

    public required string Locator { get; init; }
    public required ushort IotaNumber { get; init; }
    public required byte IotaContinentCode { get; init; }
    public required string District { get; init; }
    public required byte CqZone { get; init; }
    public required byte ItuZone { get; init; }

    public required string Manager { get; init; }
    public required byte Unknown243 { get; init; }
    public required byte CbaRaw { get; init; }
    public bool Cba => CbaRaw != 0;
    public required byte ManagerOkRaw { get; init; }
    public bool ManagerOk => ManagerOkRaw != 0;

    public required double QslSendDateRaw { get; init; }
    public DateTime? QslSendDateTime =>
        PaperQslSent && QslSendDateRaw != 0 && double.IsFinite(QslSendDateRaw)
            ? UcxDateTime.Decode(QslSendDateRaw)
            : null;

    public required byte Unknown254 { get; init; }
    public required string UnknownString255 { get; init; }
    public required string UnknownString261 { get; init; }

    public required string Award1 { get; init; }
    public required string Count1 { get; init; }
    public required string Award2 { get; init; }
    public required string Count2 { get; init; }

    public required byte[] InternalMetadata320 { get; init; }
    public required byte[] Reserved342 { get; init; }
    public required ushort UnknownTrailer { get; init; }
}
