# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

WPF / .NET 8 desktop app (Windows-only) that generates car tunes for Forza Horizon 6. The user picks a car, installs upgrade parts, chooses a discipline + season, and the app computes a full tune (tire pressure, springs, dampers, ride height, alignment, ARB, gearing, differential, brakes, aero, launch control). Car specs and all upgrade parts come from an **embedded SQLite database extracted from the game files**.

Root namespace is `Forza_Horizon_6_Tune_Master` (underscores, not dots). UI language is Russian; English is the secondary locale.

## Build & Test

```powershell
dotnet build                                                   # Debug (builds only the WPF app — see note)
dotnet build -c Release                                        # Release: no debug symbols, self-contained extract flags
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj           # Full xUnit suite
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj --filter "FullyQualifiedName~TuneGeneratorService"  # One class
```

Requires Windows + .NET 8 SDK (WPF). NuGet deps: `System.Text.Json 8.0.5`, `Microsoft.Data.Sqlite 8.0.5`.
There is **no linter / formatter / typecheck config** in the repo.

**The `.sln` contains only the WPF app.** The test and validator projects are not in it, so `dotnet test` / `dotnet build` at the solution level won't touch them — always reference `TuneMaster.Tests\TuneMaster.Tests.csproj` explicitly.

## Projects

| Path | Type | In `.sln`? |
|---|---|---|
| `Forza Horizon 6 Tune Master/` | WPF app (`WinExe`, `net8.0-windows`) | Yes |
| `TuneMaster.Tests/` | xUnit (`net8.0-windows`, `UseWPF=true`), references the app | No |
| `TuneValidator/` | Console harness (`net8.0`) for manual formula checks | No |
| `DUMPER/`, `tmpquery/` | Throwaway DB-inspection scripts (`DUMPER/` is gitignored) | No |

## Architecture

**No DI container.** `MainWindow.DataContext = new MainViewModel()`; services are static singletons (`Fh6DatabaseService.Instance`, `LocalizationService.Instance`) or static calculator classes. INPC is hand-rolled via `Models/NotifyBase.cs` (`Set<T>(ref field, value)` + `OnPropertyChanged`) — no CommunityToolkit. Commands use a custom `RelayCommand` (with `Raise()` to re-fire `CanExecuteChanged`).

**Three layers:**

1. **`ViewModels/`** — `MainViewModel` (~1450 lines) is the hub. Per-tab sub-viewmodels handle part selection by category: `SwapsViewModel`, `EnginePartsViewModel`, `MotorPartsViewModel` (electric), `SuspensionViewModel`, `TransmissionViewModel`, `TiresWheelsViewModel`, `AeroVisualViewModel`. `CarSpecController` coordinates async car-spec autofill (AI / Forza wiki). All selected part IDs live in `Models/SelectedParts.cs`.

2. **`Services/` calculators** — `TuneGeneratorService.Generate(CarCard, TrackInfo, SelectedParts, Fh6DatabaseService)` is a thin orchestrator (~50 lines). It refreshes power (`PowerCalculator`) then calls each domain calculator in order: Aero → Tire → Alignment (camber/toe/caster) → ARB → Springs → RideHeight → Dampers → Brakes → Differential → Gearing → (Drag-only LaunchControl) → `GearingCalculator.PostValidateAndRecalculate`. Calculators are `static`; each writes side effects into the shared `TuneResult` and its `Explanations` dictionary.

3. **`Services/Fh6DatabaseService`** — singleton loading the embedded SQLite into `ConcurrentDictionary` lookups at startup. `DbSchema.cs` holds the row records (`Db*` types).

## App startup (`App.xaml.cs`)

`OnStartup` → `LocalizationService.InitializeFromSystem()` → `await Fh6DatabaseService.Instance.InitializeAsync()` → `new MainWindow().Show()`. Global `DispatcherUnhandledException` / `UnhandledException` handlers show a localized message box.

## Data flow (car → tune)

1. User selects a car → `MainViewModel.SelectedCar` setter → `CarSpecController` populates `CarCard` from the DB (`CarDbId`, `CarBodyId = CarDbId * 1000`, engine/body/drivetrain IDs).
2. `MainViewModel` loads the 7 sub-VMs; each queries `Fh6DatabaseService` for the parts applicable to that car and exposes them as `PartOption` lists for ComboBoxes.
3. User edits parts / discipline / season; `GenerateCommand` → `TuneGeneratorService.Generate(...)` → `TuneResult`, which the result view binds to.

## DB keying — Ordinal ≠ CarBodyId (the #1 source of bugs)

The embedded DB keys upgrade tables by several different schemes. Mixing them up produces empty dropdowns.

