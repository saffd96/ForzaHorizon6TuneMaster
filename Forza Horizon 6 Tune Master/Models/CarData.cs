namespace Forza_Horizon_6_Tune_Master.Models;

public class CarData
{
    public int Year { get; set; }
    public string Make { get; set; } = "";
    public string Model { get; set; } = "";
    public string DisplayName => $"{Year} {Make} {Model}";
}
