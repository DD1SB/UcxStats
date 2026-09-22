using System.Globalization;
using System.Windows.Data;

namespace UcxStats.Wpf.Awards;

public sealed class WaeBandStateConverter : IValueConverter
{
    private readonly int _bandCode;

    public WaeBandStateConverter(int bandCode)
    {
        _bandCode = bandCode;
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not WaeEntityResult result ||
            !result.Bands.TryGetValue(_bandCode, out var state))
            return string.Empty;

        return state.Confirmed ? "✓" : state.Worked ? "○" : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
