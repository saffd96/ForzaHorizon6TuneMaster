using System.Text.Json.Serialization;

namespace Forza_Horizon_6_Tune_Master.Services;

#pragma warning disable CS8618

public abstract record DbUpgradePart
{
    public int Id { get; init; }
    public int Level { get; init; }
    public bool IsStock { get; init; }
    public int ManufacturerID { get; init; }
    public double MassDiff { get; init; }
    public int Price { get; init; }
    public double? WeightDistDiff { get; init; }
    public double? DragScale { get; init; }
    public double? TorqueScale { get; init; }
    public double? WindInstabilityScale { get; init; }
}

public record DbCar
{
    public int Id { get; init; }
    public int Year { get; init; }
    public int MakeID { get; init; }
    public string DisplayName { get; init; }
    public string ModelShort { get; init; }
    public string MediaName { get; init; }
    public int ClassID { get; init; }
    public int EnginePlacementID { get; init; }
    public int PowertrainID { get; init; }
    public double CurbWeight { get; init; }
    public double WeightDistribution { get; init; }
    public int NumGears { get; init; }
    public int DriveTypeID { get; init; }
    public int FrontTireWidthMM { get; init; }
    public int FrontTireAspect { get; init; }
    public int FrontWheelDiameterIN { get; init; }
    public int RearTireWidthMM { get; init; }
    public int RearTireAspect { get; init; }
    public int RearWheelDiameterIN { get; init; }
    public string MakeName { get; init; }
    public double SimPeakPower { get; init; }
    public double SimPeakAngVel { get; init; }
    public double SimPeakTorque { get; init; }
    public double SimPeakTorqueAngVel { get; init; }
    public double SimRedlineAngVel { get; init; }
    public double GameTorqueScale { get; init; }
    public double BodyAeroLongitudinalDrag { get; init; }
    public double BodyAeroForwardDownforceFront { get; init; }
    public double BodyAeroForwardDownforceRear { get; init; }
    public int Displacement { get; init; }
    public int CylinderID { get; init; }
    public int AspirationTypeId { get; init; }
    public double FrontStockRideHeight { get; init; }
    public double RearStockRideHeight { get; init; }
    public int PI { get; init; }

    // Aero / top-speed helpers
    public double GameDragScale { get; init; }
    public double GameDownforceScale { get; init; }
    public double GameDownforceScaleOffroad { get; init; }
    public double FrontDownforceClampKG { get; init; }
    public double RearDownforceClampKG { get; init; }
    public double TopSpeedMph { get; init; }
    public double SimTopSpeed { get; init; }
    public double SimBrakeDistance60Mph { get; init; }
    public double SimBrakeDistance100Mph { get; init; }
    public int BrakeProfileId { get; init; }
    public double TractionRoad { get; init; }
    public double TractionOffRoad { get; init; }
    public double TractionSnow { get; init; }

    [JsonIgnore] public string CarDisplayName => $"{Year} {MakeName} {DisplayName}".Trim();
}

public record DbCarMake
{
    public int ID { get; init; }
    public string DisplayName { get; init; }
    public string ManufacturerCode { get; init; }
    public int CountryID { get; init; }
    public string IconPath { get; init; }
    public string IconPathLarge { get; init; }
    public string IconPathBase { get; init; }
    public string Profile { get; init; }
}

public record DbCarBody
{
    public int Id { get; init; }
    public double Height { get; init; }
    public double Length { get; init; }
    public double Width { get; init; }
    public double Wheelbase { get; init; }
    public double ModelWheelbase { get; init; }
    public double ModelFrontTrackOuter { get; init; }
    public double ModelRearTrackOuter { get; init; }
    public double ModelFrontStockRideHeight { get; init; }
    public double ModelRearStockRideHeight { get; init; }
}

