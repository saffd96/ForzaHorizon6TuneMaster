using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Converters;

public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.Equals(parameter) ?? false;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (bool)value ? parameter : Binding.DoNothing;
}

public class EqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Equals(value, System.Convert.ChangeType(parameter, value?.GetType() ?? typeof(int)));

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => (bool)value ? System.Convert.ChangeType(parameter, targetType) : Binding.DoNothing;
}

public class EqualityVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Equals(value, System.Convert.ChangeType(parameter, value?.GetType() ?? typeof(int)))
            ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value != null ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Multi-value converter for unit-aware display of numeric tune values.
/// values[0] = double (the metric base value)
/// values[1] = UnitSystem | SpringUnit | PowerUnit  (depends on ConverterParameter)
/// ConverterParameter: "pressure" | "spring" | "height" | "speed" | "mass" | "power"
/// </summary>
public class AddOneConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int i ? i + 1 : 0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class UnitValueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not double val) return "—";

        bool imp = values[1] is UnitSystem us ? us == UnitSystem.Imperial : values[1] is bool b && b;
        SpringUnit su = values[1] is SpringUnit sv ? sv : SpringUnit.KgfMm;
        PowerUnit  pu = values[1] is PowerUnit  pv ? pv : PowerUnit.HP;

        return (parameter as string) switch
        {
            "pressure" => imp ? $"{val * 14.504:F1} psi"      : $"{val:F2} бар",
            "spring"   => su == SpringUnit.KgfMm   ? $"{val / 9.807:F2} кгс/мм"
                        : su == SpringUnit.LbsIn  ? $"{val * 5.710:F1} фнт/дюйм"
                        :                           $"{val:F1} Н/мм",
            "height"   => imp ? $"{val / 25.4:F2}\"" : $"{val:F0} мм",
            "speed"    => imp ? $"{val * 0.6214:F0} миль/ч" : $"{val:F0} км/ч",
            "mass"     => imp ? $"{val * 2.2046:F0} фнт"    : $"{val:F0} кг",
            "power"    => pu == PowerUnit.KW ? $"{val * 0.7457:F0} кВт"
                        : pu == PowerUnit.PS ? $"{val * 1.01387:F0} PS"
                        :                      $"{val:F0} л.с.",
            _          => $"{val:F1}"
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public static class NumericBehavior
{
    public static readonly DependencyProperty IsNumericProperty =
        DependencyProperty.RegisterAttached(
            "IsNumeric", typeof(bool), typeof(NumericBehavior),
            new PropertyMetadata(false, OnIsNumericChanged));

    public static void SetIsNumeric(DependencyObject element, bool value)
        => element.SetValue(IsNumericProperty, value);

    public static bool GetIsNumeric(DependencyObject element)
        => (bool)element.GetValue(IsNumericProperty);

    private static void OnIsNumericChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        if ((bool)e.NewValue)
        {
            tb.PreviewTextInput += OnPreviewTextInput;
            tb.TextChanged += OnTextChanged;
        }
        else
        {
            tb.PreviewTextInput -= OnPreviewTextInput;
            tb.TextChanged -= OnTextChanged;
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var tb = (TextBox)sender;
        var proposed = tb.Text[..tb.CaretIndex] + e.Text + tb.Text[tb.CaretIndex..];
        if (!IsValidNumberProposal(proposed))
            e.Handled = true;
    }

    private static void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var tb = (TextBox)sender;
        var text = tb.Text;
        if (text.IndexOf(',') >= 0)
            text = text.Replace(",", ".");

        var clean = new StringBuilder(text.Length);
        bool dotSeen = false;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '.') { if (!dotSeen) { dotSeen = true; clean.Append(c); } }
            else if (c == '-' && i == 0) clean.Append(c);
            else if (char.IsAsciiDigit(c)) clean.Append(c);
        }
        var result = clean.ToString();
        if (result != tb.Text)
        {
            tb.Text = result;
            tb.CaretIndex = result.Length;
        }
    }

    private static bool IsValidNumberProposal(string s)
    {
        if (s.Length == 0) return true;
        int dots = 0, signs = 0;
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '.')
            {
                if (++dots > 1) return false;
            }
            else if (c == '-' || c == '+')
            {
                if (++signs > 1) return false;
                if (i != 0) return false;
            }
            else if (c is ',' or ' ')
            {
                return false;
            }
            else if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }
        return true;
    }

}

public class PowertrainTypeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is PowertrainType pt ? pt switch
        {
            PowertrainType.ICE      => "ДВС (традиционный)",
            PowertrainType.Hybrid   => "Гибрид (ДВС + электромотор)",
            PowertrainType.Electric => "Электромобиль (BEV)",
            _                       => value.ToString() ?? ""
        } : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class AspirationTypeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is AspirationType asp ? asp switch
        {
            AspirationType.Natural               => "Атмосферный",
            AspirationType.SingleTurbo           => "Одиночная турбина",
            AspirationType.TwinTurbo             => "Двойной турбонаддув",
            AspirationType.PositiveDisplacement  => "Объёмный компрессор",
            AspirationType.Centrifugal           => "Центробежный компрессор",
            AspirationType.Electric              => "Электро",
            _                                    => value.ToString() ?? ""
        } : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
