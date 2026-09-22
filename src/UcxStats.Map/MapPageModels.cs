namespace UcxStats.Map;

public sealed record MapTileDefinition(
    string ProviderName,
    string UrlTemplate,
    string Attribution,
    int MaxZoom);

public sealed record MapDxccPageOptions(
    bool GeometryAvailable,
    bool EnabledByDefault,
    string? GeometryUrl,
    DxccMapStatusPayload Status,
    string Diagnostic)
{
    public static MapDxccPageOptions Unavailable(string diagnostic) => new(
        false,
        false,
        null,
        DxccMapPayloadFactory.Unavailable(diagnostic),
        diagnostic);
}
