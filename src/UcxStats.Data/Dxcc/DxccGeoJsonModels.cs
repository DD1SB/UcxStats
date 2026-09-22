namespace UcxStats.Data.Dxcc;

public enum DxccGeoJsonDiagnosticSeverity
{
    Information,
    Warning,
    Error
}

public sealed record DxccGeoJsonDiagnostic(
    DxccGeoJsonDiagnosticSeverity Severity,
    string Code,
    string Message,
    int? FeatureIndex = null);

public enum DxccGeometryType
{
    Polygon,
    MultiPolygon
}

public enum DxccGeometryFeatureKind
{
    DxccEntity,
    NonDxcc,
    Unresolved
}

public readonly record struct DxccPosition(double Longitude, double Latitude);

public sealed record DxccLinearRing(IReadOnlyList<DxccPosition> Positions);

public sealed record DxccPolygonGeometry(IReadOnlyList<DxccLinearRing> Rings);

public sealed record DxccGeometry(
    DxccGeometryType Type,
    IReadOnlyList<DxccPolygonGeometry> Polygons);

public sealed record DxccGeometryFeature(
    int SourceIndex,
    int? EntityId,
    string? Name,
    string? Prefix,
    DxccGeometryFeatureKind Kind,
    DxccGeometry Geometry,
    string GeometryFingerprint,
    string PropertiesFingerprint);

public sealed record DxccGeoJsonLoadResult(
    bool IsAvailable,
    bool IsValid,
    bool IsKnownAmazingProducerSource,
    string? Sha256,
    IReadOnlyList<DxccGeometryFeature> Features,
    IReadOnlyList<DxccGeoJsonDiagnostic> Diagnostics,
    int CoordinateCount,
    int UniqueDxccEntityIdCount,
    int CurrentCatalogEntityIdCount,
    int DeletedCatalogEntityIdCount,
    int UnknownCatalogEntityIdCount)
{
    public IReadOnlyList<int> CurrentCatalogEntityIdsMissingGeometry { get; init; } = [];
    public IReadOnlyList<int> GeometryEntityIdsNotRecognizedByCatalog { get; init; } = [];
}

public sealed record DxccGeoJsonLimits(
    long MaxFileBytes = 10 * 1024 * 1024,
    int MaxFeatures = 5_000,
    int MaxCoordinatePositions = 500_000,
    int MaxJsonDepth = 32);
