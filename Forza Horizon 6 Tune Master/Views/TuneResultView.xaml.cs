using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Forza_Horizon_6_Tune_Master.Services;
using Forza_Horizon_6_Tune_Master.ViewModels;

namespace Forza_Horizon_6_Tune_Master.Views;

public partial class TuneResultView : UserControl
{
    private TuneOverlayWindow? _overlayWindow;
    private MainViewModel? _vm;

    public TuneResultView()
    {
        InitializeComponent();
        Resources["ValueFontSize"] = 14.0;
        SizeChanged += OnSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        double size = Math.Clamp(14.0 + (e.NewSize.Width - 350.0) / 250.0 * 8.0, 14.0, 22.0);
        Resources["ValueFontSize"] = size;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is TuneOverlayWindow) return;

        if (DataContext is MainViewModel vm)
        {
            // Idempotent: a repeated Loaded without an intervening Unloaded would otherwise
            // leave a stale handler on the long-lived MainViewModel, pinning this view alive.
            if (_vm != null) _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = vm;
            vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm = null;
        }
    }

    // ── Overlay position persistence ────────────────────────────────────────
    private static string StatePath => Path.Combine(ForzaPaths.BaseDir, "overlay_state.json");

    private static void SaveOverlayState(Window w)
    {
        try
        {
            var dir = Path.GetDirectoryName(StatePath);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new { w.Left, w.Top, w.Width, w.Height });
            File.WriteAllText(StatePath, json);
        }
        catch { /* best effort */ }
    }

    private static (double left, double top, double width, double height)? LoadOverlayState()
    {
        try
        {
            if (!File.Exists(StatePath)) return null;
            var json = File.ReadAllText(StatePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return (root.GetProperty("Left").GetDouble(),
                    root.GetProperty("Top").GetDouble(),
                    root.GetProperty("Width").GetDouble(),
                    root.GetProperty("Height").GetDouble());
        }
        catch { return null; }
    }

    private static bool IsOverlayStateValid((double left, double top, double width, double height) state)
    {
        if (double.IsNaN(state.left) || double.IsNaN(state.top) ||
            double.IsNaN(state.width) || double.IsNaN(state.height))
            return false;
        if (double.IsInfinity(state.left) || double.IsInfinity(state.top) ||
            double.IsInfinity(state.width) || double.IsInfinity(state.height))
            return false;
        if (state.width < 100 || state.height < 100)
            return false;

        var winRect = new Rect(state.left, state.top, state.width, state.height);
        var screenRect = new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        return winRect.IntersectsWith(screenRect);
    }

    // ── Overlay lifecycle ───────────────────────────────────────────────────
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsOverlayMode)) return;
        if (_vm == null) return;

        if (_vm.IsOverlayMode)
        {
            if (_overlayWindow != null) return;

            _overlayWindow = new TuneOverlayWindow();
            _overlayWindow.DataContext = _vm;

            var saved = LoadOverlayState();
            if (saved.HasValue && IsOverlayStateValid(saved.Value))
            {
                _overlayWindow.Left   = saved.Value.left;
                _overlayWindow.Top    = saved.Value.top;
                _overlayWindow.Width  = saved.Value.width;
                _overlayWindow.Height = saved.Value.height;
            }
            else if (Application.Current.MainWindow is Window main)
            {
                _overlayWindow.Left = main.Left + (main.ActualWidth - _overlayWindow.Width) / 2;
                _overlayWindow.Top  = main.Top + (main.ActualHeight - _overlayWindow.Height) / 2;
            }

            var window = _overlayWindow;
            window.Closed += (_, _) =>
            {
                SaveOverlayState(window);
                _overlayWindow = null;
            };
            _overlayWindow.Show();
        }
        else
        {
            if (_overlayWindow == null) return;
            _overlayWindow.Close();
            _overlayWindow = null;
        }
    }
}
