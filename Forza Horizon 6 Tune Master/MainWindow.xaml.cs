using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
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
        // The whole UI now lives inside a Viewbox with a fixed design canvas, so it
        // scales proportionally with the window on its own. Font sizes stay at their
        // base values (scaling them here as well would double-scale the text).
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
}
