using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Forza_Horizon_6_Tune_Master.ViewModels;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master;

public partial class App : Application
{
    private Window? _mainWindow;

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

    private static void LogException(string source, Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(ForzaPaths.BaseDir);
            File.AppendAllText(
                Path.Combine(ForzaPaths.BaseDir, "error.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source}: {ex}{Environment.NewLine}");
        }
        catch { }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // If the user agreed to restart-and-update on the previous run, fetch it now,
        // before anything (localization, DB) is loaded into memory for this session.
        bool justUpdated = false;
        if (UpdateService.IsPendingUpdateMarked())
        {
            UpdateService.ClearPendingUpdateMark();
            try
            {
                var (dbOutdated, locOutdated) = await UpdateService.Instance.CheckAsync().ConfigureAwait(true);
                if (dbOutdated)
                    await UpdateService.Instance.DownloadDbAsync().ConfigureAwait(true);
                if (locOutdated)
                    await UpdateService.Instance.DownloadLocAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                LogException("Pending update download", ex);
            }
            justUpdated = true;
        }

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
        _mainWindow = mainWindow;
        mainWindow.Show();

        if (justUpdated)
            ScheduleDailyCheck();
        else
            _ = CheckForUpdateAndPromptAsync();
    }

    private async Task CheckForUpdateAndPromptAsync()
    {
        try
        {
            var (dbOutdated, locOutdated) = await UpdateService.Instance.CheckAsync().ConfigureAwait(true);
            if (dbOutdated || locOutdated)
            {
                var svc = LocalizationService.Instance;
                var result = MessageBox.Show(
                    svc.T("UpdateAvailablePrompt"),
                    svc.T("UpdateCaption"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    UpdateService.MarkPendingUpdate();
                    System.Diagnostics.Process.Start(
                        System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName);
                    Shutdown();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            LogException("Update check", ex);
        }

        ScheduleDailyCheck();
    }

    private void ScheduleDailyCheck()
    {
        var now = DateTime.UtcNow;
        var next = now.Date.AddDays(1);
        var delay = next - now;

        var timer = new DispatcherTimer
        {
            Interval = delay,
            IsEnabled = true
        };
        timer.Tick += async (_, _) =>
        {
            timer.IsEnabled = false;
            await CheckForUpdateAndPromptAsync();
        };
    }
}