public record DbEngine
{
    public int EngineID { get; init; }
    public double EngineMassKg { get; init; }
    public string MediaName { get; init; }
    public int ConfigID { get; init; }
    public int CylinderID { get; init; }
    public double Compression { get; init; }
    public int VariableTimingID { get; init; }
    public int AspirationID_Stock { get; init; }
    public double StockBoostBar { get; init; }
    public double MomentInertia { get; init; }
    public double EngineGraphingMaxTorque { get; init; }
    public double EngineGraphingMaxPower { get; init; }
    public string EngineName { get; init; }
    public int EngineRotation { get; init; }
    public bool Carbureted { get; init; }
    public bool Diesel { get; init; }
    public bool Rotary { get; init; }
    [JsonIgnore] public string DisplayName => EngineName;
}

public record DbTorqueCurve
{
    public int TorqueCurveID { get; init; }
    public double TorqueScale { get; init; }
    public int NumTorqueValues { get; init; }
    public double[] V { get; init; }
    public double ZeroThrottleTorqueScale { get; init; }
}

public record DbMotor
{
    public int MotorID { get; init; }
    public double MotorMassKg { get; init; }
    public string MediaName { get; init; }
    public double MomentInertia { get; init; }
    public double MotorGraphingMaxTorque { get; init; }
    public double MotorGraphingMaxPower { get; init; }
    public string MotorName { get; init; }
    public double RedlineRPM { get; init; }
    public int NumRPMEntriesArray { get; init; }
    public int TorqueCurveFullThrottleID { get; init; }
    public double BatteryCapacity { get; init; }
    [JsonIgnore] public string DisplayName => MotorName;
}

public sealed record DbUpgradeEngine : DbUpgradePart
{
    public int Ordinal { get; init; }
    public int EngineID { get; init; }
    [JsonIgnore] public string DisplayName => $"Swap Lv{Level} (Engine {EngineID})";
}

public sealed record DbUpgradeCamshaft : DbUpgradePart
{
    public int EngineID { get; init; }
    public double RedlineRPM { get; init; }
    public double StallRPM { get; init; }
    public double TorqueCurveMaxRPM { get; init; }
    public int NumRPMEntriesArray { get; init; }
    public int TorqueCurveFullThrottleID { get; init; }
    [JsonIgnore] public string DisplayName => $"Camshaft Lv{Level}";
}

public sealed record DbUpgradeValves : DbUpgradePart
{
    public int EngineID { get; init; }
    
    [JsonIgnore] public string DisplayName => $"Valves Lv{Level}";
}

public sealed record DbUpgradeDisplacement : DbUpgradePart
{
    public int EngineID { get; init; }
    
    public int Disp { get; init; }
    [JsonIgnore] public string DisplayName => $"Displacement Lv{Level}";
}

public sealed record DbUpgradePistons : DbUpgradePart
{
    public int EngineID { get; init; }
    
    [JsonIgnore] public string DisplayName => $"Pistons Lv{Level}";
}

public sealed record DbUpgradeFuelSystem : DbUpgradePart
{
    public int EngineID { get; init; }
    public int PartsStringID { get; init; }
    
    [JsonIgnore] public string DisplayName => $"Fuel Lv{Level}";
}

public sealed record DbUpgradeIgnition : DbUpgradePart
{
    public int EngineID { get; init; }
    public int PartsStringID { get; init; }
    
    [JsonIgnore] public string DisplayName => $"Ignition Lv{Level}";
}

public sealed record DbUpgradeExhaust : DbUpgradePart
{
    public int EngineID { get; init; }
    public int PartsStringID { get; init; }
    
    [JsonIgnore] public string DisplayName => $"Exhaust Lv{Level}";
}

public sealed record DbUpgradeIntake : DbUpgradePart
{
    public int EngineID { get; init; }
    public int PartsStringID { get; init; }
    
    [JsonIgnore] public string DisplayName => $"Intake Lv{Level}";
}

public sealed record DbUpgradeFlywheel : DbUpgradePart
{
    public int EngineID { get; init; }
    public double MomentInertia { get; init; }
    [JsonIgnore] public string DisplayName => $"Flywheel Lv{Level}";
}

