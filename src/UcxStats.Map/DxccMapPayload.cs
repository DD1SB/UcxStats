using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using UcxStats.Core.Ucx;
using UcxStats.Data.Dxcc;

namespace UcxStats.Map;

public sealed record DxccMapBandStatus(
    int EntityId,
    int BandCode,
    string Band,
    int QsoCount,
    int OtherConfirmedCount,
    int DxccCreditedCount,
    DxccOverlayStatus Status);

public sealed record DxccMapStatusPayload(
    bool Available,
    IReadOnlyList<DxccMapBandStatus> Bands,
    int UnresolvedQsoCount,
    int ExplicitNonDxccQsoCount,
    string? Error = null);

public static class DxccMapPayloadFactory
{
    public static DxccMapStatusPayload Create(
        DxccStatusSnapshot snapshot,
        IDxccEntityCatalog? catalog = null)
    {
        catalog ??= DxccEntityCatalog.ReleasedAdif317;
        return new(
            true,
            snapshot.Bands.Select(x => new DxccMapBandStatus(
                x.EntityId,
                x.BandCode,
                UcxDisplayFormat.Band((byte)x.BandCode),
                x.QsoCount,
                x.EqslOrClubLogConfirmedCount,
                x.DxccCreditedQsoCount,
                DxccOverlayStatusEvaluator.Evaluate(
                    catalog.GetStatus(x.EntityId), [x], [x.BandCode]))).ToArray(),
            snapshot.UnresolvedQsoCount,
            snapshot.ExplicitNonDxccQsoCount);
    }

    public static DxccMapStatusPayload Unavailable(string error) =>
        new(false, [], 0, 0, error);

    public static string Serialize(DxccMapStatusPayload payload) => JsonSerializer.Serialize(
        payload,
        new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Default,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        });
}

public static class DxccMapGeoJsonSerializer
{
    public static string Serialize(
        DxccGeoJsonLoadResult loadResult,
        IDxccEntityCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(loadResult);
        if (!loadResult.IsValid)
            throw new ArgumentException("Only successfully validated DXCC GeoJSON can be staged.", nameof(loadResult));

        catalog ??= DxccEntityCatalog.ReleasedAdif317;
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "FeatureCollection");
            writer.WritePropertyName("features");
            writer.WriteStartArray();
            foreach (var feature in loadResult.Features)
                WriteFeature(writer, feature, catalog);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteFeature(
        Utf8JsonWriter writer,
        DxccGeometryFeature feature,
        IDxccEntityCatalog catalog)
    {
        var entry = feature.EntityId is { } id ? catalog.Find(id) : null;
        writer.WriteStartObject();
        writer.WriteString("type", "Feature");
        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        if (feature.EntityId is { } entityId)
            writer.WriteNumber("entityId", entityId);
        else
            writer.WriteNull("entityId");
        WriteNullableString(writer, "name", entry?.Name ?? feature.Name);
        WriteNullableString(writer, "prefix", feature.Prefix);
        writer.WriteString("catalogStatus", (entry?.Status ?? DxccCatalogEntityStatus.Unknown).ToString());
        writer.WriteString("featureKind", feature.Kind.ToString());
        writer.WriteEndObject();
        writer.WritePropertyName("geometry");
        WriteGeometry(writer, feature.Geometry);
        writer.WriteEndObject();
    }

    private static void WriteNullableString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null) writer.WriteNull(name);
        else writer.WriteString(name, value);
    }

    private static void WriteGeometry(Utf8JsonWriter writer, DxccGeometry geometry)
    {
        writer.WriteStartObject();
        writer.WriteString("type", geometry.Type.ToString());
        writer.WritePropertyName("coordinates");
        writer.WriteStartArray();
        if (geometry.Type == DxccGeometryType.Polygon)
        {
            WritePolygonRings(writer, geometry.Polygons[0]);
        }
        else
        {
            foreach (var polygon in geometry.Polygons)
            {
                writer.WriteStartArray();
                WritePolygonRings(writer, polygon);
                writer.WriteEndArray();
            }
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WritePolygonRings(Utf8JsonWriter writer, DxccPolygonGeometry polygon)
    {
        foreach (var ring in polygon.Rings)
        {
            writer.WriteStartArray();
            foreach (var position in ring.Positions)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(position.Longitude);
                writer.WriteNumberValue(position.Latitude);
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
        }
    }
}
