using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;

namespace Forza_Horizon_6_Tune_Master.Converters;

public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value?.Equals(parameter) ?? false;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? parameter : Binding.DoNothing;
}

public class EqualityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        try { return Equals(value, System.Convert.ChangeType(parameter, value.GetType())); }
        catch { return Equals(value, parameter); }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool b || !b) return Binding.DoNothing;
        if (targetType == null || parameter == null) return Binding.DoNothing;
        try { return System.Convert.ChangeType(parameter, targetType); }
        catch { return parameter; }
    }
}

public class EqualityVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null) return Visibility.Collapsed;
        try { return Equals(value, System.Convert.ChangeType(parameter, value.GetType()))
            ? Visibility.Visible : Visibility.Collapsed; }
        catch { return Equals(value, parameter) ? Visibility.Visible : Visibility.Collapsed; }
    }

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

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : value;
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
        if (values == null || values.Length < 2 || values[0] is not double val) return "—";

        var v1 = values[1];
        if (v1 == DependencyProperty.UnsetValue)
            v1 = UnitSystem.Metric;
        bool imp = v1 is UnitSystem us ? us == UnitSystem.Imperial : v1 is bool b && b;
        SpringUnit su = v1 is SpringUnit sv ? sv : SpringUnit.KgfMm;
        PowerUnit  pu = v1 is PowerUnit  pv ? pv : PowerUnit.HP;

        var loc = LocalizationService.Instance;
        return (parameter as string) switch
        {
            "pressure" => imp ? $"{val * 14.504:F1} {loc.T("UnitPsi")}"      : $"{val:F2} {loc.T("UnitBar")}",
            "spring"   => su == SpringUnit.KgfMm   ? $"{val / 9.807:F2} {loc.T("UnitKgfMm")}"
                        : su == SpringUnit.LbsIn  ? $"{val * 5.710:F1} {loc.T("UnitLbsInch")}"
                        :                           $"{val:F1} {loc.T("UnitNmm")}",
            "height"   => imp ? $"{val / 25.4:F2}{loc.T("UnitInch")}" : $"{val:F0} {loc.T("UnitMm")}",
            "speed"    => imp ? $"~{val * 0.6214:F0} {loc.T("UnitMph")}" : $"~{val:F0} {loc.T("UnitKmh")}",
            "mass"     => imp ? $"{val * 2.2046:F0} {loc.T("UnitLb")}"    : $"{val:F0} {loc.T("UnitKg")}",
            "power"    => pu == PowerUnit.KW ? $"{val * 0.7457:F0} {loc.T("UnitKw")}"
                        : pu == PowerUnit.PS ? $"{val * 1.01387:F0} {loc.T("UnitPs")}"
                        :                      $"{val:F0} {loc.T("UnitHp")}",
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

        var clean = new StringBuilder(text.Length);
        bool sepSeen = false;
        for (int i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '.' or ',') { if (!sepSeen) { sepSeen = true; clean.Append('.'); } }
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
        int dots = 0, signs = 0, digits = 0;
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c is '.' or ',')
            {
                if (++dots > 1) return false;
            }
            else if (c == '-' || c == '+')
            {
                if (++signs > 1) return false;
                if (i != 0) return false;
            }
            else if (c == ' ')
            {
                return false;
            }
            else if (!char.IsAsciiDigit(c))
            {
                return false;
            }
            else
            {
                digits++;
            }
        }
        return digits > 0 || signs == 0;
    }

}

public class GenericEnumLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null) return "";
        var type = value.GetType();
        if (!type.IsEnum) return value.ToString() ?? "";

        var key = $"Enum_{type.Name}_{value}";
        var t = LocalizationService.Instance;
        if (t.TryGet(key, out var label))
            return label;
        return value.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class PowertrainTypeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is PowertrainType pt
            ? LocalizationService.Instance.T($"Enum_PowertrainType_{pt}")
            : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class AspirationTypeLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is AspirationType asp
            ? LocalizationService.Instance.T($"Enum_AspirationType_{asp}")
            : "—";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class DateDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is DateTime dt
            ? dt.ToString(LocalizationService.Instance.T("ResultCreatedAtFormat"))
            : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
