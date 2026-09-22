using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using UcxStats.Core.Ucx;
using UcxStats.Data.Database;

namespace UcxStats.Wpf;

public partial class DclDiagnosticsWindow : Window
{
    private readonly IReadOnlyList<DclDiagnosticEntry> _entries;

    public DclDiagnosticsWindow(DclImportResult result)
    {
        InitializeComponent();
        _entries = result.Diagnostics;
        DiagnosticGrid.ItemsSource = _entries;
        SummaryText.Text =
            $"{result.Unmatched:N0} nicht gefunden · {result.Ambiguous:N0} manuell zu prüfen · " +
            $"{result.Matched:N0} eindeutig zugeordnet";

        if (_entries.Count > 0)
            DiagnosticGrid.SelectedIndex = 0;
    }

    private void DiagnosticGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DiagnosticGrid.SelectedItem is not DclDiagnosticEntry entry)
        {
            CandidateGrid.ItemsSource = null;
            return;
        }

        CandidateGrid.ItemsSource = entry.Candidates.Select(x => new
        {
            x.Id,
            x.QsoDate,
            x.StartTime,
            x.CallSign,
            BandDisplay = UcxDisplayFormat.Band((byte)x.BandCode),
            ModeDisplay = UcxDisplayFormat.Mode((byte)x.ModeCode),
            Source = Path.GetFileName(x.SourceFile)
        }).ToArray();
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "DCL-Importdiagnose exportieren",
            Filter = "CSV-Datei (*.csv)|*.csv",
            DefaultExt = ".csv",
            FileName = "UcxStats_DCL_Importdiagnose.csv"
        };
        if (dialog.ShowDialog(this) != true)
            return;

        var sb = new StringBuilder();
        sb.AppendLine("Typ;Ursache;Datum;UTC;Call;Band;Mode;DCL;DXCC;ADIF2;Station;MyLocator;Kandidaten");
        foreach (var x in _entries)
        {
            var candidates = string.Join(" | ", x.Candidates.Select(c =>
                $"{c.QsoDate} {c.StartTime} {c.CallSign} {UcxDisplayFormat.Band((byte)c.BandCode)} {UcxDisplayFormat.Mode((byte)c.ModeCode)} {Path.GetFileName(c.SourceFile)}"));
            sb.AppendLine(string.Join(";",
                Csv(x.Kind), Csv(x.Reason), Csv(x.QsoDate), Csv(x.TimeOn), Csv(x.CallSign),
                Csv(x.Band), Csv(x.Mode), Csv(x.Status), Csv(x.Dxcc), Csv(x.Adif2),
                Csv(x.StationCallSign), Csv(x.MyGridSquare), Csv(candidates)));
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));
    }

    private static string Csv(string value) =>
        "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";
}
