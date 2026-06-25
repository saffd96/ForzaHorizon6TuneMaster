using System.Windows;
using Forza_Horizon_6_Tune_Master.Converters;
using Forza_Horizon_6_Tune_Master.Models;

namespace TuneMaster.Tests.Converters;

public class EnumToBoolConverterTests
{
    private readonly EnumToBoolConverter _conv = new();

    [Fact]
    public void Convert_SameValue_ReturnsTrue()
    {
        var result = _conv.Convert(Discipline.Road, null, Discipline.Road, null);
        Assert.True((bool)result!);
    }

    [Fact]
    public void Convert_DifferentValue_ReturnsFalse()
    {
        var result = _conv.Convert(Discipline.Road, null, Discipline.Rally, null);
        Assert.False((bool)result!);
    }

    [Fact]
    public void Convert_NullValue_ReturnsFalse()
    {
        var result = _conv.Convert(null, null, Discipline.Road, null);
        Assert.False((bool)result!);
    }

    [Fact]
    public void ConvertBack_True_ReturnsParameter()
    {
        var result = _conv.ConvertBack(true, typeof(Discipline), Discipline.Rally, null);
        Assert.Equal(Discipline.Rally, result);
    }

    [Fact]
    public void ConvertBack_False_ReturnsBindingDoNothing()
    {
        var result = _conv.ConvertBack(false, typeof(Discipline), Discipline.Rally, null);
        Assert.Equal(System.Windows.Data.Binding.DoNothing, result);
    }
}

public class EqualityConverterTests
{
    private readonly EqualityConverter _conv = new();

    [Fact]
    public void Convert_EqualValues_ReturnsTrue()
    {
        var result = _conv.Convert(42, null, "42", null);
        Assert.True((bool)result!);
    }

    [Fact]
    public void ConvertBack_True_ReturnsParameter()
    {
        var result = _conv.ConvertBack(true, typeof(int), "42", null);
        Assert.Equal(42, result);
    }

    [Fact]
    public void ConvertBack_False_ReturnsBindingDoNothing()
    {
        var result = _conv.ConvertBack(false, typeof(int), "42", null);
        Assert.Equal(System.Windows.Data.Binding.DoNothing, result);
    }
}

public class EqualityVisibilityConverterTests
{
    private readonly EqualityVisibilityConverter _conv = new();

    [Fact]
    public void Convert_Equal_Visible()
    {
        var result = _conv.Convert(Discipline.Road, null, Discipline.Road, null);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_NotEqual_Collapsed()
    {
        var result = _conv.Convert(Discipline.Road, null, Discipline.Rally, null);
        Assert.Equal(Visibility.Collapsed, result);
    }
}

public class NullToVisibilityConverterTests
{
    private readonly NullToVisibilityConverter _conv = new();

    [Fact]
    public void Convert_NonNull_Visible()
    {
        var result = _conv.Convert("hello", null, null, null);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_Null_Collapsed()
    {
        var result = _conv.Convert(null, null, null, null);
        Assert.Equal(Visibility.Collapsed, result);
    }
}

public class BoolToVisibilityConverterTests
{
    private readonly BoolToVisibilityConverter _conv = new();

    [Fact]
    public void Convert_True_Visible()
    {
        var result = _conv.Convert(true, null, null, null);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void Convert_False_Collapsed()
    {
        var result = _conv.Convert(false, null, null, null);
        Assert.Equal(Visibility.Collapsed, result);
    }
}

public class InverseBoolToVisibilityConverterTests
{
    private readonly InverseBoolToVisibilityConverter _conv = new();

    [Fact]
    public void Convert_True_Collapsed()
    {
        var result = _conv.Convert(true, null, null, null);
        Assert.Equal(Visibility.Collapsed, result);
    }

    [Fact]
    public void Convert_False_Visible()
    {
        var result = _conv.Convert(false, null, null, null);
        Assert.Equal(Visibility.Visible, result);
    }
}

public class InverseBoolConverterTests
{
    private readonly InverseBoolConverter _conv = new();

    [Fact]
    public void Convert_True_False()
    {
        var result = _conv.Convert(true, null, null, null);
        Assert.False((bool)result!);
    }

    [Fact]
    public void Convert_False_True()
    {
        var result = _conv.Convert(false, null, null, null);
        Assert.True((bool)result!);
    }

    [Fact]
    public void ConvertBack_True_False()
    {
        var result = _conv.ConvertBack(true, null, null, null);
        Assert.False((bool)result!);
    }
}

public class AddOneConverterTests
{
    private readonly AddOneConverter _conv = new();

    [Fact]
    public void Convert_Int_AddsOne()
    {
        var result = _conv.Convert(5, null, null, null);
        Assert.Equal(6, result);
    }

    [Fact]
    public void Convert_NonInt_ReturnsZero()
    {
        var result = _conv.Convert("hello", null, null, null);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Convert_Null_ReturnsZero()
    {
        var result = _conv.Convert(null, null, null, null);
        Assert.Equal(0, result);
    }
}

public class GenericEnumLabelConverterTests
{
    private readonly GenericEnumLabelConverter _conv = new();

    [Fact]
    public void Convert_Null_ReturnsEmpty()
    {
        var result = _conv.Convert(null, null, null, null);
        Assert.Equal("", result);
    }

    [Fact]
    public void Convert_NonEnum_ReturnsToString()
    {
        var result = _conv.Convert(123, null, null, null);
        Assert.Equal("123", result);
    }

    [Fact]
    public void Convert_Enum_ReturnsLocalizationKey()
    {
        var result = _conv.Convert(Discipline.Road, null, null, null);
        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }
}

public class PowertrainTypeLabelConverterTests
{
    private readonly PowertrainTypeLabelConverter _conv = new();

