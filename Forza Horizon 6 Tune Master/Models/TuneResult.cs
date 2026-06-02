namespace Forza_Horizon_6_Tune_Master.Models;

public class TuneResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ProfileName { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public CarCard Car { get; set; } = new();
    public TrackInfo Track { get; set; } = new();

    // Tire Pressure (bar)
    public double TirePressureFront { get; set; }
    public double TirePressureRear { get; set; }

    // Alignment
    public double CamberFront { get; set; }
    public double CamberRear { get; set; }
    public double ToeFront { get; set; }
    public double ToeRear { get; set; }
    public double Caster { get; set; }

    // ARB (1–65)
    public double ARBFront { get; set; }
    public double ARBRear { get; set; }

    // Springs (kgf/mm)
    public double SpringFront { get; set; }
    public double SpringRear { get; set; }

    // Ride Height (mm)
    public double RideHeightFront { get; set; }
    public double RideHeightRear { get; set; }

    // Dampers Rebound (1–20)
    public double ReboundFront { get; set; }
    public double ReboundRear { get; set; }

    // Dampers Bump (1–20)
    public double BumpFront { get; set; }
    public double BumpRear { get; set; }

    // Aero (kg downforce)
    public double AeroFront { get; set; }
    public double AeroRear { get; set; }

    // Differential (%)
    public double DiffAccel { get; set; }
    public double DiffDecel { get; set; }
    // AWD-only: front differential
    public double? DiffFrontAccel { get; set; }
    public double? DiffFrontDecel { get; set; }
    // AWD-only: centre diff bias (0 = front, 100 = rear)
    public double? CenterDiffBias { get; set; }

    // Brakes
    public double BrakeBalance { get; set; }
    public double BrakePressure { get; set; }

    // Gearing
    public double FinalDrive { get; set; }
    public List<double> GearRatios { get; set; } = new();
    public int RecommendedGearCount { get; set; }

    // Launch Control (Drag only)
    public double? LaunchControlRpm { get; set; }

    // Explanations
    public Dictionary<string, string> Explanations { get; set; } = new();
}
