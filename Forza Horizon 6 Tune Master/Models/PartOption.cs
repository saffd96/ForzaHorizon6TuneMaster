namespace Forza_Horizon_6_Tune_Master.Models;

public record PartOption
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = "";
    public bool IsStock { get; init; }
}
