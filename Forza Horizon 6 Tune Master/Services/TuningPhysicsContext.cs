using System;
using System.Linq;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

/// <summary>
/// Resolves the DB physics records for the parts actually selected on a car.
/// Falls back to stock parts when nothing is selected.
/// </summary>
internal static class TuningPhysicsContext
{
    public static DbSpringDamperPhysics FrontSpringDamper(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        var upgrade = ResolveSpringDamperUpgrade(car, parts, db);
        if (upgrade != null)
        {
            var phys = db.GetSpringDamperPhysics(upgrade.FrontSpringDamperPhysicsID);
            if (phys != null) return phys;
        }
        return FallbackSpringDamper(car, front: true);
    }

    public static DbSpringDamperPhysics RearSpringDamper(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        var upgrade = ResolveSpringDamperUpgrade(car, parts, db);
        if (upgrade != null)
        {
            var phys = db.GetSpringDamperPhysics(upgrade.RearSpringDamperPhysicsID);
            if (phys != null) return phys;
        }
        return FallbackSpringDamper(car, front: false);
    }

    public static DbAntiSwayPhysics FrontArb(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        var upgrade = ResolveFrontArbUpgrade(car, parts, db);
        if (upgrade != null)
        {
            var phys = db.GetAntiSwayPhysics(upgrade.AntiSwayPhysicsID);
            if (phys != null) return phys;
        }
        return FallbackAntiSwayPhysics();
    }

    public static DbAntiSwayPhysics RearArb(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        var upgrade = ResolveRearArbUpgrade(car, parts, db);
        if (upgrade != null)
        {
            var phys = db.GetAntiSwayPhysics(upgrade.AntiSwayPhysicsID);
            if (phys != null) return phys;
        }
        return FallbackAntiSwayPhysics();
    }

    public static DbAeroPhysics? RearWingAero(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        if (!car.HasRearAero) return null;
        var upgrade = ResolveRearWingUpgrade(car, parts, db);
        if (upgrade != null)
        {
            var phys = db.GetAeroPhysics(upgrade.AeroPhysicsID);
            if (phys != null) return phys;
        }
        return FallbackAeroPhysics();
    }

    public static DbAeroPhysics? FrontBumperAero(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        if (!car.HasFrontAero) return null;
        var upgrade = ResolveFrontBumperUpgrade(car, parts, db);
        if (upgrade != null && upgrade.AeroPhysicsID > 0)
        {
            var phys = db.GetAeroPhysics(upgrade.AeroPhysicsID);
            if (phys != null) return phys;
        }
        return FallbackAeroPhysics();
    }

    public static DbUpgradeTireCompound? TireCompound(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        if (parts.TireCompoundPartId.HasValue)
            return db.GetTireCompoundById(parts.TireCompoundPartId.Value);
        return db.GetTireCompounds(car.CarDbId).FirstOrDefault(p => p.IsStock)
            ?? db.GetTireCompounds(car.CarDbId).FirstOrDefault();
    }

    public static DbUpgradeBrakes? Brakes(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        if (parts.BrakePartId.HasValue)
            return db.GetBrakesById(parts.BrakePartId.Value);
        return db.GetBrakes(car.CarDbId).FirstOrDefault(p => p.IsStock)
            ?? db.GetBrakes(car.CarDbId).FirstOrDefault();
    }

    public static DbUpgradeTransmission Transmission(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        int drivetrainId = EffectiveDrivetrainId(car, parts, db);
        DbUpgradeTransmission? trans = null;
        if (parts.TransmissionPartId.HasValue)
            trans = db.GetTransmissionById(parts.TransmissionPartId.Value);
        trans ??= db.GetTransmissions(drivetrainId).FirstOrDefault(p => p.IsStock)
            ?? db.GetTransmissions(drivetrainId).FirstOrDefault();
        return trans ?? FallbackTransmission(car);
    }

    public static DbUpgradeDifferential Differential(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        int drivetrainId = EffectiveDrivetrainId(car, parts, db);
        DbUpgradeDifferential? diff = null;
        if (parts.DifferentialPartId.HasValue)
            diff = db.GetDifferentialById(parts.DifferentialPartId.Value);
        diff ??= db.GetDifferentials(drivetrainId).FirstOrDefault(p => p.IsStock)
            ?? db.GetDifferentials(drivetrainId).FirstOrDefault();
        return diff ?? FallbackDifferential(car);
    }

    public static DbUpgradeDrivetrain? Drivetrain(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        if (parts.DrivetrainSwapPartId.HasValue)
            return db.GetDrivetrainSwapById(parts.DrivetrainSwapPartId.Value);
        return db.GetDrivetrainSwaps(car.CarDbId).FirstOrDefault(p => p.IsStock)
            ?? db.GetDrivetrainSwaps(car.CarDbId).FirstOrDefault();
    }

