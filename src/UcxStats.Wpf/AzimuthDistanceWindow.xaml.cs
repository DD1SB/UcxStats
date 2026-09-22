using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using UcxStats.Core.Ucx;
using UcxStats.Data.Database;

namespace UcxStats.Wpf;

public partial class AzimuthDistanceWindow : Window
{
    private readonly IReadOnlyList<MapQsoPoint> _points;
    private const double EarthRadiusKm = 6371.0088;
    private static readonly double EarthCircumferenceKm = 2.0 * Math.PI * EarthRadiusKm;
    private static readonly string[] Palette =
    [
        "#6a3d9a","#1f78b4","#33a02c","#b15928","#e31a1c","#ff7f00",
        "#f1c40f","#8e44ad","#00a6a6","#e84393","#2d3436","#636e72",
        "#0984e3","#00b894","#d63031","#e17055","#6c5ce7"
    ];

    public AzimuthDistanceWindow(IReadOnlyList<MapQsoPoint> points, string title)
    {
        InitializeComponent();
        _points = points.Where(x => x.DistanceKm.HasValue && x.BearingDeg.HasValue).ToList();
        HeaderText.Text = title;
        UpdateStatus();
        Loaded += (_, _) => Draw();
    }

    private void PlotCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => Draw();
    private void PathModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        UpdateStatus();
        Draw();
    }

    private string PathMode => (PathModeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "short";

    private void UpdateStatus()
    {
        var mode = PathMode switch { "long" => "Longpath", "both" => "Short- und Longpath", _ => "Shortpath" };
        StatusText.Text = $"{_points.Count:N0} QSOs mit Entfernung und Azimut · {mode}.";
    }

    private static Brush BandBrush(int bandCode)
        => (Brush)new BrushConverter().ConvertFromString(Palette[Math.Abs(bandCode) % Palette.Length])!;

    private void Draw()
    {
        if (!IsLoaded || PlotCanvas.ActualWidth < 50 || PlotCanvas.ActualHeight < 50) return;
        PlotCanvas.Children.Clear();

        var mode = PathMode;
        var cx = PlotCanvas.ActualWidth / 2.0;
        var cy = PlotCanvas.ActualHeight / 2.0;
        var radius = Math.Max(20, Math.Min(PlotCanvas.ActualWidth, PlotCanvas.ActualHeight) / 2.0 - 45);

        double DisplayDistance(MapQsoPoint q, bool longPath)
            => longPath ? EarthCircumferenceKm - q.DistanceKm!.Value : q.DistanceKm!.Value;

        var maxDistance = _points.Count == 0 ? 1.0 : Math.Max(1.0,
            _points.Max(q => mode switch
            {
                "long" => DisplayDistance(q, true),
                "both" => Math.Max(DisplayDistance(q, false), DisplayDistance(q, true)),
                _ => DisplayDistance(q, false)
            }));

        for (var ring = 1; ring <= 4; ring++)
        {
            var r = radius * ring / 4.0;
            var circle = new Ellipse { Width = r * 2, Height = r * 2, Stroke = Brushes.LightGray, StrokeThickness = 1 };
            PlotCanvas.Children.Add(circle);
            Canvas.SetLeft(circle, cx - r);
            Canvas.SetTop(circle, cy - r);
            var label = new TextBlock { Text = $"{maxDistance * ring / 4.0:N0} km", Foreground = Brushes.Gray, FontSize = 11 };
            PlotCanvas.Children.Add(label);
            Canvas.SetLeft(label, cx + 4);
            Canvas.SetTop(label, cy - r + 2);
        }

        for (var degrees = 0; degrees < 360; degrees += 30)
        {
            var a = degrees * Math.PI / 180.0;
            PlotCanvas.Children.Add(new Line
            {
                X1 = cx, Y1 = cy,
                X2 = cx + radius * Math.Sin(a),
                Y2 = cy - radius * Math.Cos(a),
                Stroke = degrees % 90 == 0 ? Brushes.Silver : Brushes.Gainsboro,
                StrokeThickness = degrees % 90 == 0 ? 1.2 : 0.7
            });
        }

        AddLabel("N", cx - 5, cy - radius - 22);
        AddLabel("E", cx + radius + 12, cy - 8);
        AddLabel("S", cx - 5, cy + radius + 6);
        AddLabel("W", cx - radius - 22, cy - 8);

        foreach (var qso in _points)
        {
            if (mode is "short" or "both") AddQso(qso, false);
            if (mode is "long" or "both") AddQso(qso, true);
        }

        void AddQso(MapQsoPoint qso, bool longPath)
        {
            var distance = DisplayDistance(qso, longPath);
            var bearing = longPath ? (qso.BearingDeg!.Value + 180.0) % 360.0 : qso.BearingDeg!.Value;
            var a = bearing * Math.PI / 180.0;
            var r = radius * distance / maxDistance;
            var brush = BandBrush(qso.BandCode);
            var dot = new Ellipse
            {
                Width = longPath && mode == "both" ? 8 : 5,
                Height = longPath && mode == "both" ? 8 : 5,
                Fill = longPath && mode == "both" ? Brushes.Transparent : brush,
                Stroke = longPath && mode == "both" ? brush : Brushes.White,
                StrokeThickness = longPath && mode == "both" ? 1.6 : 0.4,
                ToolTip = $"{qso.CallSign} · {UcxDisplayFormat.Band((byte)qso.BandCode)} · {(longPath ? "Longpath" : "Shortpath")} · {distance:N1} km · {bearing:N0}°"
            };
            PlotCanvas.Children.Add(dot);
            Canvas.SetLeft(dot, cx + r * Math.Sin(a) - dot.Width / 2.0);
            Canvas.SetTop(dot, cy - r * Math.Cos(a) - dot.Height / 2.0);
        }

        void AddLabel(string text, double x, double y)
        {
            var block = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold };
            PlotCanvas.Children.Add(block);
            Canvas.SetLeft(block, x);
            Canvas.SetTop(block, y);
        }
    }
}
