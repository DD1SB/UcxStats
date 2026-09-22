# UcxLog `.UCX` record format (unofficial, reverse engineered)

> Status: working specification derived from examined UcxLog normal and contest logs plus CSV exports. This is **not** an official UcxLog format description.

## Container

The examined `.UCX` files are flat arrays of fixed-size QSO records:

```text
record_size = 364 bytes
record_count = file_size / 364
record_offset(n) = n * 364
```

No global file header was observed in the examined files. A file whose size is not divisible by 364 should be treated as unexpected/corrupt/unsupported by the current reader.

All identified multi-byte integer values are little-endian.

Most fixed text slots use a Pascal/Delphi-like short-string representation:

```text
[length byte][fixed-capacity character area]
```

The reader validates that the stored length does not exceed the slot capacity.

## Confidence markers

- **Confirmed**: directly matched across records and/or CSV/UcxLog UI.
- **Structural**: fixed slot/structure is clear, semantic meaning not fully proven.
- **Unknown**: intentionally not interpreted.

## Record map

| Offset | Size | Encoding | Meaning | Confidence |
|---:|---:|---|---|---|
| 0 | 1 | byte | 1 in all examined normal-log records, 0 in examined contest records | Structural |
| 1 | 1 | byte | day | Confirmed |
| 2 | 1 | byte | month | Confirmed |
| 3 | 2 | UInt16 LE | year | Confirmed |
| 5 | 1 | byte | start minute | Confirmed |
| 6 | 1 | byte | start hour | Confirmed |
| 7 | 1 | byte | end minute | Confirmed |
| 8 | 1 | byte | end hour | Confirmed |
| 9 | 16 | ShortString[15] | callsign | Confirmed |
| 25 | 4 | ShortString[3] | sent RST | Confirmed |
| 29 | 20 | ShortString[19] | sent exchange | Confirmed |
| 49 | 4 | ShortString[3] | received RST | Confirmed |
| 53 | 20 | ShortString[19] | received exchange | Confirmed |
| 73 | 1 | enum | band code | Confirmed |
| 74 | 1 | enum | mode code | Confirmed |
| 75 | 23 | ShortString[22] | QTH | Confirmed |
| 98 | 13 | ShortString[12] | name | Confirmed |
| 111 | 40 | ShortString[39] | remarks | Confirmed |
| 151 | 1 | bitfield | print/QSL status #1 | Partially confirmed |
| 152 | 8 | ShortString[7] | country / DXCC-style country prefix used by UcxLog | Confirmed |
| 160 | 1 | enum | continent | Confirmed for EU/AS/AF/NA/SA/OC |
| 161 | 1 | bitfield | status #2 | Partially confirmed |
| 162 | 9 | ShortString[8] | unknown string | Structural |
| 171 | 4 | bytes | unknown | Unknown |
| 175 | 8 | UInt64 LE | exact frequency in Hz | Confirmed |
| 183 | 12 | ShortString[11] | contest multiplier 1 | Confirmed |
| 195 | 12 | ShortString[11] | contest multiplier 2 | Structural/confirmed slot |
| 207 | 4 | UInt32 LE | contest QSO points | Confirmed |
| 211 | 9 | ShortString[8] | Maidenhead locator | Confirmed |
| 220 | 2 | UInt16 LE | IOTA number | Confirmed |
| 222 | 1 | enum | IOTA continent | Confirmed |
| 223 | 9 | ShortString[8] | district | Confirmed |
| 232 | 1 | byte | CQ zone | Confirmed |
| 233 | 1 | byte | ITU zone | Confirmed |
| 234 | 9 | ShortString[8] | QSL manager / via | Confirmed |
| 243 | 1 | byte | unknown | Unknown |
| 244 | 1 | boolean | CBA - QSL direct / sort forward for printing | Confirmed |
| 245 | 1 | boolean | Ok - manager verified/safe | Confirmed |
| 246 | 8 | IEEE-754 double LE | Delphi TDateTime paper-QSL send timestamp | Confirmed |
| 254 | 1 | byte | unknown | Unknown |
| 255 | 6 | ShortString[5] | unknown string | Structural |
| 261 | 11 | ShortString[10] | unknown string | Structural |
| 272 | 12 | ShortString[11] | Award 1 abbreviation | Confirmed |
| 284 | 12 | ShortString[11] | Count 1 / arbitrary award value text | Confirmed |
| 296 | 12 | ShortString[11] | Award 2 abbreviation | Confirmed structurally/semantically from UcxLog help |
| 308 | 12 | ShortString[11] | Count 2 / arbitrary award value text | Confirmed structurally/semantically from UcxLog help |
| 320 | 22 | bytes | internal metadata | Unknown |
| 342 | 20 | bytes | zero in all examined samples | Unknown/reserved |
| 362 | 2 | UInt16 LE | changing trailer/check value; algorithm unknown | Unknown |

