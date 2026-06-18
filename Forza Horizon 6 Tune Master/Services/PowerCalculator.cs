using System;
using System.Linq;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

public static class PowerCalculator
{
    private const double RadSToRPM = 60.0 / (2.0 * Math.PI);

    public static void Calculate(CarCard car, SelectedParts? parts = null)
    {
        var db = Fh6DatabaseService.Instance;
        var dbCar = db.GetCar(car.CarDbId);
        if (dbCar == null) return;

        parts ??= new SelectedParts();
        double fiPowerScale = 1.0;
        if (dbCar.PowertrainID == 1)
            CalcElectric(car, dbCar, db, parts);
        else
            fiPowerScale = CalcIce(car, dbCar, db, parts);

        // Apply rotational inertia factor
        double inertiaFactor = TuningPhysicsContext.ComputeRotationalInertiaFactor(car, parts, db);
        car.RotationalInertiaFactor = inertiaFactor;
        if (car.CachedTorqueCurveNm is { Length: > 0 } && Math.Abs(inertiaFactor - 1.0) > 0.001)
        {
            for (int i = 0; i < car.CachedTorqueCurveNm.Length; i++)
                car.CachedTorqueCurveNm[i] = Math.Round(car.CachedTorqueCurveNm[i] * inertiaFactor, 1);
        }

        car.CachedPowerCurveHP = ComputePowerCurveFromTorque(car.CachedTorqueCurveNm, car.MaxRPM);

        if (car.CachedPowerCurveHP is { Length: > 0 })
        {
            if (fiPowerScale > 1.01 || fiPowerScale < 0.99)
            {
                for (int i = 0; i < car.CachedPowerCurveHP.Length; i++)
                    car.CachedPowerCurveHP[i] = Math.Round(car.CachedPowerCurveHP[i] * fiPowerScale, 1);
            }
            car.PowerHP = Math.Round(car.CachedPowerCurveHP.Max(), 1);
        }
    }

    private static void CalcElectric(CarCard car, DbCar dbCar, Fh6DatabaseService db, SelectedParts? parts = null)
    {
        int motorId = ResolveEffectiveEngineId(car, dbCar, db, parts);
        var motor = db.GetMotor(motorId);
        if (motor == null) return;

        double maxRpm = motor.RedlineRPM;
        double peakTorqueNm = motor.MotorGraphingMaxTorque;
        double peakPowerW = motor.MotorGraphingMaxPower;

        car.MaxRPM = (int)Math.Round(maxRpm);
        car.TorqueNm = Math.Round(peakTorqueNm);
        car.PowerHP = Math.Round(peakPowerW / 745.7, 1);

        car.CachedTorqueCurveNm = LoadTorqueCurve(motor.TorqueCurveFullThrottleID, db, 1.0)
            ?? GenerateElectricTorqueCurve(peakTorqueNm, (int)Math.Round(maxRpm));
    }

    private static double CalcIce(CarCard car, DbCar dbCar, Fh6DatabaseService db, SelectedParts parts)
    {
        double redlineRPM = dbCar.SimRedlineAngVel * RadSToRPM;
        double peakTorqueNm = dbCar.SimPeakTorque;
        double peakPowerW = dbCar.SimPeakPower;
        double torqueScale = dbCar.GameTorqueScale;

        double partRedlineRPM = redlineRPM;
        int? torqueCurveId = null;
        double torqueCurveMaxRPM = redlineRPM;

        if (parts.CamshaftPartId != null)
        {
            var cam = db.GetCamshaftById(parts.CamshaftPartId.Value);
            if (cam != null)
            {
                partRedlineRPM = cam.RedlineRPM > 0 ? cam.RedlineRPM : redlineRPM;
                torqueCurveId = cam.TorqueCurveFullThrottleID > 0 ? cam.TorqueCurveFullThrottleID : null;
                torqueCurveMaxRPM = cam.TorqueCurveMaxRPM > 0 ? cam.TorqueCurveMaxRPM : partRedlineRPM;
                torqueScale *= cam.TorqueScale ?? 1.0;
            }
        }

        car.MaxRPM = (int)Math.Round(partRedlineRPM);

        double partScale = AccumulatePartTorqueScales(parts, db);

        double fiTorqueScale = 1.0;
        double fiPowerScale = 1.0;
        ApplyForcedInduction(parts, db, ref fiTorqueScale, ref fiPowerScale);

        double intercoolerScale = 1.0;
        if (parts.ForcedInductionPartId != null && parts.IntercoolerPartId != null)
        {
            var ic = db.GetIntercoolerById(parts.IntercoolerPartId.Value);
            if (ic != null && ic.MaxScaleScale > 0.001)
                intercoolerScale = ic.MaxScaleScale;
        }

        double totalTorqueScale = torqueScale * Math.Max(0.1, partScale) * Math.Max(0.1, fiTorqueScale) * intercoolerScale;
        totalTorqueScale = Math.Min(totalTorqueScale, 20.0);
        double totalPowerScale = Math.Min(Math.Max(0.1, fiPowerScale), 5.0);

        double[] torqueCurve;
        if (torqueCurveId != null)
        {
            torqueCurve = LoadTorqueCurve(torqueCurveId.Value, db, totalTorqueScale)
                ?? GenerateIceTorqueCurve(dbCar, peakTorqueNm * totalTorqueScale, partRedlineRPM);
        }
        else
        {
            torqueCurve = GenerateIceTorqueCurve(dbCar, peakTorqueNm * totalTorqueScale, partRedlineRPM);
        }

        car.CachedTorqueCurveNm = torqueCurve;
        car.TorqueNm = Math.Round(torqueCurve.Max());
        return totalPowerScale;
    }

