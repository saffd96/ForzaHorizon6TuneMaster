using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Forza_Horizon_6_Tune_Master.Views;

public partial class PowerCurveView : UserControl
{
    public static readonly DependencyProperty TorqueCurveProperty =
        DependencyProperty.Register(nameof(TorqueCurve), typeof(double[]), typeof(PowerCurveView),
            new PropertyMetadata(null, OnPropChanged));

    public static readonly DependencyProperty PowerCurveProperty =
        DependencyProperty.Register(nameof(PowerCurve), typeof(double[]), typeof(PowerCurveView),
            new PropertyMetadata(null, OnPropChanged));

    public static readonly DependencyProperty MaxRPMProperty =
        DependencyProperty.Register(nameof(MaxRPM), typeof(int), typeof(PowerCurveView),
            new PropertyMetadata(7000, OnPropChanged));

    public static readonly DependencyProperty TorquePeakRPMProperty =
        DependencyProperty.Register(nameof(TorquePeakRPM), typeof(int), typeof(PowerCurveView),
            new PropertyMetadata(4000, OnPropChanged));

    public static readonly DependencyProperty PowerPeakRPMProperty =
        DependencyProperty.Register(nameof(PowerPeakRPM), typeof(int), typeof(PowerCurveView),
            new PropertyMetadata(5800, OnPropChanged));

    public double[]? TorqueCurve
    {
        get => (double[]?)GetValue(TorqueCurveProperty);
        set => SetValue(TorqueCurveProperty, value);
    }

    public double[]? PowerCurve
    {
        get => (double[]?)GetValue(PowerCurveProperty);
        set => SetValue(PowerCurveProperty, value);
    }

    public int MaxRPM
    {
        get => (int)GetValue(MaxRPMProperty);
        set => SetValue(MaxRPMProperty, value);
    }

    public int TorquePeakRPM
    {
        get => (int)GetValue(TorquePeakRPMProperty);
        set => SetValue(TorquePeakRPMProperty, value);
    }

    public int PowerPeakRPM
    {
        get => (int)GetValue(PowerPeakRPMProperty);
        set => SetValue(PowerPeakRPMProperty, value);
    }

