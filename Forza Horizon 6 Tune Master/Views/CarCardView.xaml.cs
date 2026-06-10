using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using Forza_Horizon_6_Tune_Master.ViewModels;

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

        LocalizationService.Instance.PropertyChanged += OnLocalizationChanged;
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item") return;
        Dispatcher.Invoke(RefreshAllEnumCombos);
    }

    private void RefreshAllEnumCombos()
    {
        RefreshComboSource(PowertrainTypeCombo, Enum.GetValues<PowertrainType>());
        RefreshComboSource(EngineTypeCombo,     Enum.GetValues<EngineType>());
        RefreshComboSource(EnginePositionCombo, Enum.GetValues<EnginePosition>());
        RefreshComboSource(AspirationCombo,     Enum.GetValues<AspirationType>());
        RefreshComboSource(DriveTypeCombo,      Enum.GetValues<DriveType>());
        RefreshComboSource(TireTypeCombo,       Enum.GetValues<TireType>());
        RefreshComboSource(SuspensionCombo,     Enum.GetValues<SuspensionUpgrade>());
        RefreshComboSource(DiffCombo,           Enum.GetValues<DifferentialUpgrade>());
        RefreshComboSource(BrakesCombo,         Enum.GetValues<BrakesUpgrade>());

        EnginePositionText.GetBindingExpression(TextBlock.TextProperty)?.UpdateTarget();
    }

    private static void RefreshComboSource(ComboBox combo, Array items)
    {
        var value = combo.SelectedValue;
        combo.ItemsSource = null;
        combo.ItemsSource = items;
        if (value is not null)
            combo.SelectedValue = value;
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

    private void SuspensionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.Car.SuspensionAllowsAdvancedTuning)
        {
            Dispatcher.BeginInvoke(new Action(() => RideHeightRearEndBox?.BringIntoView()));
        }
    }

    private void AeroCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() => AeroEndBox?.BringIntoView()));
    }

    public void FlashCarSelection()
    {
        var accent = (System.Windows.Media.Brush)FindResource("AccentBrush");
        var defaultBg = CarSelectionCard.Background;

        var anim = new System.Windows.Media.Animation.ObjectAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(1.5),
            FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop
        };
        anim.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteObjectKeyFrame(accent,    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.00))));
        anim.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteObjectKeyFrame(defaultBg, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.25))));
        anim.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteObjectKeyFrame(accent,    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.50))));
        anim.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteObjectKeyFrame(defaultBg, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.75))));
        anim.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteObjectKeyFrame(accent,    KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.00))));
        anim.KeyFrames.Add(new System.Windows.Media.Animation.DiscreteObjectKeyFrame(defaultBg, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(1.25))));

        CarSelectionCard.BeginAnimation(System.Windows.Controls.Border.BackgroundProperty, anim);
    }
}
