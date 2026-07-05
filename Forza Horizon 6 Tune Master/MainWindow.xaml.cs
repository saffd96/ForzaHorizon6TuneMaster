using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Forza_Horizon_6_Tune_Master.Services;
using Forza_Horizon_6_Tune_Master.ViewModels;
using Forza_Horizon_6_Tune_Master.Views;

namespace Forza_Horizon_6_Tune_Master;

public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int  WM_HOTKEY          = 0x0312;
    private const uint MOD_CONTROL        = 0x0002;
    private const uint MOD_SHIFT          = 0x0004;
    private const uint MOD_NOREPEAT       = 0x4000;
    private const uint VK_O               = 0x4F;
    private const int  HOTKEY_ID          = 1;

    private HwndSource? _hwndSource;
    private IntPtr _hwnd;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        LoadFontOffset();
        ApplyFontOffset();
        StateChanged += OnWindowStateChanged;
        Loaded += OnLoaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);
        if (!RegisterHotKey(_hwnd, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_O))
            Debug.WriteLine("RegisterHotKey failed (Ctrl+Shift+O already in use?)");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            if (DataContext is MainViewModel vm)
                vm.IsOverlayMode = !vm.IsOverlayMode;
            handled = true;
        }
        return IntPtr.Zero;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _hwndSource?.RemoveHook(WndProc);
        UnregisterHotKey(_hwnd, HOTKEY_ID);
        base.OnClosing(e);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        bool showedHelp = false;
        // Do not show help and changelog at the same time
        if (LocalizationService.Instance.IsFirstRun())
        {
            new HelpWindow { Owner = this }.ShowDialog();
            showedHelp = true;
        }

        if (!showedHelp && LocalizationService.Instance.IsNewVersion())
        {
            new ChangelogWindow { Owner = this }.ShowDialog();
            LocalizationService.Instance.MarkVersionSeen();
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            ProfileDropdownPopup.IsOpen = false;
            CloseAllComboBoxDropDowns(this);
        }
    }

    private static void CloseAllComboBoxDropDowns(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ComboBox cb)
                cb.IsDropDownOpen = false;
            CloseAllComboBoxDropDowns(child);
        }
    }

    private void DonateButton_Click(object sender, RoutedEventArgs e)
    {
        new DonateWindow { Owner = this }.ShowDialog();
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        new HelpWindow { Owner = this }.ShowDialog();
    }

    private void VersionButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        new ChangelogWindow { Owner = this }.ShowDialog();
    }

    private void FeedbackButton_Click(object sender, RoutedEventArgs e)
    {
        new FeedbackWindow { Owner = this }.ShowDialog();
    }

    private static readonly double[] _fontBaseValues = { 13, 12, 11, 10, 15, 22, 24, 32 };
    private static readonly string[] _fontKeys =
    {
        "FontNormal", "FontSmall", "FontXSmall", "FontMicro",
        "FontHeading", "ValueFontSize", "FontTitle", "FontHuge"
    };

    private double _fontOffset;

    private void FontIncrease_Click(object sender, RoutedEventArgs e)
    {
        _fontOffset += 0.5;
        ApplyFontOffset();
    }

    private void FontDecrease_Click(object sender, RoutedEventArgs e)
    {
        if (_fontOffset <= -10) return;
        _fontOffset -= 0.5;
        ApplyFontOffset();
    }

    private void ApplyFontOffset()
    {
        var rd = FindFontResourceDictionary();
        if (rd != null)
        {
            for (int i = 0; i < _fontKeys.Length; i++)
                rd[_fontKeys[i]] = _fontBaseValues[i] + _fontOffset;
        }
        SaveFontOffset();
        FontSizesChanged?.Invoke();
    }

    public static event Action? FontSizesChanged;

    private static ResourceDictionary? FindFontResourceDictionary()
    {
        foreach (var md in Application.Current.Resources.MergedDictionaries)
        {
            if (md.Contains("FontNormal"))
                return md;
        }
        return null;
    }

    private static string FontOffsetPath => Path.Combine(ForzaPaths.BaseDir, "font_offset.json");

    private void LoadFontOffset()
    {
        try
        {
            if (!File.Exists(FontOffsetPath)) return;
            _fontOffset = double.Parse(
                File.ReadAllText(FontOffsetPath).Trim(),
                CultureInfo.InvariantCulture);
        }
        catch { }
    }

    private void SaveFontOffset()
    {
        try
        {
            Directory.CreateDirectory(ForzaPaths.BaseDir);
            File.WriteAllText(FontOffsetPath,
                _fontOffset.ToString("F1", CultureInfo.InvariantCulture));
        }
        catch { }
    }

    private void ProfileSearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        ProfileDropdownPopup.IsOpen = true;
    }

    private void ProfileSearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!ProfileListBox.IsKeyboardFocusWithin)
            ProfileDropdownPopup.IsOpen = false;
    }

    private void ProfileListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileListBox.SelectedItem != null)
            ProfileDropdownPopup.IsOpen = false;
    }

    private void ClearProfileSelection_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.SelectedProfile = null;
    }

    private void CarSearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        CarDropdownPopup.IsOpen = true;
    }

    private void CarSearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!CarListBox.IsKeyboardFocusWithin)
            CarDropdownPopup.IsOpen = false;
    }

    private void CarListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CarListBox.SelectedItem != null)
            CarDropdownPopup.IsOpen = false;
    }

    private void CarListItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CarDropdownPopup.IsOpen = false;
    }

    private void ProfileListItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ProfileDropdownPopup.IsOpen = false;
    }
}
