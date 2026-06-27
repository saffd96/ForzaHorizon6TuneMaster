using System.Collections.Generic;
using System.Windows;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.Views;

public partial class ChangelogWindow : Window
{
    public ChangelogWindow()
    {
        InitializeComponent();
        DataContext = this;

        var loc = LocalizationService.Instance;

        AppVersion = SavedProfile.ProfileVersion;

        var entries = new List<ChangelogEntry>();

        for (int i = 0; ; i++)
        {
            var key = $"ChangelogV{i}";
            var text = loc.T(key);
            if (text == key)
                break;
            entries.Add(new ChangelogEntry { Body = text });
        }

        VersionsList.ItemsSource = entries;
    }

    public string AppVersion { get; }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class ChangelogEntry
{
    public string Body { get; set; } = "";
}
