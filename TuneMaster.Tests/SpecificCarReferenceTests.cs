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

    // ── C111: Porsche 918 Spyder — Road Hybrid AWD ──────────────────────────

    [Fact]
    public void C111_Porsche918Road()
    {
        var r = Gen(CarFactory.Porsche918(), CarFactory.Road());
        Assert.True(r.SpringRear >= r.SpringFront,
            $"Hybrid RearMid: rear spring {r.SpringRear} should be >= front {r.SpringFront}");
        Assert.NotNull(r.DiffFrontAccel);
        Assert.NotNull(r.CenterDiffBias);
        Assert.True(r.CenterDiffBias >= 55, $"Porsche918 center bias {r.CenterDiffBias} should be >= 55");
        Assert.InRange(r.DiffAccel, 55, 100);
        Assert.InRange(r.CamberFront, -3.0, -0.5);
        Assert.InRange(r.CamberRear,  -3.0, -0.5);
        Assert.True(r.BrakeBalance <= 52, $"Rear-heavy brake balance {r.BrakeBalance} should be ≤ 52");
        Assert.Equal(7, r.GearRatios.Count);
    }

    // ── C112: Evo X AntiLag — Road AWD ──────────────────────────────────────

    [Fact]
    public void C112_EvoXAntiLag_Road_DiffHigherThanNoAntiLag()
    {
        var antilag   = CarFactory.LancerEvoXAntiLag();
        var noAntilag = CarFactory.LancerEvoXAntiLag(); noAntilag.AntiLag = false;
        var rAL   = Gen(antilag,   CarFactory.Road());
        var rNoAL = Gen(noAntilag, CarFactory.Road());
        Assert.True(rAL.DiffAccel >= rNoAL.DiffAccel,
            $"AntiLag diff accel {rAL.DiffAccel} should be >= no-antilag {rNoAL.DiffAccel}");
        Assert.True(rAL.SpringRear >= rNoAL.SpringRear,
            $"AntiLag rear spring {rAL.SpringRear} should be >= no-antilag {rNoAL.SpringRear}");
        Assert.InRange(rAL.Caster, 5.0, 7.0);
    }

    // ── C113: Evo X AntiLag — Drag launch RPM higher than without antilag ───

    [Fact]
    public void C113_EvoXAntiLag_Drag_LaunchHigherThanNoAntiLag()
    {
        var antilag   = CarFactory.LancerEvoXAntiLag();
        var noAntilag = CarFactory.LancerEvoXAntiLag(); noAntilag.AntiLag = false;
        var rAL   = Gen(antilag,   CarFactory.Drag());
        var rNoAL = Gen(noAntilag, CarFactory.Drag());
        Assert.NotNull(rAL.LaunchControlRpm);
        Assert.NotNull(rNoAL.LaunchControlRpm);
        Assert.True(rAL.LaunchControlRpm >= rNoAL.LaunchControlRpm,
            $"AntiLag launch {rAL.LaunchControlRpm} should be >= no-antilag {rNoAL.LaunchControlRpm}");
    }

    // ── C114: McLaren 720S — Road RWD RearMid ───────────────────────────────

    [Fact]
    public void C114_McLaren720SRoad()
    {
        var r = Gen(CarFactory.McLaren720S(), CarFactory.Road());
        Assert.True(r.SpringRear >= r.SpringFront,
            $"RearMid RWD: rear spring {r.SpringRear} should be >= front {r.SpringFront}");
        Assert.True(r.BrakeBalance <= 52,
            $"Rear-heavy (42% front) brake balance {r.BrakeBalance} should be ≤ 52");
        Assert.InRange(r.DiffAccel, 40, 100);
        Assert.InRange(r.DiffDecel, 10, 40);
        Assert.InRange(r.CamberRear, -3.0, -1.0);
        Assert.True(r.AeroFront > 0, "720S front aero should be active");
        Assert.True(r.AeroRear  > 0, "720S rear aero should be active");
        Assert.Null(r.DiffFrontAccel);
        Assert.Equal(7, r.GearRatios.Count);
    }

    // ── C115: McLaren 720S — Touge RWD ──────────────────────────────────────

    [Fact]
    public void C115_McLaren720STouge()
    {
        var r = Gen(CarFactory.McLaren720S(), CarFactory.Touge());
        Assert.True(r.SpringRear >= r.SpringFront,
            $"RearMid Touge: rear spring {r.SpringRear} should be >= front {r.SpringFront}");
        Assert.InRange(r.Caster, 5.0, 7.0);
        Assert.InRange(r.ToeFront, -0.5, 0.0);
        Assert.InRange(r.ToeRear,   0.0, 0.4);
        Assert.Equal(7, r.GearRatios.Count);
    }

    // ── C116: Bugatti Chiron — Road AWD extreme power ───────────────────────

    [Fact]
    public void C116_BugattiChironRoad()
    {
        var c = CarFactory.DefaultConstraints();
        var r = new TuneGeneratorService().Generate(CarFactory.BugattiChiron(), CarFactory.Road(), c);
        Assert.InRange(r.DiffAccel,   c.DiffAccelMin,    c.DiffAccelMax);
        Assert.InRange(r.DiffDecel,   c.DiffDecelMin,    c.DiffDecelMax);
        Assert.InRange(r.SpringFront, c.SpringFrontMin,  c.SpringFrontMax);
        Assert.InRange(r.SpringRear,  c.SpringRearMin,   c.SpringRearMax);
        Assert.InRange(r.ARBFront,    c.ARBFrontMin,     c.ARBFrontMax);
        Assert.InRange(r.ARBRear,     c.ARBRearMin,      c.ARBRearMax);
        Assert.NotNull(r.CenterDiffBias);
        Assert.True(r.CenterDiffBias >= 55, $"Chiron center bias {r.CenterDiffBias} should be >= 55");
        Assert.Equal(7, r.GearRatios.Count);
    }

    // ── C117: Honda S2000 — Touge RWD ───────────────────────────────────────

    [Fact]
    public void C117_HondaS2000Touge()
    {
        var r = Gen(CarFactory.HondaS2000(), CarFactory.Touge());
        Assert.InRange(r.Caster, 5.5, 7.0);
        Assert.InRange(r.SpringFront, 57.1, 85);
        Assert.InRange(r.SpringRear,  57.1, 90);
        Assert.InRange(r.DiffAccel, 15, 65);
        Assert.InRange(r.DiffDecel,  5, 35);
        Assert.Null(r.DiffFrontAccel);
        Assert.InRange(r.ToeFront, -0.4, 0.0);
        Assert.InRange(r.ToeRear,   0.0, 0.3);
        Assert.Equal(6, r.GearRatios.Count);
    }

    // ── C118: Porsche 918 — Drag Hybrid AWD ─────────────────────────────────

    [Fact]
    public void C118_Porsche918Drag()
    {
        var car = CarFactory.Porsche918();
        var r   = Gen(car, CarFactory.Drag());
        Assert.NotNull(r.LaunchControlRpm);
        Assert.True(r.LaunchControlRpm >= 1000, $"Launch {r.LaunchControlRpm} < 1000");
        Assert.True(r.LaunchControlRpm <= car.MaxRPM * 0.75,
            $"Launch {r.LaunchControlRpm} exceeds MaxRPM × 0.75 ({car.MaxRPM * 0.75})");
        Assert.NotNull(r.CenterDiffBias);
        Assert.InRange(r.CenterDiffBias!.Value, 50, 80);
        Assert.Equal(7, r.GearRatios.Count);
    }

    // ── C119: Hybrid adds spring stiffness vs identical ICE NA ──────────────

    [Fact]
    public void C119_HybridSpringHigherThanIceNa()
    {
        // Hybrid powertrain applies 1.05× multiplier to both springs before aspiration factor.
        // With Natural aspiration (asp factor = 1.0), ICE gets 1.0×, Hybrid gets 1.05×.
        var hybrid = CarFactory.Porsche918();
        var ice    = CarFactory.Porsche918(); ice.PowertrainType = PowertrainType.ICE;
        var rHybrid = Gen(hybrid, CarFactory.Road());
        var rIce    = Gen(ice,    CarFactory.Road());
        Assert.True(rHybrid.SpringRear >= rIce.SpringRear,
            $"Hybrid spring {rHybrid.SpringRear} should be >= ICE NA {rIce.SpringRear} (1.05× multiplier)");
        Assert.NotNull(rHybrid.CenterDiffBias);
    }

    // ── C120: McLaren 720S — Drift RWD ──────────────────────────────────────

    [Fact]
    public void C120_McLaren720SDrift()
    {
        var r = Gen(CarFactory.McLaren720S(), CarFactory.Drift());
        Assert.True(r.CamberFront <= -2.5,
            $"Drift front camber {r.CamberFront} should be ≤ -2.5°");
        Assert.True(r.ToeRear >= 0,
            $"Drift rear toe {r.ToeRear} should be ≥ 0 (toe-in for stability)");
        Assert.True(r.ARBRear >= r.ARBFront,
            $"Drift: rear ARB {r.ARBRear} should be >= front {r.ARBFront}");
        Assert.InRange(r.DiffAccel, 50, 100);
        Assert.True(r.DiffDecel <= 15,
            $"Drift decel lock {r.DiffDecel} should be ≤ 15");
        Assert.Null(r.DiffFrontAccel);
    }
}
