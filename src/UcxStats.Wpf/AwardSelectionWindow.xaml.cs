using System.Windows;
using System.Windows.Input;
using UcxStats.Data.Database;
using UcxStats.Wpf.Awards;

namespace UcxStats.Wpf;

public partial class AwardSelectionWindow : Window
{
    private readonly IQsoRepository _repository;

    public AwardSelectionWindow(IQsoRepository repository)
    {
        _repository = repository;
        InitializeComponent();
        AwardList.ItemsSource = AwardCatalog.Entries.Where(x => x.Available);
        if (AwardList.Items.Count > 0)
            AwardList.SelectedIndex = 0;
    }

    private void Open_Click(object sender, RoutedEventArgs e) => OpenSelected();

    private void AwardList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (AwardList.SelectedItem is not AwardCatalogEntry award)
            return;

        switch (award.Id)
        {
            case "wae-mixed":
                new WaeWindow(_repository) { Owner = this }.Show();
                break;
        }
    }
}
