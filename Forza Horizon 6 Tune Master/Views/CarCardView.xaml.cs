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
        DiffCombo.ItemsSource           = Enum.GetValues<DifferentialUpgrade>();
        BrakesCombo.ItemsSource         = Enum.GetValues<BrakesUpgrade>();
    }
}
