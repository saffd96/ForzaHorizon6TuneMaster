# AGENTS.md

WPF / .NET 8 desktop app (Windows-only) that generates car tunes for Forza Horizon 6 from an embedded SQLite database extracted from the game files. Root namespace `Forza_Horizon_6_Tune_Master` (underscores, not dots). Russian UI, English secondary.

## Build & Test

```powershell
dotnet build                                                   # Debug (WPF app only — see below)
dotnet build -c Release                                        # Release: no debug symbols, self-contained
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj           # Full xUnit suite
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj --filter "FullyQualifiedName~TuneGeneratorService"  # One class
```

**The `.sln` contains only the WPF app.** Test (`TuneMaster.Tests/`) and validator (`TuneValidator/`) projects are not in it — always reference `.csproj` explicitly. Requires Windows + .NET 8 SDK (WPF). NuGet: `System.Text.Json 8.0.5`, `Microsoft.Data.Sqlite 8.0.5`. No linter/formatter/typecheck config exists.

## Projects

| Path | Type | In `.sln`? |
|---|---|---|
| `Forza Horizon 6 Tune Master/` | WPF app (`WinExe`, `net8.0-windows`) | Yes |
| `TuneMaster.Tests/` | xUnit (`net8.0-windows`, `UseWPF=true`) | No |
| `TuneValidator/` | Console harness (`net8.0`) for formula checks | No |
| `DUMPER/` | Throwaway DB scripts (gitignored) | No |

## Architecture (no DI)

`MainWindow.DataContext = new MainViewModel()`. Services are static singletons or static calculators. INPC hand-rolled via `NotifyBase.Set<T>(ref field, value)`. Commands use custom `RelayCommand`.

**Layer 1 — `Models/SelectedParts.cs`** (~430 lines) holds all upgrade part IDs as nullable ints (`int?`). Fires `PartsChanged` and `CarMassUpdated` events when any part changes. Computes `ComputeTotalMass()` by summing `MassDiff` from every part via `Fh6DatabaseService`. Also computes wheel-tier mass deltas. `SetCarData(car)` initializes resolved IDs (EngineId, DrivetrainId, etc.) from the car.

**Layer 2 — 7 sub-ViewModels** each expose `ObservableCollection<PartOption>` for ComboBox binding: `SwapsVM`, `EngineVM`, `MotorVM`, `SuspensionVM`, `TransmissionVM`, `TiresWheelsVM`, `AeroVisualVM`. When a sub-VM selects a part, it writes to `SelectedParts.*PartId`, which fires `PartsChanged`.

**Layer 3 — Static calculators** in `Services/`. `TuneGeneratorService.Generate(CarCard, TrackInfo, SelectedParts, Fh6DatabaseService)` calls them in order: Power → Aero (iterative, up to 3×) → Tire → Alignment (camber→toe→caster) → ARB → Springs → RideHeight → Dampers → Brakes → Differential → Gearing → LaunchControl (Drag only) → `GearingCalculator.PostValidateAndRecalculate`. Each writes into `TuneResult` and its `Explanations`.

**Layer 4 — `Fh6DatabaseService`** (~1582 lines) singleton. `InitializeAsync` loads all DB tables into `ConcurrentDictionary` at startup (embedded via `..\DUMPER\fh6_db.sqlite`). All dictionaries must remain `ConcurrentDictionary` — parallel tests can race on `InitializeAsync`.

## App startup

`App.OnStartup` → `LocalizationService.InitializeFromSystem()` → `await Fh6DatabaseService.Instance.InitializeAsync()` → `new MainWindow().Show()`. Global `DispatcherUnhandledException` / `UnhandledException` handlers show localized message box.

## DB keying — Ordinal ≠ CarBodyId (#1 source of bugs)

| Key | Value | Used by |
|---|---|---|
| **Ordinal** | `Data_Car.Id` (= `car.CarDbId`) | Engine/spring-damper/brakes/anti-sway/tire-compound/rim/rear-wing upgrades |
| **CarBodyId** | `car.CarDbId × 1000` | Car-body weight, chassis stiffness, tire width/aspect, track spacing, bumpers/skirts |
| **EngineID** | from engine swap | Camshaft, valves, pistons, turbo, etc. |
| **DrivetrainID** | from `List_UpgradeDrivetrain` | Transmission, clutch, driveline, differential |

Drivetrain parts keyed by `DrivetrainID`, *not* `DriveTypeID` (FWD/RWD/AWD). See `DbIntegrationCalculatorTests.cs` for asserted relationships.

## Domain quirks

- Spring rates stored as **N/mm** in DB and `TuneResult` — do *not* multiply by 9.807. XAML binds to `*Display` properties for conversion.
- `CalculationHelpers.EffectiveWtDist`: when `WeightDistributionFront` is 50, engine position overrides (Front→55, Mid→48, Rear→40).
- Aero runs iteratively (up to 3× with recomputed effective max speed).
- Launch control only for `Discipline.Drag`.
- Disciplines: Road, Touge, Rally, CrossCountry, Drift, Drag, Street.

## Localization

`CalculationHelpers.L(key)` is the shorthand in calculators for explanation strings. UI strings in `Localization/ru.json` + `en.json` (add same key to both). Game part names in `Localization/GameStrings.{en,ru}.json`, merged at runtime via `PartDisplayNameResolver`.

## Persistence

All user data under `%APPDATA%\ForzaTuneMaster\`, centralized in `Services/ForzaPaths.cs`. `StorageService` / `ProfileService` serialize `SavedProfile` as indented JSON.

## Testing

- `Helpers/CarFactory.cs` provides preset cars (`DefaultCar`, `FWDStockCar`, `AWDPerformanceCar`, `ElectricCar`), `DefaultTrack`, and `RelaxedConstraints()`.
- **Filesystem isolation**: wrap tests in `TestingEnvironment` (`IDisposable`) — sets `ForzaPaths.SetTestRoot(tempDir)` so profile/cache/settings point at a temp dir deleted on dispose. Never write to real `%APPDATA%`.
- `TuneGeneratorService` has an `[Obsolete]` `Generate(CarCard, TrackInfo, TuningConstraints)` overload for older tests; new code uses the 4-arg version.

## API keys (optional)

`Services/ApiKeys.cs` (gitignored) reads env vars `FH6_CEREBRAS_API_KEY` / `FH6_OPENROUTER_API_KEY`. Copy `ApiKeys.cs.example` to enable AI autofill.

## Leftover legacy

- `TuningConstraints.cs` still exists but is unused in new code path.
- `UnitConverter.cs` still exists (formerly used by old XAML).
- `CarCard` still carries enum properties (`TireType`, `SuspensionUpgrade`, etc.) and some legacy fields — these are vestigial from the pre-DB era and are not populated by the new DB-backed path.
