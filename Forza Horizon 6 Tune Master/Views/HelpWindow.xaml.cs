using System.Windows;

namespace Forza_Horizon_6_Tune_Master.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e) => Close();
}
