using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Forza_Horizon_6_Tune_Master.ViewModels;

namespace Forza_Horizon_6_Tune_Master.Views;

public partial class TuneOverlayWindow : Window
{
    public TuneOverlayWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (DataContext is MainViewModel vm)
                vm.IsOverlayMode = false;
            e.Handled = true;
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsOverlayMode = false;
    }

    private void ResizeThumb_Top(object sender, DragDeltaEventArgs e)
    {
        double h = Height - e.VerticalChange;
        if (h >= MinHeight) { Top += e.VerticalChange; Height = h; }
    }

    private void ResizeThumb_Bottom(object sender, DragDeltaEventArgs e)
    {
        double h = Height + e.VerticalChange;
        if (h >= MinHeight) Height = h;
    }

    private void ResizeThumb_Left(object sender, DragDeltaEventArgs e)
    {
        double w = Width - e.HorizontalChange;
        if (w >= MinWidth) { Left += e.HorizontalChange; Width = w; }
    }

    private void ResizeThumb_Right(object sender, DragDeltaEventArgs e)
    {
        double w = Width + e.HorizontalChange;
        if (w >= MinWidth) Width = w;
    }

    private void ResizeThumb_BR(object sender, DragDeltaEventArgs e)
    {
        double w = Width + e.HorizontalChange;
        double h = Height + e.VerticalChange;
        if (w >= MinWidth) Width = w;
        if (h >= MinHeight) Height = h;
    }

    private void ResizeThumb_BL(object sender, DragDeltaEventArgs e)
    {
        double w = Width - e.HorizontalChange;
        double h = Height + e.VerticalChange;
        if (w >= MinWidth) { Left += e.HorizontalChange; Width = w; }
        if (h >= MinHeight) Height = h;
    }

    private void ResizeThumb_TR(object sender, DragDeltaEventArgs e)
    {
        double w = Width + e.HorizontalChange;
        double h = Height - e.VerticalChange;
        if (w >= MinWidth) Width = w;
        if (h >= MinHeight) { Top += e.VerticalChange; Height = h; }
    }

    private void ResizeThumb_TL(object sender, DragDeltaEventArgs e)
    {
        double w = Width - e.HorizontalChange;
        double h = Height - e.VerticalChange;
        if (w >= MinWidth) { Left += e.HorizontalChange; Width = w; }
        if (h >= MinHeight) { Top += e.VerticalChange; Height = h; }
    }
}