## Band code at offset 73

Observed/derived mapping:

| Code | Band | Evidence |
|---:|---|---|
| 0 | 160 m | observed |
| 1 | 80 m | observed |
| 2 | 40 m | observed |
| 3 | 30 m | derived from UcxLog UI order |
| 4 | 20 m | observed |
| 5 | 17 m | observed |
| 6 | 15 m | observed |
| 7 | 12 m | observed |
| 8 | 10 m | observed |
| 9 | 6 m | observed |
| 10 | 2 m | observed |
| 11 | 70 cm | derived from UI order |
| 12 | 23 cm | derived from UI order |
| 13 | 13 cm | derived from UI order |
| 14 | 3400 MHz | derived from UI order |
| 15 | 5600 MHz | derived from UI order |
| 16 | 10 GHz | observed (microwave/SAT record) |

UcxLog also offers `unbestimmt` and `Rest`; their numeric values have not been proven.

The raw numeric band code is therefore retained even when a friendly enum name is known.

## Mode code at offset 74

Controlled test records established:

| Code | Mode |
|---:|---|
| 0 | CW |
| 1 | SSB |
| 2 | RTTY |
| 3 | AM |
| 4 | FM |
| 5 | PSK31 |
| 7 | PKT |
| 12 | SAT |
| 30 | FT8 |

Other UcxLog modes are not yet mapped. The raw mode code is always retained.

## QSL/print status byte at offset 151

Confirmed interpretation:

```text
bits 0..1  print mark
             0 = empty
             1 = P  paper QSL should be printed/sent
             2 = n  do not send this time
             3 = N  never send

bit 3  0x08 = paper QSL sent
bit 4  0x10 = unknown (do not label as LoTW upload without a hard proof)
bit 5  0x20 = paper QSL received
bit 6  0x40 = eQSL confirmed
bit 7  0x80 = LoTW confirmed
```

Bit 2 (`0x04`) remains unknown.

## Status byte at offset 161

```text
0x20 = ClubLog confirmed     [confirmed]
0x80 = unknown               [intentionally left unknown]
```

No QRZ/ClubLog upload interpretation is currently committed to the format specification.

## QSL send timestamp at 246

The 8 bytes decode as a little-endian IEEE-754 double compatible with Delphi `TDateTime`, using 1899-12-30 as day zero for the modern positive values observed.

Important: a stale non-zero timestamp can remain in a record even if no paper QSL is currently marked as sent. Treat the timestamp as valid only when `(status151 & 0x08) != 0`.

## IOTA encoding

```text
220..221 = UInt16 LE numeric part
222      = continent enum
```

Observed IOTA continent enum:

```text
0 EU
1 AS
2 AF
3 NA
4 SA
5 OC
6 AN
```

## QSO continent at offset 160

Observed:

```text
0 unknown/not set
1 EU
2 AS
3 AF
4 NA
5 SA
6 OC
```

Antarctica was not present in the analyzed samples and is intentionally not guessed.

## Contest data philosophy

The UCX record stores generic strings and values rather than contest-specific semantics:

- Sent RST
- Sent exchange
- Received RST
- Received exchange
- Multiplier 1
- Multiplier 2
- Points

For example, an exchange may contain a serial number, locator, zone, district, HQ abbreviation or another contest-defined value. The parser should not reinterpret it.

Contest QSOs must remain normal statistical QSOs; the contest marker/fields are additional metadata, not a reason to exclude them from band/DXCC/locator/award statistics.

## QTC files

A tested `.QTC` file was a separate fixed-width line-oriented text format and is not part of the 364-byte UCX record. QTC support is deliberately outside the initial parser scope.


### Bestätigte Online-Statusbits
- Offset 151, `0x10`: zu LoTW hochgeladen
- Offset 151, `0x40`: eQSL bestätigt
- Offset 151, `0x80`: LoTW bestätigt
- Offset 161, `0x20`: ClubLog bestätigt
- Offset 161, `0x80`: zu ClubLog hochgeladen

QRZ.com wird von UcxLog als Uploadziel genutzt; im 364-Byte-QSO-Record wurde bislang kein persistenter QRZ-Uploadstatus nachgewiesen.

### Locator precision
UcxLog accepts at most 8 Maidenhead characters. The UCX record stores the locator length at offset 211 and up to 8 characters at offsets 212-219. An 8-character test locator (`JO40FG12`) was stored without truncation.


### Frequency / split operation

The 64-bit frequency field at offset 175 is modeled as `RxFrequencyHz`.
Controlled tests with simplex and split +5/+10/+15/+20 kHz showed the same persisted RX frequency and no reproducible TX frequency, split offset or split flag in the 364-byte QSO record. UcxStats therefore does not invent TX/split data that UCX does not persist.
