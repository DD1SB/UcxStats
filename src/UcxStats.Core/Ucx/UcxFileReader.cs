using System.Security.Cryptography;

namespace UcxStats.Core.Ucx;

public sealed class UcxFileReader
{
    private readonly UcxRecordParser _parser;

    public UcxFileReader(UcxRecordParser? parser = null)
    {
        _parser = parser ?? new UcxRecordParser();
    }

    public IEnumerable<UcxQsoRecord> Read(string path) => ReadWithMetadata(path).Select(x => x.Qso);

    public IEnumerable<UcxSourceRecord> ReadWithMetadata(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        if (stream.Length % UcxConstants.RecordSize != 0)
        {
            throw new InvalidDataException(
                $"UCX file size {stream.Length} is not divisible by the expected record size {UcxConstants.RecordSize}.");
        }

        var buffer = new byte[UcxConstants.RecordSize];
        var count = stream.Length / UcxConstants.RecordSize;

        for (long index = 0; index < count; index++)
        {
            stream.ReadExactly(buffer);
            yield return new UcxSourceRecord
            {
                Qso = _parser.Parse(buffer, index),
                RecordHash = Convert.ToHexString(SHA256.HashData(buffer))
            };
        }
    }

    public UcxQsoRecord ReadRecord(string path, long recordIndex)
    {
        if (recordIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(recordIndex));

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var offset = checked(recordIndex * UcxConstants.RecordSize);
        if (offset + UcxConstants.RecordSize > stream.Length)
            throw new ArgumentOutOfRangeException(nameof(recordIndex), "Record index is beyond the end of the file.");

        stream.Seek(offset, SeekOrigin.Begin);
        var buffer = new byte[UcxConstants.RecordSize];
        stream.ReadExactly(buffer);
        return _parser.Parse(buffer, recordIndex);
    }
}
