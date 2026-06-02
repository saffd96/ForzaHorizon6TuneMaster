using System.Windows;
using Forza_Horizon_6_Tune_Master.ViewModels;
using Forza_Horizon_6_Tune_Master.Views;

namespace Forza_Horizon_6_Tune_Master;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        SizeChanged += OnSizeChanged;
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        new HelpWindow { Owner = this }.ShowDialog();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool compact = ActualWidth < 1200;
        var res = Application.Current.Resources;
        res["FontNormal"]    = compact ? 12.0 : 13.0;
        res["FontSmall"]     = compact ? 11.0 : 12.0;
        res["FontXSmall"]    = compact ? 10.0 : 11.0;
        res["FontMicro"]     = compact ?  9.0 : 10.0;
        res["FontHeading"]   = compact ? 14.0 : 15.0;
        res["ValueFontSize"] = compact ? 18.0 : 22.0;
        res["FontTitle"]     = compact ? 20.0 : 24.0;
        res["FontHuge"]      = compact ? 26.0 : 32.0;
    }
}
