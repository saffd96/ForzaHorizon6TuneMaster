# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Commands

```powershell
# Build
dotnet build "Forza Horizon 6 Tune Master.sln"

# Run tests (all)
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj

# Run a single test class or method
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj --filter "FullyQualifiedName~CalculatorUnitTests"
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj --filter "FullyQualifiedName~PowerCalculatorBoltOnTests.SingleTurbo_LevelScaling"

# Run the app
dotnet run --project "Forza Horizon 6 Tune Master\Forza Horizon 6 Tune Master.csproj"

# Rebuild the slim DB after adding a new table query to Fh6DatabaseService.cs
#   (requires Python 3 and the full fh6_db.sqlite next to the script)
cd DUMPER && python trim_db.py
```

> **Do not run `dotnet test` in parallel with anything that starts the WPF host.**  
> The project has `UseWPF=true`; the testhost is a WPF app and cannot run multiple instances side-by-side. Use sequential invocations or a single `dotnet test` call.

---

## Solution layout

```
Forza Horizon 6 Tune Master.sln
├── Forza Horizon 6 Tune Master/   ← WPF .NET 8 main app
│   ├── Models/                    ← CarCard, TuneResult, SelectedParts, TuningConstraints, enums
│   ├── ViewModels/                ← MainViewModel + per-category sub-VMs
│   ├── Views/                     ← XAML UserControls and Windows
│   ├── Services/                  ← All calculators + DB + localization + storage
│   └── Localization/              ← en.json / ru.json + GameStrings.*.json (embedded resources)
├── TuneMaster.Tests/              ← xUnit test project; mirrors Services/ namespace
│   └── Helpers/CarFactory.cs      ← Static helpers that build pre-configured CarCard instances
└── DUMPER/                        ← Python DB-extraction toolchain (not part of the build)
    ├── 1_extract.py               ← Reads raw game files, produces fh6_db.sqlite
    ├── 2_str_to_json.py           ← Builds Localization/GameStrings.*.json
    ├── 3_make_gamestrings.py
    └── trim_db.py                 ← Produces the embedded fh6_db.slim.sqlite (see §DB below)
```

---

## Architecture overview

### MVVM pattern

- **`NotifyBase`** — `INotifyPropertyChanged` base with a `Set<T>()` helper used throughout models and ViewModels.
- **`MainViewModel`** owns the active `CarCard`, `TrackInfo`, `SelectedParts`, `TuningConstraints`, and the list of saved `TuneResult`s. It delegates upgrade-selection UI to seven focused sub-ViewModels: `SwapsVM`, `EngineVM`, `MotorVM`, `SuspensionVM`, `TransmissionVM`, `TiresWheelsVM`, `AeroVisualVM`.
- Sub-ViewModels expose `ObservableCollection<PartOption>` lists. Selecting a part writes the part Id back to `SelectedParts`, which fires `PartsChanged` (or `CarMassUpdated`). `MainViewModel.OnPartsChanged` reacts: refreshes constraints bounds, recalculates the tune if auto-calculate is on.

### Core data flow

```
User selects car (CarDbId) → MainViewModel loads CarCard from Fh6DatabaseService
User selects upgrades → SelectedParts fires PartsChanged
MainViewModel calls TuneGeneratorService.Generate(car, track, parts, db, constraints)
  └─ PowerCalculator.Calculate(car, parts)           // sets car.PowerHP, TorqueNm, MaxRPM
  └─ AeroCalculator.CalculateAero (×2–3 iterations) // aero + iterative speed refinement
  └─ TireCalculator, AlignmentCalculator             // pressure, camber, toe, caster
  └─ SuspensionCalculator                            // ARB, springs, ride height, dampers
  └─ BrakeCalculator, DifferentialCalculator
  └─ GearingCalculator.CalculateGearing
  └─ LaunchControlCalculator (drag discipline only)
  └─ GearingCalculator.PostValidateAndRecalculate    // adjusts final drive if needed
→ Returns TuneResult
```

