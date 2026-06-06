using Forza_Horizon_6_Tune_Master.Models;

namespace TuneMaster.Tests.Models;

public class NotifyBaseTests
{
    private class TestModel : NotifyBase
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set => Set(ref _name, value);
        }

        private int _value;
        public int Value
        {
            get => _value;
            set => Set(ref _value, value);
        }
    }

    [Fact]
    public void Set_RaisesPropertyChanged()
    {
        var model = new TestModel();
        var changed = new List<string>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        model.Name = "Hello";

        Assert.Contains("Name", changed);
        Assert.Equal("Hello", model.Name);
    }

    [Fact]
    public void Set_SameValue_DoesNotRaise()
    {
        var model = new TestModel();
        model.Name = "Hello";
        var changed = new List<string>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        model.Name = "Hello";

        Assert.Empty(changed);
    }

    [Fact]
    public void Set_MultipleProperties_RaisesForEach()
    {
        var model = new TestModel();
        var changed = new List<string>();
        model.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        model.Name = "A";
        model.Value = 42;

        Assert.Contains("Name", changed);
        Assert.Contains("Value", changed);
    }
}
