using Forza_Horizon_6_Tune_Master.Models;
using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests.Helpers;

namespace TuneMaster.Tests;

/// Tests C101–C110: Known-good car-specific tune snapshots
/// Each test generates a tune for a specific car + discipline and validates
/// key output fields against community-reference ranges (ForzaFire, FH6 wiki).
public class SpecificCarReferenceTests
{
    private static TuneResult Gen(CarCard car, TrackInfo track)
        => new TuneGeneratorService().Generate(car, track, CarFactory.DefaultConstraints());

    // ── C101: Supra A90 — Road RWD ──────────────────────────────────────

    [Fact]
    public void C101_SupraA90Road()
    {
        var r = Gen(CarFactory.SupraA90(), CarFactory.Road());
        Assert.InRange(r.CamberFront, -2.5, -1.0);
        Assert.InRange(r.CamberRear,  -1.5, -0.2);
        Assert.InRange(r.ToeFront, -0.3, 0.0);
        Assert.InRange(r.ToeRear, 0.0, 0.4);
        Assert.InRange(r.Caster, 5.5, 7.0);
        Assert.InRange(r.ARBFront, 25, 40);
        Assert.InRange(r.ARBRear, 18, 30);
        Assert.InRange(r.SpringFront, 57.1, 130);
        Assert.InRange(r.SpringRear,  57.1, 130);
        Assert.InRange(r.RideHeightFront, 50, 120);
        Assert.InRange(r.RideHeightRear,  50, 120);
        Assert.InRange(r.DiffAccel, 40, 70);
        Assert.InRange(r.DiffDecel, 10, 35);
        Assert.Null(r.DiffFrontAccel);
        Assert.Null(r.CenterDiffBias);
        Assert.InRange(r.BrakeBalance, 45, 65);
        Assert.Equal(8, r.GearRatios.Count);
    }

    // ── C102: GT-R R35 — Road AWD ───────────────────────────────────────

    [Fact]
    public void C102_GtrR35Road()
    {
        var r = Gen(CarFactory.GtrR35(), CarFactory.Road());
        Assert.InRange(r.CamberFront, -2.5, -0.8);
        Assert.InRange(r.CamberRear,  -2.0, -0.5);
        Assert.InRange(r.ToeFront, -0.3, 0.1);
        Assert.InRange(r.Caster, 6.0, 7.0);
        Assert.InRange(r.ARBFront, 25, 45);
        Assert.InRange(r.ARBRear, 30, 50);
        Assert.InRange(r.SpringFront, 57.1, 190);
        Assert.InRange(r.SpringRear,  57.1, 200);
        Assert.InRange(r.DiffAccel, 60, 95);
        Assert.InRange(r.DiffDecel, 10, 40);
        Assert.NotNull(r.DiffFrontAccel);
        Assert.NotNull(r.CenterDiffBias);
        Assert.True(r.CenterDiffBias >= 55, $"CenterDiffBias {r.CenterDiffBias} should be >= 55");
        Assert.InRange(r.BrakeBalance, 45, 60);
        Assert.Equal(6, r.GearRatios.Count);
    }

    // ── C103: Hellcat — Drag 1/4 RWD ────────────────────────────────────

    [Fact]
    public void C103_HellcatDrag()
    {
        var r = Gen(CarFactory.Hellcat(), CarFactory.Drag());
        Assert.InRange(r.CamberFront, -1.0, 0.0);
        Assert.InRange(r.CamberRear,  -0.5, 0.5);
        Assert.Equal(0.0, r.ToeFront);
        Assert.Equal(0.0, r.ToeRear);
        Assert.InRange(r.ARBFront, 1, 12);
        Assert.InRange(r.ARBRear, 5, 35);
        Assert.InRange(r.SpringFront, 57.1, 200);
        Assert.InRange(r.SpringRear,  57.1, 286);
        Assert.True(r.SpringRear > r.SpringFront);
        Assert.InRange(r.RideHeightFront, 81, 150);
        Assert.InRange(r.RideHeightRear, 91, 150);
        Assert.InRange(r.DiffAccel, 40, 100);
        Assert.True(r.DiffDecel <= 25, $"Drag RWD diff decel {r.DiffDecel} should be ≤ 25");
        Assert.Null(r.DiffFrontAccel);
        Assert.Null(r.CenterDiffBias);
        Assert.InRange(r.BrakeBalance, 45, 60);
        Assert.NotNull(r.LaunchControlRpm);
        Assert.True(r.LaunchControlRpm >= 1000, $"Launch {r.LaunchControlRpm} < 1000");
        Assert.Equal(8, r.GearRatios.Count);
    }

