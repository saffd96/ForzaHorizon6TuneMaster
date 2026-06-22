using System;
using System.IO;
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
            LogException("DispatcherUnhandledException", e.Exception);
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
            LogException("UnhandledException", e.ExceptionObject as Exception);
            var svc = LocalizationService.Instance;
            MessageBox.Show(
                svc.T("AppCriticalError", ((Exception)e.ExceptionObject).Message),
                svc.T("ErrorCaption"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };
    }

    // Best-effort crash log so swallowed UI exceptions and fatal startup failures leave a
    // trace (%APPDATA%\ForzaTuneMaster\error.log) instead of vanishing silently.
    private static void LogException(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(ForzaPaths.BaseDir);
            File.AppendAllText(
                Path.Combine(ForzaPaths.BaseDir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex}{Environment.NewLine}");
        }
        catch { /* logging must never throw */ }
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
            LogException("Database init", ex);
            var svc = LocalizationService.Instance;
            MessageBox.Show(
                svc.T("DbInitErrorMessage", ex.Message),
                svc.T("DbInitErrorCaption"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}