public sealed record DbUpgradeManifold : DbUpgradePart
{
    public int EngineID { get; init; }
    
    [JsonIgnore] public string DisplayName => $"Manifold Lv{Level}";
}

public sealed record DbUpgradeOilCooling : DbUpgradePart
{
    public int EngineID { get; init; }
    
    [JsonIgnore] public string DisplayName => $"OilCool Lv{Level}";
}

public sealed record DbUpgradeRestrictor : DbUpgradePart
{
    public int EngineID { get; init; }
    
    [JsonIgnore] public string DisplayName => $"Restrictor Lv{Level}";
}

public abstract record DbUpgradeForcedInduction : DbUpgradePart
{
    public int EngineID { get; init; }
    public double MomentInertia { get; init; }
    // Off-throttle turbo inertia. The DB uses -1 for "normal" (spools down when you lift) and a
    // large positive value (e.g. 300) for anti-lag (stays spooled). Superchargers leave it at the
    // default. Anti-lag effectively kills turbo lag, so we treat it as an earlier boost onset.
    public double OffThrottleMomentInertia { get; init; } = -1.0;
    public bool HasAntiLag => OffThrottleMomentInertia > 0.0;
    public double RobScale { get; init; }
    public double TorqueDropOffRPM0 { get; init; }
    public double TorqueDropOffRPM1 { get; init; }
    public double TorqueDropOffScale0 { get; init; }
    public double TorqueDropOffScale1 { get; init; }
}

public sealed record DbUpgradeTurboSingle : DbUpgradeForcedInduction
{
    public double MaxScale { get; init; }
    public double PowerMaxScale { get; init; }
    public double MinScale { get; init; }
    public double PowerMinScale { get; init; }
    [JsonIgnore] public string DisplayName => $"Single Turbo Lv{Level}";
}

public sealed record DbUpgradeTurboTwin : DbUpgradeForcedInduction
{
    public double MaxScale { get; init; }
    public double PowerMaxScale { get; init; }
    public double MinScale { get; init; }
    public double PowerMinScale { get; init; }
    [JsonIgnore] public string DisplayName => $"Twin Turbo Lv{Level}";
}

public sealed record DbUpgradeCSC : DbUpgradeForcedInduction
{
    public int PartsStringID { get; init; }
    public double ZeroRPMScale { get; init; }
    public double RedlineRPMScale { get; init; }
    [JsonIgnore] public string DisplayName => $"CSC Lv{Level}";
}

public sealed record DbUpgradeDSC : DbUpgradeForcedInduction
{
    public int PartsStringID { get; init; }
    public double ZeroRPMScale { get; init; }
    public double RedlineRPMScale { get; init; }
    [JsonIgnore] public string DisplayName => $"DSC Lv{Level}";
}

public sealed record DbUpgradeIntercooler : DbUpgradePart
{
    public int EngineID { get; init; }
    public int PartsStringID { get; init; }
    public double MaxScaleScale { get; init; }
    [JsonIgnore] public string DisplayName => $"Intercooler Lv{Level}";
}

public sealed record DbUpgradeTireCompound : DbUpgradePart
{
    public int Ordinal { get; init; }
    public int TireCompoundID { get; init; }
    public double FrontTirePressure { get; init; }
    public double RearTirePressure { get; init; }
    public string TireModelName { get; init; } = "";
    [JsonIgnore] public string DisplayName => $"Tires Lv{Level}";
}

public sealed record DbUpgradeSpringDamper : DbUpgradePart
{
    public int Ordinal { get; init; }
    public int FrontSpringDamperPhysicsID { get; init; }
    public int RearSpringDamperPhysicsID { get; init; }
    [JsonIgnore] public string DisplayName => $"Suspension Lv{Level}";
}

