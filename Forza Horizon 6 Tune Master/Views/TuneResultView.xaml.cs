using System;
using System.Windows;
using System.Windows.Controls;

namespace Forza_Horizon_6_Tune_Master.Views;

public partial class TuneResultView : UserControl
{
    public TuneResultView()
    {
        InitializeComponent();
        Resources["ValueFontSize"] = 14.0;
        SizeChanged += OnSizeChanged;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Linear: 14px at ~350px panel width, 22px at ~600px panel width
        double size = Math.Clamp(14.0 + (e.NewSize.Width - 350.0) / 250.0 * 8.0, 14.0, 22.0);
        Resources["ValueFontSize"] = size;
    }
}
