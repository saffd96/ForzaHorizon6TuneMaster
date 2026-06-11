using System;
using System.ComponentModel;
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
        SizeChanged += OnSizeChanged;
        StateChanged += OnWindowStateChanged;
        Loaded += OnLoaded;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);
        RegisterHotKey(_hwnd, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, VK_O);
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
        if (LocalizationService.Instance.IsFirstRun())
            new HelpWindow { Owner = this }.ShowDialog();
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

    private void AiSpecStatusOverlay_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            bool highlight = vm.NeedsCarSelectionHighlight;
            vm.DismissAiSpecStatusCommand.Execute(null);
            if (highlight)
            {
                ScrollToCarSelection();
                FlashCarSelection();
            }
        }
    }

    private void ScrollToCarSelection()
    {
        var sv = FindScrollViewer(this);
        sv?.ScrollToHome();
    }

    private void FlashCarSelection()
    {
        var carView = FindVisualChild<CarCardView>(this);
        carView?.FlashCarSelection();
    }

    private static System.Windows.Controls.ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Controls.ScrollViewer sv) return sv;
            var found = FindScrollViewer(child);
            if (found != null) return found;
        }
        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) return t;
            var found = FindVisualChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    private void DonateButton_Click(object sender, RoutedEventArgs e)
    {
        new DonateWindow { Owner = this }.ShowDialog();
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        new HelpWindow { Owner = this }.ShowDialog();
    }

    private void FeedbackButton_Click(object sender, RoutedEventArgs e)
    {
        new FeedbackWindow { Owner = this }.ShowDialog();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        bool compact = ActualWidth < 1200;
        var res = Application.Current.Resources;
        res["FontNormal"]    = compact ? 12.0 : 13.0;
        res["FontSmall"]     = compact ? 11.0 : 12.0;
        res["FontXSmall"]    = compact ? 10.0 : 11.0;
        res["FontMicro"]     = compact ?  9.0 : 10.0;
        res["FontHeading"]   = compact ? 14.0 : 15.0;
        res["ValueFontSize"] = compact ? 18.0 : 22.0;
        res["FontTitle"]     = compact ? 20.0 : 24.0;
        res["FontHuge"]      = compact ? 26.0 : 32.0;
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
}
