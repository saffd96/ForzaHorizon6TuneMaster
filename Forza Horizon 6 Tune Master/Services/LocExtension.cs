using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Forza_Horizon_6_Tune_Master.Services;

public class LocExtension : MarkupExtension
{
    public string? Key { get; set;  }
    public string? Format { get; set; }

    public LocExtension() { }

    public LocExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (string.IsNullOrEmpty(Key))
            return "[KeyMissing]";

        var svc = LocalizationService.Instance;

        var binding = new Binding($"Item[{Key}]")
        {
            Source = svc,
            Mode = BindingMode.OneWay,
            FallbackValue = Key
        };

        if (!string.IsNullOrEmpty(Format))
            binding.StringFormat = Format;

        return binding.ProvideValue(serviceProvider);
    }
}
