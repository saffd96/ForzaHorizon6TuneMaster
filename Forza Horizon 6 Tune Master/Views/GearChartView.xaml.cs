using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Forza_Horizon_6_Tune_Master.Views;

public partial class GearChartView : UserControl
{
    public static readonly DependencyProperty GearRatiosProperty =
        DependencyProperty.Register(nameof(GearRatios), typeof(IList<double>), typeof(GearChartView),
            new PropertyMetadata(null, OnPropChanged));

    public static readonly DependencyProperty FinalDriveProperty =
        DependencyProperty.Register(nameof(FinalDrive), typeof(double), typeof(GearChartView),
            new PropertyMetadata(0.0, OnPropChanged));

    public static readonly DependencyProperty MaxRPMProperty =
        DependencyProperty.Register(nameof(MaxRPM), typeof(int), typeof(GearChartView),
            new PropertyMetadata(7000, OnPropChanged));

    public static readonly DependencyProperty PowerPeakRPMProperty =
        DependencyProperty.Register(nameof(PowerPeakRPM), typeof(int), typeof(GearChartView),
            new PropertyMetadata(5800, OnPropChanged));

    public static readonly DependencyProperty TorquePeakRPMProperty =
        DependencyProperty.Register(nameof(TorquePeakRPM), typeof(int), typeof(GearChartView),
            new PropertyMetadata(4000, OnPropChanged));

    public static readonly DependencyProperty WheelDiameterInchProperty =
        DependencyProperty.Register(nameof(WheelDiameterInch), typeof(double), typeof(GearChartView),
            new PropertyMetadata(25.0, OnPropChanged));

    public static readonly DependencyProperty ActualMaxSpeedKmhProperty =
        DependencyProperty.Register(nameof(ActualMaxSpeedKmh), typeof(double), typeof(GearChartView),
            new PropertyMetadata(0.0, OnPropChanged));

    public static readonly DependencyProperty UseImperialProperty =
        DependencyProperty.Register(nameof(UseImperial), typeof(bool), typeof(GearChartView),
            new PropertyMetadata(false, OnPropChanged));

    public IList<double>? GearRatios
    {
        get => (IList<double>?)GetValue(GearRatiosProperty);
        set => SetValue(GearRatiosProperty, value);
    }

    public double FinalDrive
    {
        get => (double)GetValue(FinalDriveProperty);
        set => SetValue(FinalDriveProperty, value);
    }

    public int MaxRPM
    {
        get => (int)GetValue(MaxRPMProperty);
        set => SetValue(MaxRPMProperty, value);
    }

    public int PowerPeakRPM
    {
        get => (int)GetValue(PowerPeakRPMProperty);
        set => SetValue(PowerPeakRPMProperty, value);
    }

    public int TorquePeakRPM
    {
        get => (int)GetValue(TorquePeakRPMProperty);
        set => SetValue(TorquePeakRPMProperty, value);
    }

    public double WheelDiameterInch
    {
        get => (double)GetValue(WheelDiameterInchProperty);
        set => SetValue(WheelDiameterInchProperty, value);
    }

    public double ActualMaxSpeedKmh
    {
        get => (double)GetValue(ActualMaxSpeedKmhProperty);
        set => SetValue(ActualMaxSpeedKmhProperty, value);
    }

    public bool UseImperial
    {
        get => (bool)GetValue(UseImperialProperty);
        set => SetValue(UseImperialProperty, value);
    }

    private static readonly Color[] Palette =
    [
        Color.FromRgb(0xFF, 0x5E, 0x0E),
        Color.FromRgb(0x1A, 0xBC, 0xFE),
        Color.FromRgb(0x22, 0xC5, 0x5E),
        Color.FromRgb(0xEF, 0x44, 0x44),
        Color.FromRgb(0xA8, 0x55, 0xF7),
        Color.FromRgb(0xF5, 0x9E, 0x0B),
        Color.FromRgb(0xEC, 0x48, 0x99),
        Color.FromRgb(0x14, 0xB8, 0xA6),
        Color.FromRgb(0x8B, 0x5C, 0xF6),
        Color.FromRgb(0xF9, 0x73, 0x16),
    ];