| Key | Value | Source | Used by |
|---|---|---|---|
| **Ordinal** | `Data_Car.Id` (raw) | `car.CarDbId` | engine/spring-damper/brakes/anti-sway/tire-compound/rim/rear-wing upgrades |
| **CarBodyId** | `Data_Car.Id × 1000` | `car.CarBodyId` | car-body weight, chassis stiffness, tire width/aspect, track spacing, bumpers/skirts/hood |
| **EngineID** | from the selected engine swap | — | all `*ByEngineId` engine-internal upgrades (camshaft, valves, pistons, turbo, …) |
| **DrivetrainID** | from `List_UpgradeDrivetrain` | — | transmission, clutch, driveline, differential |

Use `car.CarDbId` for Ordinal lookups and `car.CarBodyId` for CarBodyId lookups. **Never pass a CarBodyId where an Ordinal is expected.** Drivetrain parts are keyed by the car's specific `DrivetrainID`, *not* by `DriveTypeID` (which only says FWD/RWD/AWD). See `DbIntegrationCalculatorTests.cs` for the asserted relationships.

The DB is embedded via `<EmbeddedResource Include="..\DUMPER\fh6_db.sqlite" LogicalName="DUMPER.fh6_db.sqlite">`. `Fh6DatabaseService.InitializeAsync` extracts it to a temp file, reads all tables once under a double-checked lock, and stores them in `ConcurrentDictionary` collections — **keep these concurrent**: parallel tests can invoke `InitializeAsync` from multiple threads before the `_initialized` guard is set, and a plain `Dictionary` corrupts under concurrent writes.

## Domain conventions (verify against code before relying on these)

- **Units are canonical in storage, converted only for display.** Spring rates are stored in **N/mm** in both the DB and `TuneResult` — do *not* multiply by 9.807 (a past bug treated them as kgf/mm). XAML binds to `*Display` properties; conversion happens there.
- **Engine position drives weight distribution defaults.** `CalculationHelpers.EffectiveWtDist`: when `WeightDistributionFront` is left at its default `50`, engine position overrides it (Front→55, Mid→48, Rear→40).
- **Aero is iterative** (speed-dependent downforce): `CalculateAero` runs up to 3× with a recomputed effective max speed.
- **Gearing is post-validated**: `PostValidateAndRecalculate` re-runs geometry/max-speed convergence after the main pass.
- **Launch control** is computed only for `Discipline.Drag`.
- Disciplines: Road, Touge, Rally, CrossCountry, Drift, Drag, Street (`Models/Enums.cs`).

## Localization

`LocalizationService.Instance` is the singleton; `CalculationHelpers.L(key)` is the shorthand used inside calculators for explanation strings. UI strings live in `Localization/ru.json` + `en.json` (add the same key to both). Game string tables (part/engine/drivetrain names extracted from the game's `.str` files) are embedded as `Localization/GameStrings.{en,ru}.json` and merged at runtime; `PartDisplayNameResolver` maps DB parts to their localized in-game names, falling back to generic `"Stock <Category>"` / `"<Category> Stage N"`.

## Persistence & paths

All user data lives under `%APPDATA%\ForzaTuneMaster\`, centralized in `Services/ForzaPaths.cs` (`ProfilesDir`, `CachePath`, `SettingsPath`). `StorageService` / `ProfileService` serialize `SavedProfile` (Car + Track + Constraints + last result) as indented JSON; profile names are sanitized against `Path.GetInvalidFileNameChars()`.

## Testing

- Helpers in `TuneMaster.Tests/Helpers/CarFactory.cs` provide preset cars (`DefaultCar`, `FWDStockCar`, `AWDPerformanceCar`, `ElectricCar`), `DefaultTrack`, and `RelaxedConstraints()` (wide bounds so calculators aren't clamped).
- **Filesystem isolation**: wrap a test in `TestingEnvironment` (`IDisposable`) — its ctor calls `ForzaPaths.SetTestRoot(tempDir)` (an `AsyncLocal` override) so profile/cache/settings paths point at a temp dir that's deleted on dispose. Never let tests write to the real `%APPDATA%`.
- `TuneGeneratorService` keeps an `[Obsolete]` `Generate(CarCard, TrackInfo, TuningConstraints)` overload purely for older tests; new code uses the 4-arg `Generate` with `SelectedParts` + the DB.

## API keys

`Services/ApiKeys.cs` (gitignored) supplies AI autofill keys, reading env vars `FH6_CEREBRAS_API_KEY` / `FH6_OPENROUTER_API_KEY`. Copy from `ApiKeys.cs.example` to build with autofill enabled.
