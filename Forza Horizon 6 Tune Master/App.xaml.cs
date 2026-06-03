using System.Windows;
using System.Windows.Threading;

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
            MessageBox.Show(
                $"Необработанная ошибка: {e.Exception.Message}",
                "Критическая ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            MessageBox.Show(
                $"Критическая ошибка: {((Exception)e.ExceptionObject).Message}",
                "Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        };
    }
}