    public GearChartView()
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
        Forza_Horizon_6_Tune_Master.Services.LocalizationService.Instance.PropertyChanged -= OnLocaleChanged;
        Forza_Horizon_6_Tune_Master.Services.LocalizationService.Instance.PropertyChanged += OnLocaleChanged;
        MainWindow.FontSizesChanged -= OnFontSizesChanged;
        MainWindow.FontSizesChanged += OnFontSizesChanged;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        SizeChanged -= OnSizeChanged;
        Forza_Horizon_6_Tune_Master.Services.LocalizationService.Instance.PropertyChanged -= OnLocaleChanged;
        MainWindow.FontSizesChanged -= OnFontSizesChanged;
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

    private void OnFontSizesChanged()
    {
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
        if (d is GearChartView g) g.RequestAnimatedRedraw();
    }

    /// <summary>Speed (km/h) for a given gear at given RPM.</summary>
    private double SpeedAt(int gearIdx, double rpm)
    {
        var ratios = GearRatios;
        if (ratios == null || gearIdx < 0 || gearIdx >= ratios.Count || ratios[gearIdx] <= 0 || FinalDrive <= 0) return 0;
        double L = Math.PI * WheelDiameterInch * 0.0254; // tyre circumference (m)
        return rpm * L * 60.0 / (1000.0 * ratios[gearIdx] * FinalDrive);
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();

        var ratios = GearRatios;
        if (ratios == null || ratios.Count < 2 || FinalDrive <= 0 || MaxRPM <= 0) return;

        double w = ChartCanvas.ActualWidth;
        double h = ChartCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        const double padL = 48, padR = 50, padT = 24, padB = 48;
        double cw = w - padL - padR;
        double ch = h - padT - padB;
        if (cw <= 0 || ch <= 0) return;

        int n = ratios.Count;

        // max speed across all gears at rev limit
        double maxSpeed = 0;
        for (int i = 0; i < n; i++)
            maxSpeed = Math.Max(maxSpeed, SpeedAt(i, MaxRPM));
        if (maxSpeed <= 0) return;
        double speedCap = maxSpeed * 1.08;

        double kmhToDisplay = UseImperial ? 0.621371 : 1.0;
        double displayCap = speedCap * kmhToDisplay;

        double rpmCap = MaxRPM * 1.06;

        // mapping helpers
        double SpeedToX(double s) => padL + (s / speedCap) * cw;
        double RpmToY(double r)   => padT + ch - (r / rpmCap) * ch;

        var gridBrush     = new SolidColorBrush(Color.FromRgb(0x20, 0x35, 0x50));
        var lblBrush      = new SolidColorBrush(Color.FromRgb(0x88, 0x92, 0xA4));
        var dimLblBrush   = new SolidColorBrush(Color.FromRgb(0x66, 0x70, 0x80));
        var lblFamily     = new FontFamily(new Uri("pack://application:,,,/"), "./Resources/Fonts/#JetBrains Mono");
        double fontMicro  = (double?)TryFindResource("FontMicro") ?? 10;
        double fontNormal = (double?)TryFindResource("FontNormal") ?? 13;

        // ── gridlines (vertical – speed) ──
        int speedStep = (int)(speedCap <= 100 ? 20 : speedCap <= 250 ? 50 : 100);
        int displayStep = UseImperial ? (int)Math.Round(speedStep * kmhToDisplay / 10) * 10 : speedStep;
        if (displayStep < 5) displayStep = 5;
        for (int ds = 0; ds <= displayCap + displayStep; ds += displayStep)
        {
            double s = UseImperial ? ds / kmhToDisplay : ds;
            double x = SpeedToX(s);
            ChartCanvas.Children.Add(new Line
            {
                X1 = x, X2 = x, Y1 = padT, Y2 = padT + ch,
                Stroke = gridBrush,
                StrokeThickness = s == 0 ? 1.5 : 0.5
            });
            var tb = new TextBlock
            {
                Text = $"{ds}",
                Foreground = lblBrush,
                FontSize = fontMicro,
                FontFamily = lblFamily,
                TextAlignment = TextAlignment.Center
            };
            ChartCanvas.Children.Add(tb);
            Canvas.SetLeft(tb, x - 12);
            Canvas.SetTop(tb, padT + ch + 4);
        }

        // ── gridlines (horizontal – RPM) ──
        int rpmStep = MaxRPM <= 4000 ? 500 : 1000;
        for (int r = 0; r <= rpmCap; r += rpmStep)
        {
            double y = RpmToY(r);
            ChartCanvas.Children.Add(new Line
            {
                X1 = padL, X2 = w - padR, Y1 = y, Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = r == 0 ? 1.5 : 0.5
            });
            string txt = r >= 1000 ? $"{r / 1000}k" : $"{r}";
            var tb = new TextBlock
            {
                Text = txt,
                Foreground = lblBrush,
                FontSize = fontMicro,
                FontFamily = lblFamily,
                TextAlignment = TextAlignment.Right
            };
            ChartCanvas.Children.Add(tb);
            Canvas.SetLeft(tb, 2);
            Canvas.SetTop(tb, y - fontMicro * 0.5);
        }

        // ── axis labels ──
        var locSvc = Forza_Horizon_6_Tune_Master.Services.LocalizationService.Instance;
        string FormatLabel(string key, int rpm) => $"{locSvc.T(key)} {rpm / 1000}k";
        string axisLabel = UseImperial
            ? $"{locSvc.T("ChartAxisSpeed")}, {locSvc.T("UnitMph")}"
            : $"{locSvc.T("ChartAxisSpeed")}, {locSvc.T("UnitKmh")}";
        var axisLbl = new TextBlock
        {
            Text = axisLabel,
            Foreground = dimLblBrush,
            FontSize = fontMicro,
            FontFamily = lblFamily,
            TextAlignment = TextAlignment.Center
        };
        ChartCanvas.Children.Add(axisLbl);
        Canvas.SetLeft(axisLbl, padL + cw * 0.5 - 28);
        Canvas.SetTop(axisLbl, h - 24);

        axisLbl = new TextBlock
        {
            Text = locSvc.T("ChartAxisRPM"),
            Foreground = dimLblBrush,
            FontSize = fontMicro,
            FontFamily = lblFamily
        };
        ChartCanvas.Children.Add(axisLbl);
        Canvas.SetLeft(axisLbl, 2);
        Canvas.SetTop(axisLbl, 10);

        // ── horizontal reference lines ──
        // rev limit
        double refY = RpmToY(MaxRPM);
        ChartCanvas.Children.Add(new Line
        {
            X1 = padL, X2 = w - padR, Y1 = refY, Y2 = refY,
            Stroke = new SolidColorBrush(Color.FromArgb(0x70, 0xEF, 0x44, 0x44)),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection([6, 4])
        });
        var refLbl = new TextBlock
        {
            Text = FormatLabel("ChartRevLimitLabel", MaxRPM),
            Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 0xEF, 0x44, 0x44)),
            FontSize = fontMicro,
            FontFamily = lblFamily
        };
        ChartCanvas.Children.Add(refLbl);
        double refLabelLeft = w - padR - 60;
        Canvas.SetLeft(refLbl, refLabelLeft);
        Canvas.SetTop(refLbl, refY - 8);

        // ── gear lines ──
        // pre-compute end points
        var endX = new double[n];
        var endY = new double[n];
        for (int i = 0; i < n; i++)
        {
            endX[i] = SpeedToX(SpeedAt(i, MaxRPM));
            endY[i] = RpmToY(MaxRPM);
        }

        for (int i = 0; i < n; i++)
        {
            var col = Palette[i % Palette.Length];
            var brush = new SolidColorBrush(col);

            // main line (0,0) → (maxSpeedAtRedline, MaxRPM)
            double x0 = SpeedToX(0);
            double y0 = RpmToY(0);
            double x1 = endX[i];
            double y1 = endY[i];
            ChartCanvas.Children.Add(new Line
            {
                X1 = x0, Y1 = y0, X2 = x1, Y2 = y1,
                Stroke = brush,
                StrokeThickness = 2
            });

            // gear number label at the end of the line
            var lbl = new TextBlock
            {
                Text = $"{i + 1}",
                Foreground = brush,
                FontSize = fontNormal,
                FontWeight = FontWeights.Bold,
                FontFamily = lblFamily,
                TextAlignment = TextAlignment.Center
            };
            ChartCanvas.Children.Add(lbl);
            Canvas.SetLeft(lbl, x1 - 5);
            Canvas.SetTop(lbl, y1 - 20);

            // speed label at top of gear line (km/h or mph at redline)
            double topSpeed = SpeedAt(i, MaxRPM) * kmhToDisplay;
            var speedLbl = new TextBlock
            {
                Text = $"{topSpeed:F0}",
                Foreground = new SolidColorBrush(Color.FromArgb(0xBB, col.R, col.G, col.B)),
                FontSize = fontMicro,
                FontFamily = lblFamily,
                TextAlignment = TextAlignment.Center
            };
            ChartCanvas.Children.Add(speedLbl);
            Canvas.SetLeft(speedLbl, x1 + 6);
            Canvas.SetTop(speedLbl, 4);
        }

        // vertical dashed line at actual max speed
        double actualMaxKmh = ActualMaxSpeedKmh;
        double actualMaxDisplay = actualMaxKmh * kmhToDisplay;
        if (actualMaxKmh > 0 && actualMaxKmh <= speedCap)
        {
            double xMax = SpeedToX(actualMaxKmh);
            ChartCanvas.Children.Add(new Line
            {
                X1 = xMax, X2 = xMax, Y1 = padT, Y2 = padT + ch,
                Stroke = new SolidColorBrush(Color.FromArgb(0x99, 0x22, 0xC5, 0x5E)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection([5, 4])
            });
            var maxLbl = new TextBlock
            {
                Text = $"{actualMaxDisplay:F0}",
                Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0x22, 0xC5, 0x5E)),
                FontSize = fontMicro,
                FontFamily = lblFamily,
                TextAlignment = TextAlignment.Center
            };
            ChartCanvas.Children.Add(maxLbl);
            Canvas.SetLeft(maxLbl, xMax - 12);
            Canvas.SetTop(maxLbl, padT + ch + 4);
        }

        // ── shift trajectory (acceleration ladder) ──
        var trajBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        var shiftBrush = new SolidColorBrush(Color.FromArgb(0xBB, 0xFF, 0xFF, 0xFF));
        var shiftLblBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0xE4, 0xE8, 0xF0));

        // start at origin
        double curX = SpeedToX(0);
        double curY = RpmToY(0);

        for (int i = 0; i < n; i++)
        {
            // acceleration along gear i — from current (curX, curY) up to redline
            double targetX = endX[i];
            double targetY = endY[i];

            ChartCanvas.Children.Add(new Line
            {
                X1 = curX, Y1 = curY, X2 = targetX, Y2 = targetY,
                Stroke = trajBrush,
                StrokeThickness = 3,
                StrokeLineJoin = PenLineJoin.Round
            });

            // data point dot at the start of this gear segment
            ChartCanvas.Children.Add(new Ellipse
            {
                Width = 6, Height = 6,
                Fill = i == 0 ? Brushes.White : new SolidColorBrush(Palette[(i - 1) % Palette.Length])
            });
            Canvas.SetLeft(ChartCanvas.Children[^1], curX - 3);
            Canvas.SetTop(ChartCanvas.Children[^1], curY - 3);

            // shift to next gear
            if (i < n - 1)
            {
                double dropRpm = MaxRPM * ratios[i + 1] / ratios[i];
                double shiftX = targetX; // same speed
                double shiftY = RpmToY(dropRpm);

                // vertical drop line
                ChartCanvas.Children.Add(new Line
                {
                    X1 = targetX, Y1 = targetY, X2 = shiftX, Y2 = shiftY,
                    Stroke = shiftBrush,
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection([3, 3])
                });

                // RPM drop label
                var dropLbl = new TextBlock
                {
                    Text = $"{(int)Math.Round(dropRpm)}",
                    Foreground = shiftLblBrush,
                    FontSize = fontMicro,
                    FontFamily = lblFamily,
                    TextAlignment = TextAlignment.Center
                };
                ChartCanvas.Children.Add(dropLbl);
                Canvas.SetLeft(dropLbl, targetX + 6);
                Canvas.SetTop(dropLbl, targetY - 12);

                curX = shiftX;
                curY = shiftY;
            }
            else
            {
                curX = targetX;
                curY = targetY;
            }
        }

        // final dot
        ChartCanvas.Children.Add(new Ellipse
        {
            Width = 6, Height = 6,
            Fill = Brushes.White
        });
        Canvas.SetLeft(ChartCanvas.Children[^1], curX - 3);
        Canvas.SetTop(ChartCanvas.Children[^1], curY - 3);

    }
}
