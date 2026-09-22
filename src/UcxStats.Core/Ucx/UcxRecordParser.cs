using System.Buffers.Binary;

namespace UcxStats.Core.Ucx;

public sealed class UcxRecordParser
{
    private readonly UcxParseOptions _options;

    public UcxRecordParser(UcxParseOptions? options = null)
    {
        _options = options ?? new UcxParseOptions();
    }

    public UcxQsoRecord Parse(ReadOnlySpan<byte> record, long recordIndex = -1)
    {
        if (record.Length != UcxConstants.RecordSize)
            throw new ArgumentException($"A UCX record must be exactly {UcxConstants.RecordSize} bytes.", nameof(record));

        var year = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(3, 2));

        DateOnly date;
        TimeOnly startTime;
        TimeOnly endTime;
        try
        {
            date = new DateOnly(year, record[2], record[1]);
            startTime = new TimeOnly(record[6], record[5]);
            endTime = new TimeOnly(record[8], record[7]);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidDataException($"Record {recordIndex}: invalid date/time fields.", ex);
        }

        return new UcxQsoRecord
        {
            RecordIndex = recordIndex,
            RecordTypeRaw = record[0],
            Date = date,
            StartTime = startTime,
            EndTime = endTime,

            CallSign = ReadShortString(record, 9, 15, recordIndex),
            SentRst = ReadShortString(record, 25, 3, recordIndex),
            SentExchange = ReadShortString(record, 29, 19, recordIndex),
            ReceivedRst = ReadShortString(record, 49, 3, recordIndex),
            ReceivedExchange = ReadShortString(record, 53, 19, recordIndex),

            BandCode = record[73],
            ModeCode = record[74],
            Qth = ReadShortString(record, 75, 22, recordIndex),
            Name = ReadShortString(record, 98, 12, recordIndex),
            Remarks = ReadShortString(record, 111, 39, recordIndex),

            Status151Raw = record[151],
            Country = ReadShortString(record, 152, 7, recordIndex),
            ContinentCode = record[160],
            Status161Raw = record[161],
            UnknownString162 = ReadShortString(record, 162, 8, recordIndex),
            Unknown171 = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(171, 4)),

            RxFrequencyHz = BinaryPrimitives.ReadUInt64LittleEndian(record.Slice(175, 8)),
            Multiplier1 = ReadShortString(record, 183, 11, recordIndex),
            Multiplier2 = ReadShortString(record, 195, 11, recordIndex),
            Points = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(207, 4)),

            Locator = ReadShortString(record, 211, 8, recordIndex),
            IotaNumber = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(220, 2)),
            IotaContinentCode = record[222],
            District = ReadShortString(record, 223, 8, recordIndex),
            CqZone = record[232],
            ItuZone = record[233],

            Manager = ReadShortString(record, 234, 8, recordIndex),
            Unknown243 = record[243],
            CbaRaw = record[244],
            ManagerOkRaw = record[245],
            QslSendDateRaw = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(record.Slice(246, 8))),

            Unknown254 = record[254],
            UnknownString255 = ReadShortString(record, 255, 5, recordIndex),
            UnknownString261 = ReadShortString(record, 261, 10, recordIndex),

            Award1 = ReadShortString(record, 272, 11, recordIndex),
            Count1 = ReadShortString(record, 284, 11, recordIndex),
            Award2 = ReadShortString(record, 296, 11, recordIndex),
            Count2 = ReadShortString(record, 308, 11, recordIndex),

            InternalMetadata320 = record.Slice(320, 22).ToArray(),
            Reserved342 = record.Slice(342, 20).ToArray(),
            UnknownTrailer = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(362, 2))
        };
    }

    private string ReadShortString(ReadOnlySpan<byte> record, int offset, int capacity, long recordIndex)
    {
        var length = record[offset];
        if (length > capacity)
        {
            if (_options.StrictStringLengths)
                throw new InvalidDataException($"Record {recordIndex}: string at offset {offset} claims length {length}, capacity is {capacity}.");
            length = (byte)capacity;
        }

        return _options.TextEncoding.GetString(record.Slice(offset + 1, length)).TrimEnd('\0');
    }
}