    [Fact]
    public void Convert_NonPowertrain_ReturnsEmpty()
    {
        var result = _conv.Convert("hello", null, null, null);
        Assert.Equal("", result);
    }

    [Fact]
    public void Convert_Powertrain_ReturnsLabel()
    {
        var result = _conv.Convert(PowertrainType.ICE, null, null, null);
        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }
}

public class AspirationTypeLabelConverterTests
{
    private readonly AspirationTypeLabelConverter _conv = new();

    [Fact]
    public void Convert_Null_ReturnsDash()
    {
        var result = _conv.Convert(null, null, null, null);
        Assert.Equal("\u2014", result);
    }

    [Fact]
    public void Convert_Aspiration_ReturnsLabel()
    {
        var result = _conv.Convert(AspirationType.TwinTurbo, null, null, null);
        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }
}

public class DateDisplayConverterTests
{
    private readonly DateDisplayConverter _conv = new();

    [Fact]
    public void Convert_DateTime_ReturnsFormatted()
    {
        var dt = new DateTime(2024, 6, 15, 14, 30, 0);
        var result = _conv.Convert(dt, null, null, null);
        Assert.NotNull(result);
        Assert.NotEmpty((string)result);
    }

    [Fact]
    public void Convert_NonDateTime_ReturnsEmpty()
    {
        var result = _conv.Convert("hello", null, null, null);
        Assert.Equal("", result);
    }

    [Fact]
    public void Convert_Null_ReturnsEmpty()
    {
        var result = _conv.Convert(null, null, null, null);
        Assert.Equal("", result);
    }
}

public class UnitValueConverterTests
{
    private readonly UnitValueConverter _conv = new();

    [Fact]
    public void Convert_FewerThan2Values_ReturnsDash()
    {
        var result = _conv.Convert(new object[] { 2.5 }, null, "pressure", null);
        Assert.Equal("\u2014", result);
    }

    [Fact]
    public void Convert_NonDoubleFirst_ReturnsDash()
    {
        var result = _conv.Convert(new object[] { "hello", UnitSystem.Metric }, null, "pressure", null);
        Assert.Equal("\u2014", result);
    }

    [Fact]
    public void Convert_Pressure_Metric()
    {
        var result = _conv.Convert(new object[] { 2.5, UnitSystem.Metric }, null, "pressure", null);
        Assert.Contains("2,50", (string)result);

    }

    [Fact]
    public void Convert_Pressure_Imperial()
    {
        var result = _conv.Convert(new object[] { 2.5, UnitSystem.Imperial }, null, "pressure", null);
        Assert.Contains("36,3", (string)result);

    }

    [Fact]
    public void Convert_Height_Metric()
    {
        var result = _conv.Convert(new object[] { 150.0, UnitSystem.Metric }, null, "height", null);
        Assert.Contains("150", (string)result);

    }

    [Fact]
    public void Convert_Height_Imperial()
    {
        var result = _conv.Convert(new object[] { 150.0, UnitSystem.Imperial }, null, "height", null);
        Assert.Contains("5,91", (string)result);

    }

    [Fact]
    public void Convert_Speed_Metric()
    {
        var result = _conv.Convert(new object[] { 250.0, UnitSystem.Metric }, null, "speed", null);
        Assert.Contains("250", (string)result);

    }

    [Fact]
    public void Convert_Speed_Imperial()
    {
        var result = _conv.Convert(new object[] { 250.0, UnitSystem.Imperial }, null, "speed", null);
        Assert.Contains("155", (string)result);

    }

    [Fact]
    public void Convert_Mass_Metric()
    {
        var result = _conv.Convert(new object[] { 1400.0, UnitSystem.Metric }, null, "mass", null);
        Assert.Contains("1400", (string)result);

    }

    [Fact]
    public void Convert_Mass_Imperial()
    {
        var result = _conv.Convert(new object[] { 1400.0, UnitSystem.Imperial }, null, "mass", null);
        Assert.Contains("3086", (string)result);

    }

    [Fact]
    public void Convert_Power_HP()
    {
        var result = _conv.Convert(new object[] { 300.0, PowerUnit.HP }, null, "power", null);
        Assert.Contains("300", (string)result);

    }

    [Fact]
    public void Convert_Power_KW()
    {
        var result = _conv.Convert(new object[] { 300.0, PowerUnit.KW }, null, "power", null);
        Assert.Contains("224", (string)result);

    }

    [Fact]
    public void Convert_Power_PS()
    {
        var result = _conv.Convert(new object[] { 300.0, PowerUnit.PS }, null, "power", null);
        Assert.Contains("304", (string)result);

    }

    [Fact]
    public void Convert_Spring_KgfMm()
    {
        var result = _conv.Convert(new object[] { 100.0, SpringUnit.KgfMm }, null, "spring", null);
        Assert.Contains("101,97", (string)result);

    }

    [Fact]
    public void Convert_Spring_NMm()
    {
        var result = _conv.Convert(new object[] { 100.0, SpringUnit.NMm }, null, "spring", null);
        Assert.Contains("1000,0", (string)result);

    }

    [Fact]
    public void Convert_Spring_LbsIn()
    {
        var result = _conv.Convert(new object[] { 100.0, SpringUnit.LbsIn }, null, "spring", null);
        Assert.Contains("571", (string)result);

    }

    [Fact]
    public void Convert_DefaultParameter_ReturnsFormatted()
    {
        var result = _conv.Convert(new object[] { 42.5, UnitSystem.Metric }, null, null, null);
        Assert.Equal("42,5", result);

    }

    [Fact]
    public void Convert_BoolUnitSystem_Imperial()
    {
        var result = _conv.Convert(new object[] { 2.5, true }, null, "pressure", null);
        Assert.Contains("36,3", (string)result);

    }
}