public sealed record DbSpringDamperPhysics
{
    public int SpringDamperPhysicsID { get; init; }
    public int Ordinal { get; init; }
    public double DefRideHeight { get; init; }
    public double MinRideHeight { get; init; }
    public double MaxRideHeight { get; init; }
    public double DefSpringRate { get; init; }
    public double MinSpringRate { get; init; }
    public double MaxSpringRate { get; init; }
    public double DefDampenBumpRate { get; init; }
    public double MinDampenBumpRate { get; init; }
    public double MaxDampenBumpRate { get; init; }
    public double DefDampenReboundRate { get; init; }
    public double MinDampenReboundRate { get; init; }
    public double MaxDampenReboundRate { get; init; }
    public double StaticToe { get; init; }
    public double StaticCamber { get; init; }
    public double Caster { get; init; }
}

public sealed record DbUpgradeBrakes : DbUpgradePart
{
    public int Ordinal { get; init; }
    public double FrontBrakeSizeMM { get; init; }
    public double RearBrakeSizeMM { get; init; }
    public double GameFrictionScaleBraking { get; init; }
    public double BrakeTorqueSlider { get; init; }
    public double BrakeBiasSlider { get; init; }
    public double FrontBrakeTorqueClamp { get; init; }
    public double RearBrakeTorqueClamp { get; init; }
    [JsonIgnore] public string DisplayName => $"Brakes Lv{Level}";
}

public sealed record DbUpgradeTransmission : DbUpgradePart
{
    public int DrivetrainID { get; init; }
    public double MomentInertia { get; init; }
    public double FinalDriveRatio { get; init; }
    public int NumGears { get; init; }
    public double GearRatio0 { get; init; }
    public double GearRatio1 { get; init; }
    public double GearRatio2 { get; init; }
    public double GearRatio3 { get; init; }
    public double GearRatio4 { get; init; }
    public double GearRatio5 { get; init; }
    public double GearRatio6 { get; init; }
    public double GearRatio7 { get; init; }
    public double GearRatio8 { get; init; }
    public double GearRatio9 { get; init; }
    public double GearRatio10 { get; init; }
    [JsonIgnore] public string DisplayName => $"Trans Lv{Level}";
    [JsonIgnore] public double[] GearRatios =>
        new[] { GearRatio0, GearRatio1, GearRatio2, GearRatio3, GearRatio4,
                GearRatio5, GearRatio6, GearRatio7, GearRatio8, GearRatio9, GearRatio10 };
}

public sealed record DbUpgradeClutch : DbUpgradePart
{
    public int DrivetrainID { get; init; }
    public double ClutchFriction { get; init; }
    [JsonIgnore] public string DisplayName => $"Clutch Lv{Level}";
}

public sealed record DbUpgradeDriveline : DbUpgradePart
{
    public int DrivetrainId { get; init; }
    public double MomentInertia { get; init; }
    [JsonIgnore] public string DisplayName => $"Driveline Lv{Level}";
}

public sealed record DbUpgradeDifferential : DbUpgradePart
{
    public int DrivetrainID { get; init; }
    public double FrontLimitedSlipTorqueAccel { get; init; }
    public double FrontLimitedSlipTorqueDecel { get; init; }
    public double RearLimitedSlipTorqueAccel { get; init; }
    public double RearLimitedSlipTorqueDecel { get; init; }
    public double CenterLimitedSlipTorqueAccel { get; init; }
    public double CenterLimitedSlipTorqueDecel { get; init; }
    public double RearToqueSplit { get; init; }
    [JsonIgnore] public string DisplayName => $"Diff Lv{Level}";
}

public sealed record DbUpgradeDrivetrain : DbUpgradePart
{
    public int Ordinal { get; init; }
    public int DrivetrainID { get; init; }
    public int PowertrainId { get; init; }
    public int DriveTypeID { get; init; }
    [JsonIgnore] public string DisplayName => $"Drivetrain Lv{Level}";
}

public sealed record DbUpgradeAntiSwayFront : DbUpgradePart
{
    public int Ordinal { get; init; }
    public int AntiSwayPhysicsID { get; init; }
    [JsonIgnore] public string DisplayName => $"ARB Front Lv{Level}";
}

