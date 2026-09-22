using UcxStats.Data.Database;

namespace UcxStats.Wpf.Awards;

public static class WaeEvaluator
{
    public static WaeEvaluation EvaluateMixed(IReadOnlyList<WaeQsoCandidate> qsos)
    {
        var grouped = new Dictionary<string, Dictionary<int, List<WaeQsoCandidate>>>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var qso in qsos)
        {
            var entity = WaeEntityResolver.Resolve(qso);
            if (entity is null)
                continue;

            if (!grouped.TryGetValue(entity.Code, out var bands))
            {
                bands = new Dictionary<int, List<WaeQsoCandidate>>();
                grouped[entity.Code] = bands;
            }

            if (!bands.TryGetValue(qso.BandCode, out var list))
            {
                list = [];
                bands[qso.BandCode] = list;
            }

            list.Add(qso);
        }

        var results = new List<WaeEntityResult>();
        foreach (var entity in WaeEntityResolver.Entities)
        {
            grouped.TryGetValue(entity.Code, out var bands);
            bands ??= new Dictionary<int, List<WaeQsoCandidate>>();

            var states = bands.ToDictionary(
                x => x.Key,
                x => new WaeBandState(
                    x.Key,
                    Worked: x.Value.Count > 0,
                    Confirmed: x.Value.Any(q => WaeEntityResolver.IsConfirmationValidForEntity(q, entity)),
                    Qsos: x.Value.OrderBy(q => q.QsoDate).ThenBy(q => q.StartTime).ToArray()));

            var confirmedBands = states.Values.Count(x => x.Confirmed);
            results.Add(new WaeEntityResult(entity, states, confirmedBands, Math.Min(5, confirmedBands)));
        }

        return new WaeEvaluation(
            results,
            ConfirmedCountries: results.Count(x => x.ConfirmedBandCount > 0),
            Points: results.Sum(x => x.Points),
            WorkedCountries: results.Count(x => x.Bands.Values.Any(b => b.Worked)));
    }

    public static IReadOnlyList<WaeClassProgress> GetClassProgress(WaeEvaluation evaluation) =>
    [
        Progress("WAE III", 40, 100, evaluation),
        Progress("WAE II", 50, 150, evaluation),
        Progress("WAE I", 60, 200, evaluation),
        Progress("WAE TOP", 70, 300, evaluation)
    ];

    private static WaeClassProgress Progress(
        string name, int countries, int points, WaeEvaluation evaluation) =>
        new(
            name,
            countries,
            points,
            evaluation.ConfirmedCountries >= countries && evaluation.Points >= points,
            Math.Max(0, countries - evaluation.ConfirmedCountries),
            Math.Max(0, points - evaluation.Points));
}
