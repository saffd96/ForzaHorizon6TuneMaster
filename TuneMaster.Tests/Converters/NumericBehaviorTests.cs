using System.Reflection;
using System.Windows.Controls;
using Forza_Horizon_6_Tune_Master.Converters;

namespace TuneMaster.Tests.Converters;

public class NumericBehaviorTests
{
    private static bool IsValidNumberProposal(string s)
    {
        var method = typeof(NumericBehavior).GetMethod("IsValidNumberProposal",
            BindingFlags.NonPublic | BindingFlags.Static);
        return (bool)method!.Invoke(null, new object[] { s })!;
    }

    [Fact]
    public void IsValid_EmptyString()
    {
        Assert.True(IsValidNumberProposal(""));
    }

    [Fact]
    public void IsValid_SingleDigit()
    {
        Assert.True(IsValidNumberProposal("5"));
    }

    [Fact]
    public void IsValid_MultiDigit()
    {
        Assert.True(IsValidNumberProposal("12345"));
    }

    [Fact]
    public void IsValid_Decimal()
    {
        Assert.True(IsValidNumberProposal("123.45"));
    }

    [Fact]
    public void IsValid_CommaAsSeparator()
    {
        Assert.True(IsValidNumberProposal("123,45"));
    }

    [Fact]
    public void IsValid_NegativeNumber()
    {
        Assert.True(IsValidNumberProposal("-5"));
    }

    [Fact]
    public void IsValid_NegativeDecimal()
    {
        Assert.True(IsValidNumberProposal("-123.45"));
    }

    [Fact]
    public void IsValid_PositiveSign()
    {
        Assert.True(IsValidNumberProposal("+5"));
    }

    [Fact]
    public void IsValid_Zero()
    {
        Assert.True(IsValidNumberProposal("0"));
    }

    [Fact]
    public void IsValid_TrailingDot()
    {
        Assert.True(IsValidNumberProposal("123."));
    }

    [Fact]
    public void IsInvalid_DoubleDot()
    {
        Assert.False(IsValidNumberProposal("1.2.3"));
    }

    [Fact]
    public void IsInvalid_DoubleComma()
    {
        Assert.False(IsValidNumberProposal("1,2,3"));
    }

    [Fact]
    public void IsInvalid_DotAndComma()
    {
        Assert.False(IsValidNumberProposal("1.2,3"));
    }

    [Fact]
    public void IsInvalid_LetterInNumber()
    {
        Assert.False(IsValidNumberProposal("12a34"));
    }

    [Fact]
    public void IsInvalid_SpaceInNumber()
    {
        Assert.False(IsValidNumberProposal("12 34"));
    }

    [Fact]
    public void IsInvalid_SignInMiddle()
    {
        Assert.False(IsValidNumberProposal("123-45"));
    }

    [Fact]
    public void IsInvalid_DoubleSign()
    {
        Assert.False(IsValidNumberProposal("--5"));
    }

    [Fact]
    public void IsInvalid_SpecialChars()
    {
        Assert.False(IsValidNumberProposal("12#34"));
    }

    [Fact]
    public void IsValid_LeadingDot()
    {
        Assert.True(IsValidNumberProposal(".5"));
    }

    [Fact]
    public void IsInvalid_OnlyDot()
    {
        Assert.True(IsValidNumberProposal("."));
    }

    [Fact]
    public void IsValid_MultipleDigitsAfterDot()
    {
        Assert.True(IsValidNumberProposal("3.14159"));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("0.5")]
    [InlineData("100.0")]
    [InlineData("3.14159")]
    [InlineData("999999")]
    [InlineData("-999.999")]
    public void IsValid_AllCommonFormats(string input)
    {
        Assert.True(IsValidNumberProposal(input));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1 2")]
    [InlineData("1..2")]
    [InlineData("1,,2")]
    [InlineData("1.2.3")]
    [InlineData("1-2")]
    [InlineData("-")]
    [InlineData("+")]
    [InlineData("12/34")]
    [InlineData("12:34")]
    public void IsInvalid_AllCommonRejections(string input)
    {
        Assert.False(IsValidNumberProposal(input));
    }

    [Fact]
    public void SetIsNumeric_OnTextBox_DoesNotThrow()
    {
        var tb = new TextBox();
        NumericBehavior.SetIsNumeric(tb, true);
        Assert.True(NumericBehavior.GetIsNumeric(tb));
    }

    [Fact]
    public void SetIsNumeric_OnNonTextBox_DoesNotThrow()
    {
        var btn = new System.Windows.Controls.Button();
        NumericBehavior.SetIsNumeric(btn, true);
        Assert.True(NumericBehavior.GetIsNumeric(btn));
    }

    [Fact]
    public void SetIsNumeric_DefaultIsFalse()
    {
        var tb = new TextBox();
        Assert.False(NumericBehavior.GetIsNumeric(tb));
    }
}
