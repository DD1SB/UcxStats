using System.IO;
using System.Windows;
using UcxStats.Core.Ucx;
using UcxStats.Data.Database;
using UcxStats.Wpf.Awards;

namespace UcxStats.Wpf;

public partial class WaeQsoDetailWindow : Window
{
    public WaeQsoDetailWindow(
        WaeEntity entity,
        int bandCode,
        IReadOnlyList<WaeQsoCandidate> qsos)
    {
        InitializeComponent();
        HeaderText.Text = $"{entity.Code} · {entity.Name} · {UcxDisplayFormat.Band((byte)bandCode)}";
        QsoGrid.ItemsSource = qsos.Select(q => new
        {
            q.QsoDate,
            q.StartTime,
            q.CallSign,
            UcxCountry = q.Country,
            ResolvedWae = WaeEntityResolver.Resolve(q)?.Code ?? "—",
            ModeDisplay = UcxDisplayFormat.Mode((byte)q.ModeCode),
            Confirmation = WaeEntityResolver.IsConfirmationValidForEntity(q, entity)
                ? q.ConfirmationText
                : q.Confirmed
                    ? $"nicht für {entity.Code} gewertet ({q.ConfirmationText})"
                    : "nicht bestätigt",
            q.Locator,
            Source = Path.GetFileName(q.SourceFile)
        }).ToArray();
    }
}