    public PowerCurveView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => DrawChart();
        Services.LocalizationService.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Item") DrawChart();
        };
    }

    private static void OnPropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PowerCurveView p) p.DrawChart();
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();

        var torque = TorqueCurve;
        var power = PowerCurve;
        if (torque == null || torque.Length < 2 || power == null || power.Length < 2 || MaxRPM <= 0) return;

        double w = ChartCanvas.ActualWidth;
        double h = ChartCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        const double padL = 48, padR = 150, padT = 48, padB = 48;
        double cw = w - padL - padR;
        double ch = h - padT - padB;
        if (cw <= 0 || ch <= 0) return;

        // If the two curves don't match yet (mid-update), skip — the next change will redraw.
        if (torque.Length != power.Length) return;
        int n = torque.Length;
        if (n < 2) return;
        double rpmCap = MaxRPM * 1.06;
        double maxTorque = torque.Max() * 1.10;
        double maxPower = power.Max() * 1.10;
        if (maxTorque <= 0 || maxPower <= 0) return;

        double RpmToX(double r) => padL + (r / rpmCap) * cw;
        double TorqueToY(double t) => padT + ch - (t / maxTorque) * ch;
        double PowerToY(double p) => padT + ch - (p / maxPower) * ch;

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x20, 0x35, 0x50));
        var lblBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x92, 0xA4));
        var dimLblBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x70, 0x80));
        var lblFamily = new FontFamily(new Uri("pack://application:,,,/"), "./Resources/Fonts/#JetBrains Mono");
        const double lblSize = 10;

        // ── gridlines (vertical — RPM) ──
        int rpmStep = MaxRPM <= 4000 ? 500 : 1000;
        for (int r = 0; r <= rpmCap; r += rpmStep)
        {
            double x = RpmToX(r);
            ChartCanvas.Children.Add(new Line
            {
                X1 = x, X2 = x, Y1 = padT, Y2 = padT + ch,
                Stroke = gridBrush,
                StrokeThickness = r == 0 ? 1.5 : 0.5
            });
            string txt = r >= 1000 ? $"{r / 1000}k" : $"{r}";
            var tb = new TextBlock
            {
                Text = txt,
                Foreground = lblBrush,
                FontSize = lblSize,
                FontFamily = lblFamily,
                TextAlignment = TextAlignment.Center
            };
            ChartCanvas.Children.Add(tb);
            Canvas.SetLeft(tb, x - 10);
            Canvas.SetTop(tb, padT + ch + 4);
        }

        // ── gridlines (horizontal — Torque) ──
        double tStep = maxTorque <= 100 ? 25 : maxTorque <= 300 ? 50 : 100;
        for (double t = 0; t <= maxTorque + tStep; t += tStep)
        {
            double y = TorqueToY(t);
            ChartCanvas.Children.Add(new Line
            {
                X1 = padL, X2 = w - padR, Y1 = y, Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = t == 0 ? 1.5 : 0.5
            });
            var tb = new TextBlock
            {
                Text = $"{t:F0}",
                Foreground = lblBrush,
                FontSize = lblSize,
                FontFamily = lblFamily,
                TextAlignment = TextAlignment.Right
            };
            ChartCanvas.Children.Add(tb);
            Canvas.SetLeft(tb, 2);
            Canvas.SetTop(tb, y - lblSize * 0.5);
        }

        // ── horizontal gridlines (power — right side) ──
        double pStep = maxPower <= 100 ? 25 : maxPower <= 300 ? 50 : maxPower <= 1000 ? 100 : 250;
        for (double p = 0; p <= maxPower + pStep; p += pStep)
        {
            double y = PowerToY(p);
            var tb = new TextBlock
            {
                Text = $"{p:F0}",
                Foreground = lblBrush,
                FontSize = lblSize,
                FontFamily = lblFamily,
                TextAlignment = TextAlignment.Left
            };
            ChartCanvas.Children.Add(tb);
            Canvas.SetLeft(tb, w - padR + 4);
            Canvas.SetTop(tb, y - lblSize * 0.5);
        }

        // ── axis labels ──
        var loc = Services.LocalizationService.Instance;

        var tAxisLbl = new TextBlock
        {
            Text = loc.T("CurveAxisTorque"),
            Foreground = dimLblBrush,
            FontSize = 9,
            FontFamily = lblFamily,
            TextAlignment = TextAlignment.Center
        };
        ChartCanvas.Children.Add(tAxisLbl);
        Canvas.SetLeft(tAxisLbl, 4);
        Canvas.SetTop(tAxisLbl, 8);

        var pAxisLbl = new TextBlock
        {
            Text = loc.T("CurveAxisPower"),
            Foreground = dimLblBrush,
            FontSize = 9,
            FontFamily = lblFamily,
            TextAlignment = TextAlignment.Center
        };
        ChartCanvas.Children.Add(pAxisLbl);
        Canvas.SetLeft(pAxisLbl, w - padR + 0);
        Canvas.SetTop(pAxisLbl, 0);

        var rpmAxisLbl = new TextBlock
        {
            Text = loc.T("ChartAxisRPM"),
            Foreground = dimLblBrush,
            FontSize = 9,
            FontFamily = lblFamily,
            TextAlignment = TextAlignment.Center
        };
        ChartCanvas.Children.Add(rpmAxisLbl);
        Canvas.SetLeft(rpmAxisLbl, padL + cw * 0.5 - 14);
        Canvas.SetTop(rpmAxisLbl, h - 24);

        // ── torque curve (filled polygon + line) ──
        var torqueColor = Color.FromRgb(0x1A, 0xBC, 0xFE);
        var torqueBrush = new SolidColorBrush(torqueColor);
        var torqueFill = new SolidColorBrush(Color.FromArgb(0x30, 0x1A, 0xBC, 0xFE));

        var tPoints = new PointCollection(n + 2);
        tPoints.Add(new Point(RpmToX(0), padT + ch));
        for (int i = 0; i < n; i++)
        {
            double rpm = MaxRPM * i / (double)(n - 1);
            tPoints.Add(new Point(RpmToX(rpm), TorqueToY(torque[i])));
        }
        tPoints.Add(new Point(RpmToX(MaxRPM), padT + ch));

        ChartCanvas.Children.Add(new Polygon
        {
            Points = tPoints,
            Fill = torqueFill,
            StrokeThickness = 0
        });

        var tLine = new Polyline
        {
            Points = new PointCollection(
                Enumerable.Range(0, n).Select(i =>
                {
                    double rpm = MaxRPM * i / (double)(n - 1);
                    return new Point(RpmToX(rpm), TorqueToY(torque[i]));
                })),
            Stroke = torqueBrush,
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        };
        ChartCanvas.Children.Add(tLine);

        // ── power curve (line) ──
        var powerColor = Color.FromRgb(0xFF, 0x5E, 0x0E);
        var powerBrush = new SolidColorBrush(powerColor);

        var pLine = new Polyline
        {
            Points = new PointCollection(
                Enumerable.Range(0, n).Select(i =>
                {
                    double rpm = MaxRPM * i / (double)(n - 1);
                    return new Point(RpmToX(rpm), PowerToY(power[i]));
                })),
            Stroke = powerBrush,
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        };
        ChartCanvas.Children.Add(pLine);

        // ── torque peak marker ──
        double tpX = RpmToX(TorquePeakRPM);
        double tpY = TorqueToY(torque.Max());
        double peakTorqueVal = torque.Max();

        ChartCanvas.Children.Add(new Line
        {
            X1 = tpX, X2 = tpX, Y1 = padT, Y2 = padT + ch,
            Stroke = new SolidColorBrush(Color.FromArgb(0x50, 0x1A, 0xBC, 0xFE)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection([4, 4])
        });

        var tpLbl = new TextBlock
        {
            Text = $"{peakTorqueVal:F0} Nm @ {TorquePeakRPM / 1000}k",
            Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0x1A, 0xBC, 0xFE)),
            FontSize = 9,
            FontFamily = lblFamily
        };
        ChartCanvas.Children.Add(tpLbl);
        Canvas.SetLeft(tpLbl, Math.Min(tpX + 4, w - padR - 80));
        Canvas.SetTop(tpLbl, tpY - 18);

        // dot at torque peak
        ChartCanvas.Children.Add(new Ellipse
        {
            Width = 7, Height = 7,
            Fill = torqueBrush
        });
        Canvas.SetLeft(ChartCanvas.Children[^1], tpX - 3.5);
        Canvas.SetTop(ChartCanvas.Children[^1], tpY - 3.5);

        // ── power peak marker ──
        double ppX = RpmToX(PowerPeakRPM);
        double ppY = PowerToY(power.Max());
        double peakPowerVal = power.Max();

        ChartCanvas.Children.Add(new Line
        {
            X1 = ppX, X2 = ppX, Y1 = padT, Y2 = padT + ch,
            Stroke = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0x5E, 0x0E)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection([4, 4])
        });

        var ppLbl = new TextBlock
        {
            Text = $"{peakPowerVal:F0} HP @ {PowerPeakRPM / 1000}k",
            Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0x5E, 0x0E)),
            FontSize = 9,
            FontFamily = lblFamily
        };
        ChartCanvas.Children.Add(ppLbl);
        Canvas.SetLeft(ppLbl, Math.Min(ppX + 4, w - padR - 80));
        Canvas.SetTop(ppLbl, ppY + 4);

        // dot at power peak
        ChartCanvas.Children.Add(new Ellipse
        {
            Width = 7, Height = 7,
            Fill = powerBrush
        });
        Canvas.SetLeft(ChartCanvas.Children[^1], ppX - 3.5);
        Canvas.SetTop(ChartCanvas.Children[^1], ppY - 3.5);

        // ── legend ──
        double legendX = w - padR + 46;
        double legendY = 36;

        // torque legend
        var tLegendLine = new Line
        {
            X1 = legendX, X2 = legendX + 14, Y1 = legendY + 6, Y2 = legendY + 6,
            Stroke = torqueBrush,
            StrokeThickness = 2.5
        };
        ChartCanvas.Children.Add(tLegendLine);

        var tLegendLbl = new TextBlock
        {
            Text = loc.T("CurveLegendTorque"),
            Foreground = lblBrush,
            FontSize = 9,
            FontFamily = lblFamily
        };
        ChartCanvas.Children.Add(tLegendLbl);
        Canvas.SetLeft(tLegendLbl, legendX + 18);
        Canvas.SetTop(tLegendLbl, legendY);

        // power legend
        double pLegendY = legendY + 16;
        var pLegendLine = new Line
        {
            X1 = legendX, X2 = legendX + 14, Y1 = pLegendY + 6, Y2 = pLegendY + 6,
            Stroke = powerBrush,
            StrokeThickness = 2.5
        };
        ChartCanvas.Children.Add(pLegendLine);

        var pLegendLbl = new TextBlock
        {
            Text = loc.T("CurveLegendPower"),
            Foreground = lblBrush,
            FontSize = 9,
            FontFamily = lblFamily
        };
        ChartCanvas.Children.Add(pLegendLbl);
        Canvas.SetLeft(pLegendLbl, legendX + 18);
        Canvas.SetTop(pLegendLbl, pLegendY);
    }
}