    private static int ResolveEffectiveEngineId(CarCard car, DbCar dbCar, Fh6DatabaseService db, SelectedParts? parts = null)
    {
        int? swapId = parts?.EngineSwapPartId;
        if (swapId != null)
        {
            var swap = db.GetEngineSwapById(swapId.Value);
            if (swap != null) return swap.EngineID;
        }
        if (car.EngineDbId > 0) return car.EngineDbId;

        var swaps = db.GetEngineSwaps(dbCar.Id);
        return swaps?.FirstOrDefault(e => e.IsStock)?.EngineID ?? 0;
    }

    private static double AccumulatePartTorqueScales(SelectedParts parts, Fh6DatabaseService db)
    {
        var scales = new System.Collections.Generic.List<double>();
        AddTorqueScaleIfNotStock(db, parts.DisplacementPartId, id => db.GetDisplacementById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.ValvesPartId, id => db.GetValvesById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.PistonsPartId, id => db.GetPistonsById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.FuelSystemPartId, id => db.GetFuelSystemById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.IgnitionPartId, id => db.GetIgnitionById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.ExhaustPartId, id => db.GetExhaustById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.IntakePartId, id => db.GetIntakeById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.ManifoldPartId, id => db.GetManifoldById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.OilCoolingPartId, id => db.GetOilCoolingById(id), scales);
        AddTorqueScaleIfNotStock(db, parts.RestrictorPartId, id => db.GetRestrictorById(id), scales);

        if (scales.Count == 0) return 1.0;

        double product = scales.Aggregate(1.0, (p, s) => p * s);
        return Math.Pow(product, 1.0 / Math.Sqrt(scales.Count));
    }

    private static void AddTorqueScaleIfNotStock<T>(Fh6DatabaseService db, int? partId, Func<int, T?> getter, System.Collections.Generic.List<double> scales)
        where T : DbUpgradePart
    {
        if (partId == null) return;
        var part = getter(partId.Value);
        if (part == null) return;
        if (part.IsStock) return;
        double s = part.TorqueScale ?? 1.0;
        if (Math.Abs(s - 1.0) < 0.005) return;
        scales.Add(s);
    }

    private static void ApplyForcedInduction(SelectedParts parts, Fh6DatabaseService db, ref double torqueScale, ref double powerScale)
    {
        if (parts.ForcedInductionPartId == null) return;

        var fi = db.GetForcedInductionById(parts.ForcedInductionPartId.Value);
        if (fi == null) return;

        if (fi is DbUpgradeTurboSingle ts)
        {
            torqueScale *= ts.MaxScale;
            powerScale *= ts.PowerMaxScale;
        }
        else if (fi is DbUpgradeTurboTwin tt)
        {
            torqueScale *= tt.MaxScale;
            powerScale *= tt.PowerMaxScale;
        }
        else if (fi is DbUpgradeCSC csc)
        {
            torqueScale *= csc.RedlineRPMScale;
            powerScale *= csc.RedlineRPMScale;
        }
        else if (fi is DbUpgradeDSC dsc)
        {
            torqueScale *= dsc.RedlineRPMScale;
            powerScale *= dsc.RedlineRPMScale;
        }
    }

    private static double[]? LoadTorqueCurve(int curveId, Fh6DatabaseService db, double scale)
    {
        var tc = db.GetTorqueCurve(curveId);
        if (tc?.V == null || tc.V.Length == 0) return null;
        return tc.V.Select(v => Math.Round(v * tc.TorqueScale * scale, 1)).ToArray();
    }

    private static double[] GenerateIceTorqueCurve(DbCar dbCar, double peakTorque, double redlineRPM)
    {
        int points = 20;
        double[] curve = new double[points];
        double torquePeakRPM = dbCar.SimPeakTorqueAngVel * RadSToRPM;
        double powerPeakRPM = dbCar.SimPeakAngVel * RadSToRPM;

        for (int i = 0; i < points; i++)
        {
            double rpm = redlineRPM * i / (points - 1);
            if (rpm <= torquePeakRPM)
                curve[i] = peakTorque * (0.65 + 0.35 * rpm / Math.Max(torquePeakRPM, 1));
            else if (rpm <= powerPeakRPM)
                curve[i] = peakTorque * (1.0 - 0.15 * (rpm - torquePeakRPM) / Math.Max(powerPeakRPM - torquePeakRPM, 1));
            else
                curve[i] = peakTorque * 0.85 * (1.0 - (rpm - powerPeakRPM) / Math.Max(redlineRPM - powerPeakRPM, 1) * 0.7);
            curve[i] = Math.Round(Math.Max(curve[i], peakTorque * 0.15), 1);
        }
        return curve;
    }

    private static double[] GenerateElectricTorqueCurve(double peakTorque, int maxRPM)
    {
        int points = 20;
        double[] curve = new double[points];
        for (int i = 0; i < points; i++)
        {
            double pct = (double)i / (points - 1);
            curve[i] = pct < 0.9
                ? peakTorque
                : peakTorque * (1.0 - (pct - 0.9) / 0.1 * 0.5);
            curve[i] = Math.Round(curve[i], 1);
        }
        return curve;
    }

    private static double[]? ComputePowerCurveFromTorque(double[]? torqueCurve, int maxRPM)
    {
        if (torqueCurve == null || torqueCurve.Length == 0) return null;
        int points = torqueCurve.Length;
        double[] power = new double[points];
        for (int i = 0; i < points; i++)
        {
            double rpm = maxRPM * i / (double)(points - 1);
            power[i] = Math.Round(torqueCurve[i] * rpm / 7121.0, 1);
        }
        return power;
    }
}