    // ── C104: Koenigsegg Gemera — Drag Mile AWD ─────────────────────────

    [Fact]
    public void C104_GemeraDrag()
    {
        var r = Gen(CarFactory.Gemera(), CarFactory.Drag(DragDistance.Mile));
        Assert.InRange(r.CamberFront, -1.0, 0.0);
        Assert.InRange(r.CamberRear,  -0.5, 0.5);
        Assert.InRange(r.ARBFront, 1, 10);
        Assert.InRange(r.ARBRear, 5, 30);
        Assert.Equal(57.1, r.SpringFront);
        Assert.InRange(r.SpringRear, 150, 286);
        Assert.InRange(r.RideHeightFront, 81, 120);
        Assert.InRange(r.RideHeightRear, 91, 120);
        Assert.True(r.RideHeightRear >= r.RideHeightFront);
        Assert.Equal(0.0, r.DiffDecel);
        Assert.InRange(r.DiffAccel, 50, 100);
        Assert.NotNull(r.DiffFrontAccel);
        Assert.NotNull(r.CenterDiffBias);
        Assert.NotNull(r.LaunchControlRpm);
        Assert.Equal(10, r.GearRatios.Count);
    }

    // ── C105: Mazda MX-5 — Road RWD ─────────────────────────────────────

    [Fact]
    public void C105_Mx5Road()
    {
        var r = Gen(CarFactory.Mx5(), CarFactory.Road());
        Assert.InRange(r.CamberFront, -2.5, -0.8);
        Assert.InRange(r.CamberRear,  -1.5, -0.2);
        Assert.InRange(r.ToeFront, -0.3, 0.0);
        Assert.InRange(r.ToeRear, 0.0, 0.3);
        Assert.InRange(r.Caster, 5.0, 6.5);
        Assert.InRange(r.ARBFront, 18, 40);
        Assert.InRange(r.ARBRear, 15, 30);
        Assert.InRange(r.SpringFront, 57.1, 90);
        Assert.InRange(r.SpringRear,  57.1, 90);
        Assert.InRange(r.DiffAccel, 15, 65);
        Assert.InRange(r.DiffDecel, 5, 35);
        Assert.Null(r.DiffFrontAccel);
        Assert.InRange(r.BrakeBalance, 45, 65);
        Assert.Equal(6, r.GearRatios.Count);
    }

    // ── C106: Civic Type R — Road FWD ───────────────────────────────────

    [Fact]
    public void C106_CivicTypeR_Road()
    {
        var r = Gen(CarFactory.CivicTypeR(), CarFactory.Road());
        Assert.InRange(r.CamberFront, -2.0, -0.5);
        Assert.InRange(r.CamberRear,  -1.5, -0.2);
        Assert.InRange(r.ToeFront, -0.3, 0.2);
        Assert.InRange(r.ToeRear, 0.0, 0.4);
        Assert.InRange(r.ARBFront, 5, 25);
        Assert.True(r.ARBFront <= r.ARBRear,
            $"FWD road: front ARB {r.ARBFront} should be <= rear {r.ARBRear}");
        Assert.InRange(r.DiffAccel, 70, 95);
        Assert.InRange(r.DiffDecel, 0, 20);
        Assert.True(r.BrakeBalance >= 50, $"FWD brake balance {r.BrakeBalance} should be >= 50");
        Assert.Null(r.DiffFrontAccel);
        Assert.Null(r.CenterDiffBias);
        Assert.Equal(6, r.GearRatios.Count);
    }