public sealed record DbUpgradeAntiSwayRear : DbUpgradePart
{
    public int Ordinal { get; init; }
    public int AntiSwayPhysicsID { get; init; }
    [JsonIgnore] public string DisplayName => $"ARB Rear Lv{Level}";
}

public sealed record DbUpgradeRearWing : DbUpgradePart
{
    public int Ordinal { get; init; }
    public int AeroPhysicsID { get; init; }
    [JsonIgnore] public string DisplayName => $"Wing Lv{Level}";
}

public sealed record DbUpgradeFrontBumper : DbUpgradePart
{
    public int CarBodyId { get; init; }
    public int AeroPhysicsID { get; init; }
    [JsonIgnore] public string DisplayName => $"FrontBumper Lv{Level}";
}

public sealed record DbUpgradeRearBumper : DbUpgradePart
{
    public int CarBodyId { get; init; }
    public double BodyAeroForwardDownforceFront { get; init; }
    public double BodyAeroForwardDownforceRear { get; init; }
    [JsonIgnore] public string DisplayName => $"RearBumper Lv{Level}";
}

public sealed record DbUpgradeSideSkirt : DbUpgradePart
{
    public int CarBodyId { get; init; }
    [JsonIgnore] public string DisplayName => $"SideSkirt Lv{Level}";
}

// A hood upgrade (List_UpgradeCarBodyHood), keyed by CarBodyID like the bumpers/skirts.
// Carbon/lightweight hoods carry a negative MassDiff; a body-kit conversion also re-keys
// here — a swapped body's stock hood can differ in mass from the original (e.g. −5 kg).
public sealed record DbUpgradeHood : DbUpgradePart
{
    public int CarBodyId { get; init; }
    [JsonIgnore] public string DisplayName => $"Hood Lv{Level}";
}

// A body-kit conversion (List_UpgradeCarBody): maps a car (Ordinal) to a specific
// CarBodyID. The stock kit's CarBodyID is Ordinal×1000; swapped kits use other ids and
// re-key every CarBody-scoped upgrade (bumpers, skirts, tire fitment, track) plus the
// body geometry itself (Data_CarBody — a widebody kit has a wider track).
public sealed record DbUpgradeCarBody : DbUpgradePart
{
    public int Ordinal { get; init; }
    public int CarBodyId { get; init; }
    [JsonIgnore] public string DisplayName => $"BodyKit Lv{Level}";
}

public sealed record DbAeroPhysics
{
    public int AeroPhysicsID { get; init; }
    public int Ordinal { get; init; }
    public double DefaultTuneSlider { get; init; }
    public double Drag0 { get; init; }
    public double Downforce0 { get; init; }
    public double Drag1 { get; init; }
    public double Downforce1 { get; init; }
    public double AngleZeroDownforce { get; init; }
}

public sealed record DbAntiSwayPhysics
{
    public int AntiSwayPhysicsID { get; init; }
    public int Ordinal { get; init; }
    public double DefSwaybarStiffness { get; init; }
    public double MinSwaybarStiffness { get; init; }
    public double MaxSwaybarStiffness { get; init; }
    public double SwaybarDamping { get; init; }
    public double BeamStiffness { get; init; }
}

public sealed record DbTireCompound
{
    public int TireCompoundID { get; init; }
    public string DisplayName { get; init; }
    public double DefaultPressure { get; init; }
    public double PressureIncreaseWithMassScaler { get; init; }
    public double TorqueFreeLatFrictionScale { get; init; }
    public double TorqueFreeLongFrictionScaleBrake { get; init; }
    public double TorqueFreeLongFrictionScaleAccel0 { get; init; }
    public double TorqueFreeLongFrictionScaleAccel1 { get; init; }
    public double CompoundStiffness { get; init; }
    public int IsOffroad { get; init; }
}