    public static int EffectiveDrivetrainId(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        if (parts.DrivetrainId.HasValue)
            return parts.DrivetrainId.Value;
        var stock = db.GetDrivetrainSwaps(car.CarDbId).FirstOrDefault(p => p.IsStock)
            ?? db.GetDrivetrainSwaps(car.CarDbId).FirstOrDefault();
        return stock?.DrivetrainID ?? 0;
    }

    public static double ComputeRotationalInertiaFactor(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        // Engine/motor inertia
        double engineMI;
        if (car.PowertrainType == PowertrainType.Electric)
        {
            var motor = db.GetMotor(car.MotorDbId);
            engineMI = motor?.MomentInertia ?? 0;
        }
        else
        {
            var eng = db.GetEngine(car.EngineDbId);
            engineMI = eng?.MomentInertia ?? 0;
        }
        if (engineMI <= 0) return 1.0;

        int engineId = car.EngineDbId;
        int drivetrainId = EffectiveDrivetrainId(car, parts, db);

        // Stock flywheel inertia (baseline)
        var stockFw = db.GetFlywheels(engineId).FirstOrDefault(p => p.IsStock);
        double stockFlyMI = stockFw?.MomentInertia ?? 0;

        // Stock driveline inertia (baseline)
        var stockDl = db.GetDrivelines(drivetrainId).FirstOrDefault(p => p.IsStock);
        double stockDrivelineMI = stockDl?.MomentInertia ?? 0;

        double stockTotal = engineMI + stockFlyMI + stockDrivelineMI;

        // Current flywheel inertia
        double flyMI = 0;
        if (parts.FlywheelPartId.HasValue)
        {
            var fw = db.GetFlywheelById(parts.FlywheelPartId.Value);
            if (fw != null) flyMI = fw.MomentInertia;
        }
        else flyMI = stockFlyMI;

        // NOTE: forced-induction rotational inertia is deliberately excluded here. This
        // factor scales the whole torque/power curve, and a turbo's spool inertia is a lag
        // characteristic — it must not cut PEAK power. Including it made adding a turbo to a
        // naturally-aspirated engine drop the factor to its 0.7 floor and lose ~30% power,
        // cancelling the boost. The boost itself is modelled in PowerCalculator.

        // Current driveline inertia
        double drivelineMI = 0;
        if (parts.DrivelinePartId.HasValue)
        {
            var dl = db.GetDrivelineById(parts.DrivelinePartId.Value);
            if (dl != null) drivelineMI = dl.MomentInertia;
        }
        else drivelineMI = stockDrivelineMI;

        double currentTotal = engineMI + flyMI + drivelineMI;
        if (currentTotal <= 0) return 1.0;

        return Math.Clamp(stockTotal / currentTotal, 0.7, 1.3);
    }

    private static DbUpgradeSpringDamper? ResolveSpringDamperUpgrade(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        if (parts.SpringDamperPartId.HasValue)
            return db.GetSpringDamperById(parts.SpringDamperPartId.Value);
        return db.GetSpringDampers(car.CarDbId).FirstOrDefault(p => p.IsStock)
            ?? db.GetSpringDampers(car.CarDbId).FirstOrDefault();
    }

    private static DbUpgradeAntiSwayFront? ResolveFrontArbUpgrade(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        if (parts.AntiSwayFrontPartId.HasValue)
            return db.GetArbFrontById(parts.AntiSwayFrontPartId.Value);
        return db.GetAntiSwayFront(car.CarDbId).FirstOrDefault(p => p.IsStock)
            ?? db.GetAntiSwayFront(car.CarDbId).FirstOrDefault();
    }

    private static DbUpgradeAntiSwayRear? ResolveRearArbUpgrade(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        if (parts.AntiSwayRearPartId.HasValue)
            return db.GetArbRearById(parts.AntiSwayRearPartId.Value);
        return db.GetAntiSwayRear(car.CarDbId).FirstOrDefault(p => p.IsStock)
            ?? db.GetAntiSwayRear(car.CarDbId).FirstOrDefault();
    }

    private static DbUpgradeRearWing? ResolveRearWingUpgrade(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        if (parts.RearWingPartId.HasValue)
            return db.GetRearWingById(parts.RearWingPartId.Value);
        return db.GetRearWings(car.CarDbId).FirstOrDefault(p => p.IsStock)
            ?? db.GetRearWings(car.CarDbId).FirstOrDefault();
    }

    // Front bumpers/splitters are keyed by CarBodyId (not Ordinal). When no specific bumper
    // is selected, fall back to a non-stock bumper that actually carries an aero profile —
    // a stock bumper usually has none (AeroPhysicsID == 0), which is why HasFrontAero only
    // turns true for a fitted aero front bumper.
    private static DbUpgradeFrontBumper? ResolveFrontBumperUpgrade(CarCard car, SelectedParts parts, Fh6DatabaseService db)
    {
        if (parts.FrontBumperPartId.HasValue)
            return db.GetFrontBumperById(parts.FrontBumperPartId.Value);
        var bumpers = db.GetFrontBumpers(car.CarBodyId);
        return bumpers.FirstOrDefault(p => !p.IsStock && p.AeroPhysicsID > 0)
            ?? bumpers.FirstOrDefault(p => p.AeroPhysicsID > 0);
    }

