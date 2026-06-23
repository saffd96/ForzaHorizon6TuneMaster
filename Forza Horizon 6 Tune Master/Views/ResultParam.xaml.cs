using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Forza_Horizon_6_Tune_Master.Views
{
    /// <summary>
    /// One result parameter rendered as a compact "label — value — progress" row,
    /// matching the web design. ValueText/Progress are bound (often via MultiBinding)
    /// by the consumer; Hint feeds the tooltip.
    /// The progress bar still animates smoothly; the numeric value is applied directly
    /// (no count-up): the old animated value path ran overlapping AnimationClocks, so a
    /// fast unit switch could let a stale clock write back the previous value after the
    /// new one — making the displayed number "stick".
    /// </summary>
    public partial class ResultParam : UserControl
    {
        private static readonly Duration AnimDuration = new Duration(TimeSpan.FromMilliseconds(550));

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

        // Numeric value is set directly — no count-up animation (see class summary).
        private static void OnValueTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((ResultParam)d).ValueBlock.Text = (string)(e.NewValue ?? string.Empty);

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
    }
}
