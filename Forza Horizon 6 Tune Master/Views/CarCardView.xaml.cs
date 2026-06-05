using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Views;

public partial class CarCardView : UserControl
{
    public CarCardView()
    {
        InitializeComponent();

        PowertrainTypeCombo.ItemsSource = Enum.GetValues<PowertrainType>();
        EngineTypeCombo.ItemsSource     = Enum.GetValues<EngineType>();
        EnginePositionCombo.ItemsSource = Enum.GetValues<EnginePosition>();
        AspirationCombo.ItemsSource     = Enum.GetValues<AspirationType>();
        DriveTypeCombo.ItemsSource      = Enum.GetValues<DriveType>();
        TireTypeCombo.ItemsSource       = Enum.GetValues<TireType>();
        SuspensionCombo.ItemsSource     = Enum.GetValues<SuspensionUpgrade>();
        DiffCombo.ItemsSource           = Enum.GetValues<DifferentialUpgrade>().Where(d => d != DifferentialUpgrade.Street).ToArray();
        BrakesCombo.ItemsSource         = Enum.GetValues<BrakesUpgrade>();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        CarListBox.Visibility = Visibility.Visible;
        CarSearchBox.Focus();
    }

    private void CarSearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        CarListBox.Visibility = Visibility.Visible;
    }

    private void CarSearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!CarListBox.IsKeyboardFocusWithin)
            CarListBox.Visibility = Visibility.Collapsed;
    }

    private void CarListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CarListBox.SelectedItem != null)
            CarListBox.Visibility = Visibility.Collapsed;
    }
}
