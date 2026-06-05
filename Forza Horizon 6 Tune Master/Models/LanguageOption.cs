namespace Forza_Horizon_6_Tune_Master.Models;

public class LanguageOption : NotifyBase
{
    public string Code { get; set; } = "";

    private string _label = "";
    public string Label
    {
        get => _label;
        set { _label = value; OnPropertyChanged(); }
    }

    public override string ToString() => Label;
}
