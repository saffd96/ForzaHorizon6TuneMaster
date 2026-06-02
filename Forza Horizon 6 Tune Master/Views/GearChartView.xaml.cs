using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

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
        SizeChanged += (_, _) => DrawChart();
    }

    private static void OnPropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GearChartView g) g.DrawChart();
    }

    /// <summary>Speed (km/h) for a given gear at given RPM.</summary>
    private double SpeedAt(int gearIdx, double rpm)
    {
        var ratios = GearRatios;
        if (ratios == null || FinalDrive <= 0) return 0;
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

        const double padL = 48, padR = 56, padT = 28, padB = 32;
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

        double rpmCap = MaxRPM * 1.06;

        // mapping helpers
        double SpeedToX(double s) => padL + (s / speedCap) * cw;
        double RpmToY(double r)   => padT + ch - (r / rpmCap) * ch;

        var gridBrush     = new SolidColorBrush(Color.FromRgb(0x2D, 0x35, 0x50));
        var lblBrush      = new SolidColorBrush(Color.FromRgb(0x88, 0x92, 0xA4));
        var dimLblBrush   = new SolidColorBrush(Color.FromRgb(0x66, 0x70, 0x80));
        var lblFamily     = new FontFamily("Segoe UI");
        const double lblSize = 10;

        // ── gridlines (vertical – speed) ──
        int speedStep = speedCap <= 100 ? 20 : speedCap <= 250 ? 50 : 100;
        for (int s = 0; s <= speedCap + speedStep; s += speedStep)
        {
            double x = SpeedToX(s);
            ChartCanvas.Children.Add(new Line
            {
                X1 = x, X2 = x, Y1 = padT, Y2 = padT + ch,
                Stroke = gridBrush,
                StrokeThickness = s == 0 ? 1.5 : 0.5
            });
            var tb = new TextBlock
            {
                Text = $"{s}",
                Foreground = lblBrush,
                FontSize = lblSize,
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
                FontSize = lblSize,
                FontFamily = lblFamily,
                TextAlignment = TextAlignment.Right
            };
            ChartCanvas.Children.Add(tb);
            Canvas.SetLeft(tb, 2);
            Canvas.SetTop(tb, y - lblSize * 0.5);
        }

        // ── axis labels ──
        var axisLbl = new TextBlock
        {
            Text = "Скорость, км/ч",
            Foreground = dimLblBrush,
            FontSize = 9,
            FontFamily = lblFamily,
            TextAlignment = TextAlignment.Center
        };
        ChartCanvas.Children.Add(axisLbl);
        Canvas.SetLeft(axisLbl, padL + cw * 0.5 - 28);
        Canvas.SetTop(axisLbl, h - 18);

        axisLbl = new TextBlock
        {
            Text = "об/мин",
            Foreground = dimLblBrush,
            FontSize = 9,
            FontFamily = lblFamily
        };
        ChartCanvas.Children.Add(axisLbl);
        Canvas.SetLeft(axisLbl, 2);
        Canvas.SetTop(axisLbl, 6);

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
            Text = $"отсечка {MaxRPM / 1000}k",
            Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 0xEF, 0x44, 0x44)),
            FontSize = 9,
            FontFamily = lblFamily
        };
        ChartCanvas.Children.Add(refLbl);
        Canvas.SetLeft(refLbl, w - padR + 4);
        Canvas.SetTop(refLbl, refY - 8);

        // power peak
        double ppY = RpmToY(PowerPeakRPM);
        ChartCanvas.Children.Add(new Line
        {
            X1 = padL, X2 = w - padR, Y1 = ppY, Y2 = ppY,
            Stroke = new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0x5E, 0x0E)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection([4, 4])
        });
        refLbl = new TextBlock
        {
            Text = $"Pmax {PowerPeakRPM / 1000}k",
            Foreground = new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0x5E, 0x0E)),
            FontSize = 9,
            FontFamily = lblFamily
        };
        ChartCanvas.Children.Add(refLbl);
        Canvas.SetLeft(refLbl, w - padR + 4);
        Canvas.SetTop(refLbl, ppY - 8);

        // torque peak
        double tpY = RpmToY(TorquePeakRPM);
        ChartCanvas.Children.Add(new Line
        {
            X1 = padL, X2 = w - padR, Y1 = tpY, Y2 = tpY,
            Stroke = new SolidColorBrush(Color.FromArgb(0x50, 0x1A, 0xBC, 0xFE)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection([4, 4])
        });
        refLbl = new TextBlock
        {
            Text = $"Mmax {TorquePeakRPM / 1000}k",
            Foreground = new SolidColorBrush(Color.FromArgb(0x90, 0x1A, 0xBC, 0xFE)),
            FontSize = 9,
            FontFamily = lblFamily
        };
        ChartCanvas.Children.Add(refLbl);
        Canvas.SetLeft(refLbl, w - padR + 4);
        Canvas.SetTop(refLbl, tpY - 8);

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

            // label — gear number at the end
            var lbl = new TextBlock
            {
                Text = $"{i + 1}",
                Foreground = brush,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                FontFamily = lblFamily,
                TextAlignment = TextAlignment.Center
            };
            ChartCanvas.Children.Add(lbl);
            Canvas.SetLeft(lbl, x1 - 12);
            Canvas.SetTop(lbl, y1 - 20);
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
                Fill = i == 0 ? Brushes.White : new SolidColorBrush(Palette[i - 1 % Palette.Length])
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
                    FontSize = 9,
                    FontFamily = lblFamily,
                    TextAlignment = TextAlignment.Center
                };
                ChartCanvas.Children.Add(dropLbl);
                Canvas.SetLeft(dropLbl, targetX + 6);
                Canvas.SetTop(dropLbl, targetY - 10);

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
