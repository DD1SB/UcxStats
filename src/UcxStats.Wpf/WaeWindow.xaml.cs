using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using UcxStats.Core.Ucx;
using UcxStats.Data.Database;
using UcxStats.Wpf.Awards;

namespace UcxStats.Wpf;

public partial class WaeWindow : Window
{
    private readonly IQsoRepository _repository;
    private WaeEvaluation? _evaluation;

    private static readonly int[] BandCodes = Enumerable.Range(0, 17).ToArray();

    public ObservableCollection<WaeGridRow> Rows { get; } = [];

    public WaeWindow(IQsoRepository repository)
    {
        _repository = repository;
        InitializeComponent();
        WaeGrid.ItemsSource = Rows;
        AddBandColumns();
        Loaded += async (_, _) => await RefreshAsync();
    }

    private void AddBandColumns()
    {
        foreach (var bandCode in BandCodes)
        {
            var code = bandCode;
            WaeGrid.Columns.Add(new DataGridTextColumn
            {
                Header = UcxDisplayFormat.Band((byte)code),
                Width = 62,
                Binding = new Binding($"Result")
                {
                    Converter = new WaeBandStateConverter(code)
                }
            });
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            IsEnabled = false;
            SummaryText.Text = "WAE wird ausgewertet…";
            var qsos = await _repository.GetWaeQsoCandidatesAsync();
            _evaluation = WaeEvaluator.EvaluateMixed(qsos);

            Rows.Clear();
            foreach (var result in _evaluation.Entities)
                Rows.Add(new WaeGridRow { Result = result });

            SummaryText.Text =
                $"{_evaluation.ConfirmedCountries:N0} bestätigte WAE-Länder · " +
                $"{_evaluation.Points:N0} Punkte · {_evaluation.WorkedCountries:N0} Länder gearbeitet";

            ClassProgressList.ItemsSource = WaeEvaluator.GetClassProgress(_evaluation)
                .Select(x => new
                {
                    x.Name,
                    StatusText = x.Achieved
                        ? $"✓ erreicht ({x.RequiredCountries} Länder / {x.RequiredPoints} Punkte)"
                        : $"fehlen: {x.MissingCountries} Länder / {x.MissingPoints} Punkte"
                })
                .ToArray();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "WAE Mixed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void WaeGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (WaeGrid.CurrentCell.Item is not WaeGridRow row)
            return;

        var columnIndex = WaeGrid.CurrentCell.Column?.DisplayIndex ?? -1;
        // 0 Code, 1 Land, 2 Punkte, then one column per Ucx band code.
        var bandIndex = columnIndex - 3;
        if (bandIndex < 0 || bandIndex >= BandCodes.Length)
            return;

        var bandCode = BandCodes[bandIndex];
        var qsos = row.QsosForBand(bandCode);
        if (qsos.Count == 0)
            return;

        new WaeQsoDetailWindow(row.Result.Entity, bandCode, qsos) { Owner = this }.Show();
    }
}