public sealed record DbUpgradeWeightReduction : DbUpgradePart
{
    public int CarBodyId { get; init; }
    public double Mass { get; init; }
    public double InitialMass { get; init; }
    public double CMHeight { get; init; }
    public double CMBackFront { get; init; }
    [JsonIgnore] public string DisplayName => $"Weight Lv{Level}";
}

public sealed record DbUpgradeChassisStiffness : DbUpgradePart
{
    public int CarbodyId { get; init; }
    public int? PartsStringID { get; init; }
    public double CMHeightDiff { get; init; }
    [JsonIgnore] public string DisplayName => $"Chassis Lv{Level}";
}

public sealed record DbUpgradeTireWidthFront : DbUpgradePart
{
    public int CarBodyId { get; init; }
    public int FrontTireWidth { get; init; }
    [JsonIgnore] public string DisplayName => $"Width Front {FrontTireWidth}mm";
}

public sealed record DbUpgradeTireWidthRear : DbUpgradePart
{
    public int CarBodyId { get; init; }
    public int RearTireWidth { get; init; }
    [JsonIgnore] public string DisplayName => $"Width Rear {RearTireWidth}mm";
}

public sealed record DbUpgradeRimFront : DbUpgradePart
{
    public int Ordinal { get; init; }
    public int FrontWheelDiameter { get; init; }
    [JsonIgnore] public string DisplayName => $"Rim Front {FrontWheelDiameter}\"";
}

public sealed record DbUpgradeRimRear : DbUpgradePart
{
    public int Ordinal { get; init; }
    public int RearWheelDiameter { get; init; }
    [JsonIgnore] public string DisplayName => $"Rim Rear {RearWheelDiameter}\"";
}

public sealed record DbUpgradeTireAspectRatioFront : DbUpgradePart
{
    public int CarBodyId { get; init; }
    public double FrontTireAspectRatioOffset { get; init; }
    [JsonIgnore] public string DisplayName => $"Profile Front {FrontTireAspectRatioOffset:F0}";
}

public sealed record DbUpgradeTireAspectRatioRear : DbUpgradePart
{
    public int CarBodyId { get; init; }
    public double RearTireAspectRatioOffset { get; init; }
    [JsonIgnore] public string DisplayName => $"Profile Rear {RearTireAspectRatioOffset:F0}";
}

public sealed record DbUpgradeTrackSpacingFront : DbUpgradePart
{
    public int CarBodyId { get; init; }
    public double Spacing { get; init; }
    [JsonIgnore] public string DisplayName => $"Spacer Front {Spacing:F0}mm";
}

public sealed record DbUpgradeTrackSpacingRear : DbUpgradePart
{
    public int CarBodyId { get; init; }
    public double Spacing { get; init; }
    [JsonIgnore] public string DisplayName => $"Spacer Rear {Spacing:F0}mm";
}

public sealed record DbUpgradeMotorSwap : DbUpgradePart
{
    public int Ordinal { get; init; }
    public int MotorID { get; init; }
    [JsonIgnore] public string DisplayName => $"Motor Lv{Level}";
}

public sealed record DbUpgradeMotorPart : DbUpgradePart
{
    public int MotorID { get; init; }
    
    [JsonIgnore] public string DisplayName => $"Motor Upgrade Lv{Level}";
}

public sealed record DbUpgradePartInfo
{
    public int Id { get; init; }
    public string TableName { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public string PartName { get; init; } = "";
    public string CameraName { get; init; } = "";
    public bool OkIfNoStockPart { get; init; }
    public bool HasRequiresGraphics { get; init; }
    public bool HasManufacturerId { get; init; }
}

public sealed record DbWheel
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = "";
    public double Mass { get; init; }
    // Lightness tier (0 = heaviest … 4 = lightest). This — not Mass — drives the
    // in-game weight change when fitting a wheel style.
    public int MassLevel { get; init; }
    public int ManufacturerId { get; init; }
    public bool IsStock { get; init; }
}