### Fh6DatabaseService (singleton)

`Fh6DatabaseService.Instance` is the single source of truth for all game data. It loads the embedded SQLite (`DUMPER/fh6_db.slim.sqlite` — embedded as `DUMPER.fh6_db.sqlite`) into `ConcurrentDictionary` caches at first access via `InitializeAsync()`.

- All caches use `ConcurrentDictionary` — required because parallel tests all call `InitializeAsync` before the guard flips; a plain `Dictionary` corrupts.
- Forced-induction parts have non-unique IDs across tables. The service offsets each FI kind by `FiKindStride = 100_000_000` at load time so a single `int ForcedInductionPartId` can be unambiguously resolved with `GetForcedInductionById`.
- `CarDatabaseService` (separate) wraps the singleton to project `DbCar` → `CarData` for the UI car list.

### Slim DB pipeline

The app embeds `DUMPER/fh6_db.slim.sqlite` (not the full `fh6_db.sqlite`). `trim_db.py` copies the full DB and drops all tables **not** in its `KEEP_TABLES` set, then VACUUMs.

**Critical:** every time `Fh6DatabaseService.cs` adds a `SELECT … FROM <table>` or `JOIN <table>` that was not previously queried, that table name must be added to `KEEP_TABLES` in `trim_db.py`, and the slim DB must be regenerated. Missing tables fail silently with empty drop-downs or missing physics data.

### TuningPhysicsContext

Resolves the correct `DbSpringDamperPhysics`, `DbAntiSwayPhysics`, `DbUpgradeBrakes`, etc. for the currently selected upgrade level. Falls back to stock-part data when nothing is selected. All calculator internals call this; they never assume the highest-level part.

### TuningConstraints

Holds user-settable min/max for every tunable parameter. `ApplyPhysicsBounds(car, parts, db)` refreshes spring, ride-height, ARB, aero, and brake bounds from the DB for the active car+parts selection. Calculators clamp their outputs to these constraints. The `FinalDriveMin/Max` here (default 2.2–6.0) is the single source of truth — hardcoded constants elsewhere were removed.

### PowerCalculator

`PowerCalculator.Calculate(car, parts)` mutates `car.PowerHP`, `car.TorqueNm`, `car.MaxRPM`, `car.RotationalInertiaFactor`. Key constants:

- `GameRedlineScale = 1.108` — the in-game rev limiter sits 10.8% above the DB `RedlineRPM`. Applied universally. Do not remove.
- Power is **dyno-anchored**: stock `SimPeakPower` from the DB is the baseline; upgrades contribute a delta curve; forced induction adds `max(additive PowerMaxScale×1.341, multiplicative MaxScale)`.
- Electric motors (`AspirationTypeId == 8`) take a separate branch (`CalcElectric`).

### SelectedParts & [ResetToStock]

`SelectedParts` properties tagged `[ResetToStock]` are reset to `null` by `PickStock()` when an engine swap is installed — avoids stale part IDs from a different engine. The `BodyKitPartId` change re-keys every `CarBodyId`-scoped upgrade.

### Localization

`LocalizationService.Instance` is a lazy singleton. Strings are in `Localization/en.json` and `ru.json` (embedded resources). `CalculationHelpers.L(key)` is the shorthand used throughout calculators. `GameStrings.*.json` hold display names for game enum values (aspiration, drivetrain, etc.).

### File I/O & test isolation

`ForzaPaths` uses `AsyncLocal<string?> _testRoot` so each test can redirect all file I/O (profiles, settings, cache) to a per-test temp directory without touching production paths. `TestingEnvironment` wraps this pattern; use `FileSystemTestCollection` for any test class that touches the file system to avoid xUnit parallelism races.

---

## Key numeric invariants (do not change without understanding)

