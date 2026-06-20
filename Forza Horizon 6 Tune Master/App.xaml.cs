using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master;

public partial class App : Application
{
    public App()
    {
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(
                System.Windows.Markup.XmlLanguage.GetLanguage("en-US")));

        DispatcherUnhandledException += (_, e) =>
        {
            var svc = LocalizationService.Instance;
            MessageBox.Show(
                svc.T("AppErrorMessage", e.Exception.Message),
                svc.T("CriticalErrorCaption"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var svc = LocalizationService.Instance;
            MessageBox.Show(
                svc.T("AppCriticalError", ((Exception)e.ExceptionObject).Message),
                svc.T("ErrorCaption"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LocalizationService.Instance.InitializeFromSystem();

        try
        {
            await Fh6DatabaseService.Instance.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize database: {ex.Message}\n\nThe application cannot continue.",
                "Database Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}