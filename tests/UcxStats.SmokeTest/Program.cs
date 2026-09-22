using System.Buffers.Binary;
using System.Text;
using UcxStats.Core.Ucx;

var recordData = new byte[UcxConstants.RecordSize];
recordData[0] = 1;
recordData[1] = 12; // day
recordData[2] = 8;  // month
BinaryPrimitives.WriteUInt16LittleEndian(recordData.AsSpan(3, 2), 2026);
recordData[5] = 34; // start minute
recordData[6] = 21; // start hour
recordData[7] = 35; // end minute
recordData[8] = 21; // end hour
WriteShort(recordData, 9, 15, "DD1SB");
WriteShort(recordData, 25, 3, "59");
WriteShort(recordData, 49, 3, "59");
recordData[73] = (byte)UcxBand.B20m;
recordData[74] = (byte)UcxMode.Ssb;
WriteShort(recordData, 111, 39, "synthetic smoke test");
recordData[151] = 0x08 | 0x40 | 0x80;
WriteShort(recordData, 152, 7, "DL");
recordData[160] = 1;
recordData[161] = 0x20;
BinaryPrimitives.WriteUInt64LittleEndian(recordData.AsSpan(175, 8), 14_250_000UL);
WriteShort(recordData, 211, 8, "JO00AA");
recordData[232] = 14;
recordData[233] = 28;

var qso = new UcxRecordParser().Parse(recordData, 0);

Require(qso.CallSign == "DD1SB", "Callsign");
Require(qso.Band == UcxBand.B20m, "Band");
Require(qso.Mode == UcxMode.Ssb, "Mode");
Require(qso.RxFrequencyHz == 14_250_000UL, "Frequency");
Require(qso.PaperQslSent, "Paper QSL sent");
Require(qso.EqslConfirmed, "eQSL");
Require(qso.LotwConfirmed, "LoTW");
Require(qso.ClubLogConfirmed, "ClubLog");

Console.WriteLine("UcxStats smoke test passed.");

static void WriteShort(byte[] target, int offset, int capacity, string value)
{
    var bytes = Encoding.Latin1.GetBytes(value);
    if (bytes.Length > capacity) throw new ArgumentException("Test string too long.");
    target[offset] = (byte)bytes.Length;
    bytes.CopyTo(target, offset + 1);
}

static void Require(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException($"Smoke test failed: {name}");
}
