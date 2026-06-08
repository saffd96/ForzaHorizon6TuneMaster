using System.Windows;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        DontShowAgainCheck.IsChecked = true;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (DontShowAgainCheck.IsChecked == true)
            LocalizationService.Instance.SetFirstRunCompleted();
        else
            LocalizationService.Instance.ClearFirstRunCompleted();
        Close();
    }
}
