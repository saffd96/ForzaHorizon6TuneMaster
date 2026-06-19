using System.Windows;
using System.Windows.Controls;

namespace Forza_Horizon_6_Tune_Master.Views
{
    /// <summary>
    /// One result parameter rendered as a compact "label — value — progress" row,
    /// matching the web design. ValueText/Progress are bound (often via MultiBinding)
    /// by the consumer; Hint feeds the tooltip.
    /// </summary>
    public partial class ResultParam : UserControl
    {
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
                new PropertyMetadata(string.Empty));

        public string ValueText
        {
            get => (string)GetValue(ValueTextProperty);
            set => SetValue(ValueTextProperty, value);
        }

        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register(nameof(Progress), typeof(double), typeof(ResultParam),
                new PropertyMetadata(0.0));

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
    }
}
