using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

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

    public static readonly DependencyProperty PowerUnitProperty =
        DependencyProperty.Register(nameof(PowerUnit), typeof(PowerUnit), typeof(PowerCurveView),
            new PropertyMetadata(PowerUnit.HP, OnPropChanged));

    public static readonly DependencyProperty UseImperialProperty =
        DependencyProperty.Register(nameof(UseImperial), typeof(bool), typeof(PowerCurveView),
            new PropertyMetadata(false, OnPropChanged));

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

    public PowerUnit PowerUnit
    {
        get => (PowerUnit)GetValue(PowerUnitProperty);
        set => SetValue(PowerUnitProperty, value);
    }

    public bool UseImperial
    {
        get => (bool)GetValue(UseImperialProperty);
        set => SetValue(UseImperialProperty, value);
    }

    public PowerCurveView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _revealTimer.Tick += OnRevealTick;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Idempotent: WPF can raise Loaded again without a matching Unloaded (visual-tree
        // re-parenting, tab switches), so unsubscribe first to avoid a stale handler pinning
        // this view alive on the long-lived LocalizationService singleton.
        SizeChanged -= OnSizeChanged;
        SizeChanged += OnSizeChanged;
        Services.LocalizationService.Instance.PropertyChanged -= OnLocaleChanged;
        Services.LocalizationService.Instance.PropertyChanged += OnLocaleChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        Services.LocalizationService.Instance.PropertyChanged -= OnLocaleChanged;
    }

    // On resize, redraw immediately (no animation). If a data-driven animation is
    // still pending (e.g. the chart had no size when the data first arrived), route
    // through the animated path now that a size exists.
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isTransitioning) { _dirtyAfterRedraw = true; return; }
        if (_animateNextDraw) RequestAnimatedRedraw();
        else DrawChart();
    }
    private void OnLocaleChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item") return;
        if (_isTransitioning) { _dirtyAfterRedraw = true; return; }
        DrawChart();
    }

    // Animate the chart when the plotted data changes: hide (fade out) → redraw with the
    // latest data → show (fade in). Plain resize/locale redraws don't animate. The debounce
    // lets a swap's wave of generations settle so they collapse into one transition; the
    // hidden redraw phase always uses the freshest data, so the user never sees a redraw
    // after the chart is shown.
    private bool _animateNextDraw = true;
    private bool _isTransitioning;
    private bool _redrawPassed;       // the hidden redraw phase has run for the active transition
    private bool _dirtyAfterRedraw;   // data changed after that redraw → needs another transition
    private readonly DispatcherTimer _revealTimer = new() { Interval = TimeSpan.FromMilliseconds(260) };

    private void RequestAnimatedRedraw()
    {
        // Deliberately do NOT redraw here: the old chart must stay on screen so the
        // hide phase has something to fade out before the new data is drawn.
        if (_isTransitioning)
        {
            // Before the hidden redraw it will pick up this change for free; only a change
            // arriving after it needs a fresh transition (avoids a bare redraw-after-show).
            if (_redrawPassed) _dirtyAfterRedraw = true;
            return;
        }
        _animateNextDraw = true;
        _revealTimer.Stop();
        _revealTimer.Start();
    }

    private void OnRevealTick(object? sender, EventArgs e)
    {
        _revealTimer.Stop();
        if (!_animateNextDraw) return;

        double w = ChartCanvas.ActualWidth, h = ChartCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return; // not laid out yet; OnSizeChanged will re-request

        _animateNextDraw = false;
        _isTransitioning = true;
        _redrawPassed = false;
        _dirtyAfterRedraw = false;
        ChartReveal.Transition(ChartCanvas, w, h,
            redraw: () => { DrawChart(); _redrawPassed = true; },
            onCompleted: OnTransitionCompleted);
    }

    private void OnTransitionCompleted()
    {
        _isTransitioning = false;
        // Data changed after the new chart was drawn → run a fresh hide→redraw→show
        // so the latest data is shown with the same ordering (never a bare redraw).
        if (_dirtyAfterRedraw)
        {
            _dirtyAfterRedraw = false;
            RequestAnimatedRedraw();
        }
    }

    private static void OnPropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PowerCurveView p) p.RequestAnimatedRedraw();
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();

        var torqueSrc = TorqueCurve;
        var powerSrc = PowerCurve;
        if (torqueSrc == null || torqueSrc.Length < 2 || powerSrc == null || powerSrc.Length < 2 || MaxRPM <= 0) return;

        double w = ChartCanvas.ActualWidth;
        double h = ChartCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        const double padL = 48, padR = 150, padT = 48, padB = 48;
        double cw = w - padL - padR;
        double ch = h - padT - padB;
        if (cw <= 0 || ch <= 0) return;

        // If the two curves don't match yet (mid-update), skip — the next change will redraw.
        if (torqueSrc.Length != powerSrc.Length) return;
        int n = torqueSrc.Length;
        if (n < 2) return;

        // Convert both curves to the user-selected display units. Axis numbers, gridlines,
        // peak labels and unit captions all follow PowerUnit / UseImperial; the curve shape
        // is unaffected because each axis rescales to its own max.
        var torque = new double[n];
        var power = new double[n];
        for (int i = 0; i < n; i++)
        {
            torque[i] = UnitConverter.TorqueToDisplay(torqueSrc[i], UseImperial);
            power[i] = UnitConverter.PowerToDisplay(powerSrc[i], PowerUnit);
        }
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
        string torqueUnit = loc.T(UseImperial ? "UnitLbFt" : "UnitNm");
        string powerUnit = PowerUnit switch
        {
            PowerUnit.KW => loc.T("UnitKw"),
            PowerUnit.PS => loc.T("UnitPs"),
            _            => loc.T("UnitHp")
        };

        var tAxisLbl = new TextBlock
        {
            Text = $"{loc.T("CurveLegendTorque")}, {torqueUnit}",
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
            Text = $"{loc.T("CurveLegendPower")}, {powerUnit}",
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
