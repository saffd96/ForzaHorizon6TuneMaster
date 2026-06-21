using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Forza_Horizon_6_Tune_Master.Views
{
    /// <summary>
    /// One result parameter rendered as a compact "label — value — progress" row,
    /// matching the web design. ValueText/Progress are bound (often via MultiBinding)
    /// by the consumer; Hint feeds the tooltip.
    /// When a value changes the number "counts up" to its target and the progress
    /// bar fills in smoothly, so freshly generated tunes appear animated.
    /// </summary>
    public partial class ResultParam : UserControl
    {
        private static readonly Duration AnimDuration = new Duration(TimeSpan.FromMilliseconds(550));

        // Splits the value into [prefix][number][suffix], e.g. "120% RWD" or "32.5 psi" or "-2.0°".
        private static readonly Regex NumberRegex =
            new Regex(@"^(?<pre>[^\d\-+]*)(?<num>[-+]?\d(?:[\d  .,]*\d)?)(?<post>.*)$",
                      RegexOptions.Compiled | RegexOptions.Singleline);

        private string _displayedValue = string.Empty;

        public ResultParam()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(ResultParam),
                new PropertyMetadata(string.Empty));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty ValueTextProperty =
            DependencyProperty.Register(nameof(ValueText), typeof(string), typeof(ResultParam),
                new PropertyMetadata(string.Empty, OnValueTextChanged));

        public string ValueText
        {
            get => (string)GetValue(ValueTextProperty);
            set => SetValue(ValueTextProperty, value);
        }

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ResultParam),
                new PropertyMetadata(0.0, OnProgressChanged));

        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        public static readonly DependencyProperty HintProperty =
            DependencyProperty.Register(nameof(Hint), typeof(object), typeof(ResultParam),
                new PropertyMetadata(null));

        public object? Hint
        {
            get => GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }

        private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((ResultParam)d).AnimateProgress((double)e.NewValue);

        private static void OnValueTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((ResultParam)d).AnimateValue((string)(e.NewValue ?? string.Empty));

        private void AnimateProgress(double target)
        {
            if (double.IsNaN(target) || double.IsInfinity(target)) target = 0;
            target = Math.Max(0, Math.Min(1, target));

            var anim = new DoubleAnimation
            {
                To = target,
                Duration = AnimDuration,
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Bar.BeginAnimation(ProgressBar.ValueProperty, anim);
        }

        private void AnimateValue(string newValue)
        {
            newValue ??= string.Empty;

            var from = NumberRegex.Match(_displayedValue ?? string.Empty);
            var to = NumberRegex.Match(newValue);

            // Count up only when both old and new strings share the same prefix/suffix
            // shape; otherwise just pop the new text in.
            if (to.Success &&
                TryParseNumber(to.Groups["num"].Value, out double toNum))
            {
                double fromNum = 0;
                if (from.Success &&
                    from.Groups["pre"].Value == to.Groups["pre"].Value &&
                    from.Groups["post"].Value == to.Groups["post"].Value &&
                    TryParseNumber(from.Groups["num"].Value, out double parsedFrom))
                {
                    fromNum = parsedFrom;
                }

                int decimals = DecimalsOf(to.Groups["num"].Value);
                string pre = to.Groups["pre"].Value;
                string post = to.Groups["post"].Value;

                // Drive the text per-frame from a clock; ease the progress ourselves.
                var counter = new DoubleAnimation { From = 0, To = 1, Duration = AnimDuration };
                var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
                var clock = counter.CreateClock();
                clock.CurrentTimeInvalidated += (s, _) =>
                {
                    double p = (s as AnimationClock)?.CurrentProgress ?? 1.0;
                    double v = fromNum + (toNum - fromNum) * ease.Ease(p);
                    ValueBlock.Text = pre + v.ToString("F" + decimals, CultureInfo.CurrentCulture) + post;
                };
                clock.Completed += (_, __) => ValueBlock.Text = newValue;
                clock.Controller?.Begin();
            }
            else
            {
                ValueBlock.Text = newValue;
            }

            _displayedValue = newValue;
            PlayPop();
        }

        // Small fade + slide-up so the value visibly "lands".
        private void PlayPop()
        {
            var fade = new DoubleAnimation
            {
                From = 0.35,
                To = 1.0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300))
            };
            ValueBlock.BeginAnimation(OpacityProperty, fade);

            var slide = new DoubleAnimation
            {
                From = 6,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ValueShift.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        private static bool TryParseNumber(string s, out double value)
        {
            // Strip group separators/spaces so "1 850" / "1,850" parse cleanly.
            s = s.Replace(" ", string.Empty).Replace(" ", string.Empty);
            return double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out value)
                || double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private static int DecimalsOf(string num)
        {
            int dot = num.LastIndexOfAny(new[] { '.', ',' });
            if (dot < 0) return 0;
            int count = 0;
            for (int i = dot + 1; i < num.Length; i++)
                if (char.IsDigit(num[i])) count++;
            return count;
        }
    }
}
