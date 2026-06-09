using System.Diagnostics;
using System.Windows;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.Views;

public partial class FeedbackWindow : Window
{
    public FeedbackWindow()
    {
        InitializeComponent();
    }

    private const string TargetEmail = "saffdtune@gmail.com";

    private void SendButton_Click(object sender, RoutedEventArgs e)
    {
        var feedback = FeedbackTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(feedback))
        {
            MessageBox.Show(
                LocalizationService.Instance.T("FeedbackTextLabel"),
                LocalizationService.Instance.T("FeedbackWindowTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var userEmail = EmailTextBox.Text?.Trim();
        var subject = Uri.EscapeDataString("Tune Master Feedback");
        var body = feedback;
        if (userEmail is { Length: > 0 })
            body += $"\n\n---\nFrom: {userEmail}";
        var encodedBody = Uri.EscapeDataString(body);

        var mailto = $"mailto:{TargetEmail}?subject={subject}&body={encodedBody}";

        Process.Start(new ProcessStartInfo(mailto) { UseShellExecute = true });

        FeedbackTextBox.IsEnabled = false;
        EmailTextBox.IsEnabled = false;
        SendButton.IsEnabled = false;
        SentPanel.Visibility = Visibility.Visible;
    }

    private async void DirectEmail_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Clipboard.SetText(TargetEmail);
        var previous = DirectEmailText.Text;
        DirectEmailText.Text = LocalizationService.Instance.T("FeedbackCopied");
        await Task.Delay(2000);
        DirectEmailText.Text = previous;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