    // ── C107: GT3 RS — Road RWD (Rear-engine) ───────────────────────────

    [Fact]
    public void C107_Gt3RsRoad()
    {
        var r = Gen(CarFactory.Gt3Rs(), CarFactory.Road());
        Assert.InRange(r.CamberFront, -2.5, -0.5);
        Assert.InRange(r.CamberRear,  -2.5, -0.5);
        Assert.InRange(r.Caster, 4.5, 7.0);
        Assert.InRange(r.ARBFront, 25, 45);
        Assert.InRange(r.ARBRear, 20, 40);
        Assert.True(r.AeroFront > 0, $"Road rear-engine: front aero {r.AeroFront} should be > 0");
        Assert.True(r.AeroRear  > 0, $"Road rear-engine: rear aero {r.AeroRear} should be > 0");
        Assert.InRange(r.DiffAccel, 40, 75);
        Assert.InRange(r.DiffDecel, 10, 35);
        Assert.Null(r.DiffFrontAccel);
        Assert.Equal(7, r.GearRatios.Count);
    }

    // ── C108: Model S Plaid — Road AWD EV ───────────────────────────────

    [Fact]
    public void C108_ModelSPlaidRoad()
    {
        var r = Gen(CarFactory.ModelSPlaid(), CarFactory.Road());
        Assert.InRange(r.CamberFront, -2.5, -0.5);
        Assert.InRange(r.CamberRear,  -2.0, -0.5);
        Assert.NotNull(r.DiffFrontAccel);
        Assert.NotNull(r.CenterDiffBias);
        Assert.True(r.CenterDiffBias >= 55);
        Assert.Single(r.GearRatios);
        Assert.InRange(r.FinalDrive, 2.2, 6.1);
        Assert.InRange(r.SpringFront, 57.1, 286);
        Assert.InRange(r.SpringRear,  57.1, 286);
    }

    // ── C109: WRX STI — Rally AWD ───────────────────────────────────────

    [Fact]
    public void C109_WrxStiRally()
    {
        var r = Gen(CarFactory.WrxSti(), CarFactory.Rally());
        Assert.InRange(r.CamberFront, -2.0, -0.5);
        Assert.InRange(r.CamberRear,  -1.5, -0.2);
        Assert.InRange(r.ToeFront, -0.5, 0.0);
        Assert.InRange(r.ToeRear, 0.0, 0.3);
        Assert.InRange(r.ARBFront, 1, 20);
        Assert.InRange(r.ARBRear, 1, 20);
        Assert.InRange(r.RideHeightFront, 70, 220);
        Assert.InRange(r.RideHeightRear,  80, 230);
        Assert.InRange(r.DiffAccel, 50, 85);
        Assert.InRange(r.DiffDecel, 10, 40);
        Assert.NotNull(r.DiffFrontAccel);
        Assert.NotNull(r.CenterDiffBias);
        Assert.Equal(6, r.GearRatios.Count);
    }

    // ── C110: Wrangler — CrossCountry AWD ───────────────────────────────

    [Fact]
    public void C110_WranglerCrossCountry()
    {
        var r = Gen(CarFactory.Wrangler(), CarFactory.CrossCountry());
        Assert.InRange(r.CamberFront, -1.5, 0.0);
        Assert.InRange(r.CamberRear,  -1.5, 0.0);
        Assert.InRange(r.ARBFront, 1, 20);
        Assert.InRange(r.ARBRear,  1, 20);
        Assert.InRange(r.SpringFront, 57.1, 150);
        Assert.InRange(r.SpringRear,  57.1, 150);
        Assert.InRange(r.RideHeightFront, 80, 250);
        Assert.InRange(r.RideHeightRear,  90, 250);
        Assert.InRange(r.DiffAccel, 50, 85);
        Assert.InRange(r.DiffDecel, 10, 40);
        Assert.NotNull(r.DiffFrontAccel);
        Assert.NotNull(r.CenterDiffBias);
        Assert.Equal(6, r.GearRatios.Count);
    }
}
