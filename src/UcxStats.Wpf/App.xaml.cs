using System.Windows;
using UcxStats.Map;

namespace UcxStats.Wpf;

public partial class App : Application
{
    public static MapSettingsService MapSettings { get; } = new();
}