    // ── Synthetic fallbacks for cars/tests without DB part records ──────
    private static DbSpringDamperPhysics FallbackSpringDamper(CarCard car, bool front)
    {
        bool race = car.SuspensionUpgrade == SuspensionUpgrade.Race;
        bool sport = car.SuspensionUpgrade == SuspensionUpgrade.Sport;
        double levelMul = race ? 1.0 : sport ? 0.75 : 0.55;
        double minRide = 0.080;
        double maxRide = 0.180;
        // Spring rates are in N/mm, matching the real DB (adjustable race ≈ 40-200 N/mm).
        double minSpring = 30.0 * levelMul;
        double maxSpring = 220.0 * levelMul;
        double defSpring = (minSpring + maxSpring) / 2.0;
        double defRebound = race ? 12.0 : sport ? 10.0 : 8.0;
        double defBump = race ? 7.5 : sport ? 6.0 : 5.0;
        double staticCamber = front ? -0.5 : -1.0;
        double staticToe = front ? 0.0 : 0.05;

        return new DbSpringDamperPhysics
        {
            SpringDamperPhysicsID = 0,
            Ordinal = 0,
            DefRideHeight = (minRide + maxRide) / 2.0,
            MinRideHeight = minRide,
            MaxRideHeight = maxRide,
            DefSpringRate = defSpring,
            MinSpringRate = minSpring,
            MaxSpringRate = maxSpring,
            DefDampenBumpRate = defBump,
            MinDampenBumpRate = 0.0,
            MaxDampenBumpRate = 80.0,
            DefDampenReboundRate = defRebound,
            MinDampenReboundRate = 0.0,
            MaxDampenReboundRate = 80.0,
            StaticToe = staticToe,
            StaticCamber = staticCamber,
            Caster = 6.5
        };
    }

    private static DbAntiSwayPhysics FallbackAntiSwayPhysics() => new()
    {
        AntiSwayPhysicsID = 0,
        Ordinal = 0,
        DefSwaybarStiffness = 20.0,
        MinSwaybarStiffness = 1.0,
        MaxSwaybarStiffness = 65.0,
        SwaybarDamping = 0.0,
        BeamStiffness = 0.0
    };

    private static DbAeroPhysics FallbackAeroPhysics() => new()
    {
        AeroPhysicsID = 0,
        Ordinal = 0,
        DefaultTuneSlider = 0.5,
        Drag0 = 0.0,
        Downforce0 = 0.0,
        Drag1 = 0.35,
        Downforce1 = 200.0,
        AngleZeroDownforce = 0.0
    };

    private static DbUpgradeTransmission FallbackTransmission(CarCard car)
    {
        int gears = Math.Clamp(car.GearCount > 0 ? car.GearCount : 6, 1, 10);
        double first = 3.50;
        double step = 0.75;
        double[] ratios = new double[11];
        for (int i = 0; i < gears; i++)
            ratios[i] = Math.Round(first * Math.Pow(step, i), 2);

        return new DbUpgradeTransmission
        {
            Id = 0,
            Level = 1,
            IsStock = true,
            DrivetrainID = 0,
            NumGears = gears,
            FinalDriveRatio = 3.5,
            GearRatio0 = ratios[0],
            GearRatio1 = ratios[1],
            GearRatio2 = ratios[2],
            GearRatio3 = ratios[3],
            GearRatio4 = ratios[4],
            GearRatio5 = ratios[5],
            GearRatio6 = ratios[6],
            GearRatio7 = ratios[7],
            GearRatio8 = ratios[8],
            GearRatio9 = ratios[9],
            GearRatio10 = ratios[10]
        };
    }

    private static DbUpgradeDifferential FallbackDifferential(CarCard car)
    {
        bool hasDecel = car.DifferentialUpgrade != DifferentialUpgrade.Stock
                     && car.DifferentialUpgrade != DifferentialUpgrade.Sport;
        return new DbUpgradeDifferential
        {
            Id = 0,
            Level = (int)car.DifferentialUpgrade,
            IsStock = car.DifferentialUpgrade == DifferentialUpgrade.Stock,
            DrivetrainID = 0,
            FrontLimitedSlipTorqueAccel = 0.65,
            FrontLimitedSlipTorqueDecel = hasDecel ? 0.20 : 0.0,
            RearLimitedSlipTorqueAccel = 0.95,
            RearLimitedSlipTorqueDecel = hasDecel ? 0.50 : 0.0,
            CenterLimitedSlipTorqueAccel = 0.0,
            CenterLimitedSlipTorqueDecel = 0.0,
            RearToqueSplit = 0.65
        };
    }
}
