using UcxStats.Core.Ucx;
using UcxStats.Data.Database;

namespace UcxStats.Wpf.Awards;

public sealed record WaeEntity(
    string Code,
    string Name,
    DateOnly? ValidFrom = null,
    DateOnly? ValidTo = null);

public sealed record WaeBandState(
    int BandCode,
    bool Worked,
    bool Confirmed,
    IReadOnlyList<WaeQsoCandidate> Qsos);

public sealed record WaeEntityResult(
    WaeEntity Entity,
    IReadOnlyDictionary<int, WaeBandState> Bands,
    int ConfirmedBandCount,
    int Points);

public sealed record WaeEvaluation(
    IReadOnlyList<WaeEntityResult> Entities,
    int ConfirmedCountries,
    int Points,
    int WorkedCountries);

public sealed record WaeClassProgress(
    string Name,
    int RequiredCountries,
    int RequiredPoints,
    bool Achieved,
    int MissingCountries,
    int MissingPoints);