| Constant / fact | Location | Notes |
|---|---|---|
| Spring rates in DB are **N/mm** | `DbSpringDamperPhysics` | Not kgf/mm — multiplying by 9.807 is wrong |
| Spring-rate **display** multiplies the canonical N/mm value by an extra **×10** | `Converters.cs` `UnitValueConverter` ("spring" branch, N/mm and kgf/mm cases), `MainViewModel.SpringFrontMinDisplay`, `SpringFrontMinOverrideText`/`SpringFrontMaxOverrideText`/rear equivalents | **Confirmed 2026-07 as correct/intentional, not a bug** — a prior session (this same commit range) flagged it as a regression from `4419e92` "revert x10" and nearly reverted it; do not "fix" this again without new evidence. The lbs/in branch does not carry the ×10 (unaffected). Any new spring-rate display code must match this ×10 to stay consistent with what's already on screen |
| `GameRedlineScale = 1.108` | `PowerCalculator` | Game rev limiter vs DB redline |
| `BodyAeroLongitudinalDrag` is **not Cd×A** | `CalculationHelpers` | Use `CarCard.CdABodyEstimate` for aero/top-speed math |
| Wheel-style weight delta uses `MassLevel` (tier) | `Fh6DatabaseService` | Formula: tier×D²×W×4.99e-5, relative to stock tier. Refitted 2026-07 against 4 real in-game readings on an FXXK Evo at two different wheel fitment sizes (confirms the D²×W shape itself, not just the constant — residual ≤0.6kg at both sizes) — the old 6.8e-5 overshot by 24-34%. `List_Wheels.Mass` (the raw per-wheel-style kg field) is NOT what drives this — same-tier wheels with different `Mass` (e.g. XD9 Mass=30 vs stock Mass=38, both MassLevel=4) show **zero** weight change in-game, confirming only the tier comparison matters, not the raw mass. Note: tire aspect-ratio changes carry a real in-game mass cost with no corresponding DB column anywhere (`List_UpgradeCarBodyTireAspectRatioFront/Rear` has no MassDiff field) — unmodeled, ~2-6kg per change observed, not fixable from available data |
| Tire width `MassDiff` (DB) undershoots real growth by ~2.6x | `SelectedParts.TireWidthMassCoef` | **Correction (2026-07) of an earlier, wrong "matches within rounding" claim.** Isolating width alone (Toyota GT86 and Ferrari FXXK Evo WP both expose only ONE `TireAspectRatioFront/Rear` row in the DB — profile is a fixed function of width, not an independently selectable option, so no confound from a separate aspect-ratio effect) across 9 real in-game readings, `List_UpgradeCarBodyTireWidthFront/Rear.MassDiff` consistently undershoots by ~2.6x (least-squares fit through origin: k=2.596). Applied as `TireWidthMassCoef=2.6` in `SelectedParts.ComputeTotalMass`. Rim **diameter** (`RimSize.MassDiff`) is a separate, unrelated table/effect — do not conflate; see below, it does NOT get this same correction (inconsistent across cars, see next row) |
| Rim diameter `MassDiff` (DB) undershoots real growth by ~1.10x | `SelectedParts.RimSizeMassCoef` | Early 1-2-step isolated-diameter tests on 3 cars (Sprinter, GT86, FXXK Evo) gave wildly inconsistent ratios (0.7x-2.2x) — turned out to be whole-kg display rounding dominating such small deltas, not a real absence of pattern. Resolved 2026-07 with an 8-step test on a Toyota Tacoma TRD Pro (16" stock, all 8 rim-size levels 16"->24", front+rear together, profile held stock): cumulative deltas up to +25 kg give a tight, consistent fit (ratio 0.92-1.15 across all 8 points). Combined least-squares fit through the origin (Tacoma's 8 points + GT86's 2 + FXXK Evo's 2, all on the stock-tier wheel so `WheelTierMassDiff` contributes nothing): k=1.10. Applied as `RimSizeMassCoef=1.10` in `SelectedParts.ComputeTotalMass`. **One car remains a confirmed outlier**: 1985 Toyota Sprinter FE (ordinal 4162, stock fitment 13"), isolated 13"->14" (both axles, profile held stock) gives +6.0 kg real vs +3.0 kg predicted with k=1.10 — 2x over, while the other 3 cars agree with k=1.10 within rounding. Not accommodated in code (per-car workaround would contradict 3-car consensus); flagged here in case more Sprinter-like outliers turn up |
| Body-kit's own mass delta lives in per-body `InitialMass`, not its `MassDiff` | `SelectedParts.BodyKitCurbWeightDiff` | `List_UpgradeCarBody.MassDiff` is ~always 0; the real kit weight is `List_UpgradeCarBodyWeight`'s stock `InitialMass` for the kit's CarBodyID minus the stock body's (can differ 100+ kg, e.g. a stripped race shell). The body-scoped hood (`List_UpgradeCarBodyHood`) is a separate, additive delta (carbon-hood choice), not the kit's own mass source |
| Intercooler base-tier subtraction must use `IsStock`, not lowest `Level` | `SelectedParts.ComputeTotalMass` (intercooler block) | When FI is installed, the intercooler's own `MassDiff` has a "free baseline" subtracted so the kit's bundled base IC isn't double-counted. **Bug fixed 2026-07**: the code subtracted the lowest-*Level* row's `MassDiff` unconditionally — correct only for the 351/620 FI-capable engines whose intercooler list has a genuine `IsStock` row (always `MassDiff=0` there, verified DB-wide, so old/new behavior is identical for them). The other 269/620 (43%) have **no** `IsStock` intercooler row at all — every tier for them is a real, separately-required part (~20-35 kg), so subtracting "lowest Level" silently wrote off real mass on every FI build. Fixed to subtract `allIcs.FirstOrDefault(x => x.IsStock)?.MassDiff ?? 0` (0 when a stock row exists, nothing when it doesn't) |
| Wheel `Mass` (kg, `List_Wheels`) feeds rotational inertia, not curb weight | `TuningPhysicsContext.ComputeRotationalInertiaFactor` | I≈0.6·m·r² per wheel (2/axle), r = car's actual rolling radius (rim+tyre sidewall) so tyre profile still matters even though only rim mass is counted (no tyre-mass field exists anywhere in the DB). **`RotationalInertiaFactor` only feeds an explanation string (`Expl_Inertia`) — it has never scaled PowerHP/TorqueNm/gearing anywhere in the codebase**, by original design (`Expl_Inertia` literally says "peak power unchanged"); adding wheels here changes that displayed text on wheel-style changes, not any computed stat. **Tried making it scale PowerHP/TorqueNm (2026-07), reverted**: the one real-game reading that seemed to need it (calc 1385 vs real 1599 hp on a built Sprinter) turned out to be a mismatched forced-induction level in a saved profile, not a missing effect — the correct FI level already hits `EngineGraphingMaxPower` (the DB ceiling) exactly with no inertia scaling needed, and applying it on top pushed a maxed build's power past its own documented ceiling |
| Stock part `Level` is inconsistent across swaps | `SelectedParts` | On engine swap, reset parts to stock via `PickStock()`, don't preserve level |
| FI part IDs shifted by `FiKindStride` | `Fh6DatabaseService` | Single: +100M, Twin: +200M, CSC: +300M, DSC: +400M |

---

## Testing conventions

- `CarFactory` (in `TuneMaster.Tests/Helpers/`) builds fully-configured `CarCard` instances for common test scenarios (`DefaultCar`, `FWDStockCar`, `AWDPerformanceCar`, etc.). Prefer adding a factory method over duplicating boilerplate in individual tests.
- Calculator tests that require the DB use `Fh6DatabaseService.Instance` directly (integration-style). Calculator logic tests that don't touch the DB use `CarFactory` plus manual property assignment.
- Validate `PowerCalculator` results against DB fields (`SimPeakPower`, `EngineGraphingMaxPower`) — not against in-game measurements.
- Bug fixes should use a general formula that works across all cars, not a per-car workaround. Validate on a representative spread of cars (check `DbIntegrationCalculatorTests`).
