using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using UcxStats.Data.Database;

namespace UcxStats.Wpf;

public partial class QueryWindow : Window
{
    private readonly IQsoRepository _repository;
    private readonly Window _mainOwner;
    private QueryResult? _lastResult;

    public QueryWindow(IQsoRepository repository, Window owner)
    {
        _repository = repository;
        _mainOwner = owner;
        InitializeComponent();
        Owner = owner;
        SqlBox.Text = """
SELECT
    q.id AS qso_id,
    q.qso_date,
    q.start_time,
    q.callsign,
    q.band_code,
    q.mode_code,
    q.rx_frequency_hz,
    q.country,
    q.locator,
    q.points,
    q.award1,
    q.count1,
    q.award2,
    q.count2,
    s.file_name AS source
FROM qso q
JOIN source_file s ON s.full_path = q.source_file
ORDER BY q.qso_date DESC, q.start_time DESC
LIMIT 500;
""";
    }

    private async void Execute_Click(object sender, RoutedEventArgs e) => await ExecuteAsync();

    private async Task ExecuteAsync()
    {
        try
        {
            StatusText.Text = "Abfrage läuft…";
            ResultGrid.ItemsSource = null;
            _lastResult = await _repository.ExecuteReadOnlyQueryAsync(SqlBox.Text);

            var table = new DataTable();
            foreach (var column in _lastResult.Columns)
                table.Columns.Add(column, typeof(object));
            foreach (var sourceRow in _lastResult.Rows)
            {
                var row = table.NewRow();
                for (var i = 0; i < sourceRow.Count; i++)
                    row[i] = sourceRow[i] ?? DBNull.Value;
                table.Rows.Add(row);
            }

            ResultGrid.ItemsSource = table.DefaultView;
            StatusText.Text = $"{_lastResult.Rows.Count:N0} Ergebniszeilen · {_lastResult.Columns.Count:N0} Spalten";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Abfrage fehlgeschlagen.";
            MessageBox.Show(this, ex.Message, "SQL-Abfrage", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SqlBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            _ = ExecuteAsync();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "SQL-Abfrage (*.sql)|*.sql", DefaultExt = ".sql" };
        if (dialog.ShowDialog(this) == true)
            File.WriteAllText(dialog.FileName, SqlBox.Text);
    }

    private void Load_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "SQL-Abfrage (*.sql)|*.sql" };
        if (dialog.ShowDialog(this) == true)
            SqlBox.Text = File.ReadAllText(dialog.FileName);
    }

    private async void ToMap_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult is null)
        {
            MessageBox.Show(this, "Bitte zuerst eine Abfrage ausführen.", "Karte");
            return;
        }

        var idIndex = -1;
        for (var i = 0; i < _lastResult.Columns.Count; i++)
            if (string.Equals(_lastResult.Columns[i], "qso_id", StringComparison.OrdinalIgnoreCase))
                idIndex = i;

        if (idIndex < 0)
        {
            MessageBox.Show(this, "Die Abfrage muss qso.id AS qso_id zurückgeben.", "Karte",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var ids = _lastResult.Rows
            .Select(r => r[idIndex])
            .Where(v => v is not null)
            .Select(v => Convert.ToInt64(v))
            .Distinct()
            .ToArray();

        if (ids.Length == 0) return;
        var scope = QsoScope.ForQsoIds(ids);
        var points = await _repository.GetMapQsoPointsAsync(scope);
        if (points.Count == 0)
        {
            MessageBox.Show(this, "Die Ergebnismenge enthält keine kartierbaren QSOs.", "Karte");
            return;
        }

        new MapWindow(_repository, scope, points, $"SQL-Ergebnis · {points.Count:N0} kartierbare QSOs")
            { Owner = _mainOwner }.Show();
    }
}
