using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace Forza_Horizon_6_Tune_Master.Views
{
    /// <summary>
    /// A single "label + dropdown" row used inside the parts accordions.
    /// The whole row hides itself automatically when there are no options
    /// (mirrors the old per-ComboBox CountToVisibility behaviour).
    /// </summary>
    public partial class PartRow : UserControl
    {
        public PartRow()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(PartRow),
                new PropertyMetadata(string.Empty));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty OptionsProperty =
            DependencyProperty.Register(nameof(Options), typeof(IEnumerable), typeof(PartRow),
                new PropertyMetadata(null, OnOptionsChanged));

        public IEnumerable? Options
        {
            get => (IEnumerable?)GetValue(OptionsProperty);
            set => SetValue(OptionsProperty, value);
        }

        public static readonly DependencyProperty SelectedProperty =
            DependencyProperty.Register(nameof(Selected), typeof(object), typeof(PartRow),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public object? Selected
        {
            get => GetValue(SelectedProperty);
            set => SetValue(SelectedProperty, value);
        }

        private static void OnOptionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not PartRow row) return;

            if (e.OldValue is INotifyCollectionChanged oldInpc)
                oldInpc.CollectionChanged -= row.OnOptionsCollectionChanged;
            if (e.NewValue is INotifyCollectionChanged newInpc)
                newInpc.CollectionChanged += row.OnOptionsCollectionChanged;

            row.UpdateVisibility();
        }

        private void OnOptionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => UpdateVisibility();

        private void UpdateVisibility()
        {
            // Show the row only when there is a real choice (more than one option).
            // A single option is always the stock part — nothing to pick, so hide it.
            int count = 0;
            if (Options is IEnumerable items)
            {
                foreach (var _ in items) { count++; if (count > 1) break; }
            }
            Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
