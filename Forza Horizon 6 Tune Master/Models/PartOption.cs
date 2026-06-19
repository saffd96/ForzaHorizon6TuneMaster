namespace Forza_Horizon_6_Tune_Master.Models;

public record PartOption
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = "";
    public bool IsStock { get; init; }
}

/// <summary>Kind of forced induction, used for the two-step FI selector (type → level).</summary>
public enum FiKind { None, SingleTurbo, TwinTurbo, Centrifugal, PositiveDisplacement }

public record FiTypeOption
{
    public FiKind Kind { get; init; }
    public string DisplayName { get; init; } = "";
}

/// <summary>A rim "appearance" option reduced to its mass (kg).</summary>
public record RimMassOption
{
    public double Mass { get; init; }
    public string DisplayName { get; init; } = "";
    public bool IsStock { get; init; }
}
