using Forza_Horizon_6_Tune_Master.Models;

namespace TuneMaster.Tests.Models;

public class LanguageOptionTests
{
    [Fact]
    public void ToString_ReturnsLabel()
    {
        var opt = new LanguageOption { Code = "en", Label = "English" };
        Assert.Equal("English", opt.ToString());
    }

    [Fact]
    public void PropertyChanged_RaisedOnLabelChange()
    {
        var opt = new LanguageOption();
        var changed = false;
        opt.PropertyChanged += (_, e) => { if (e.PropertyName == "Label") changed = true; };

        opt.Label = "New Label";

        Assert.True(changed);
    }
}
