namespace Forza_Horizon_6_Tune_Master.Models;

public record PartOption
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = "";
    public bool IsStock { get; init; }
    public double Mass { get; init; }
    public string Manufacturer { get; init; } = "";
}

/// <summary>Kind of forced induction, used for the two-step FI selector (type → level).</summary>
public enum FiKind { None, SingleTurbo, TwinTurbo, Centrifugal, PositiveDisplacement }

public record FiTypeOption
{
    public FiKind Kind { get; init; }
    public string DisplayName { get; init; } = "";
}
