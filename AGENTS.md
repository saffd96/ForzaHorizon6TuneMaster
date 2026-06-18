# Forza Horizon 6 Tune Master

.NET 8 WPF app that generates car tunes from vehicle specs + track discipline + user constraints. Parts are selected through per-category sub-viewmodels backed by an embedded SQLite database.

## Build & Test

```powershell
dotnet build                                                   # Debug
dotnet build -c Release                                        # Release (strips debug symbols)
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj           # All ~23 test files
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj --filter "FullyQualifiedName~TuneGeneratorService"  # Single class
```

Windows + .NET 8 SDK required (WPF is Windows-only). Only NuGet dep: `System.Text.Json 8.0.5`.
**No linter, formatter, or typecheck config exists in repo.**

## Projects

| Project | Type | In `.sln`? |
|---|---|---|
| `Forza Horizon 6 Tune Master/` | WPF app (WinExe, `net8.0-windows`) | Yes |
| `TuneMaster.Tests/` | xUnit (`net8.0-windows`, `UseWPF=true`) | No — run `.csproj` directly |
| `TuneValidator/` | Console harness (`net8.0`) | No — manual formula verification |

## Architecture

- **TuneGeneratorService** (42 lines) — thin orchestrator. Heavy formulas live in per-domain `static` calculators under `Services/`: `AeroCalculator`, `TireCalculator`, `SuspensionCalculator`, `AlignmentCalculator`, `BrakeCalculator`, `DifferentialCalculator`, `GearingCalculator`, `LaunchControlCalculator`. Only 3 `switch` on `Discipline` remain (Aero, Differential, Tire).
- **MainViewModel** (~1321 lines) — single ViewModel, constructs services inline (no DI). Implements `INotifyPropertyChanged` directly.
- **No DI**: `MainWindow.DataContext = new MainViewModel()`. Custom `RelayCommand` with `Raise()` for manual `CanExecuteChanged`.
- **INPC**: Custom `NotifyBase` with `Set<T>(ref field, value)`. No CommunityToolkit.
- **Root namespace**: `Forza_Horizon_6_Tune_Master` (underscores, not dots).
- **Empty dirs**: `Services/Abstractions/` and `Data/` are unused.

## Data Flow

1. User selects car from `CarListBox` → `MainViewModel.SelectedCar` setter → `CarSpecController.SelectCar()` → `PopulateCarFromDb()` fills `CarCard` (CarDbId, CarBodyId, EngineDbId, etc.)
2. `LoadSubViewModels()` creates `SelectedParts`, calls `SetCarData()`, then loads all 7 sub-VMs: SwapsVM, EnginePartsVM, MotorPartsVM, SuspensionVM, TransmissionVM, TiresWheelsVM, AeroVisualVM
3. Each sub-VM queries `Fh6DatabaseService` for applicable parts using `car.CarDbId` (Ordinal, e.g. 247) or `car.CarBodyId` (CarBodyId, e.g. 247000)
4. User adjusts parts, fills discipline + season in `TrackInfoView`
5. Clicks "Сгенерировать" → `MainViewModel.GenerateCommand` → `TuneGeneratorService.Generate(Car, Track, Constraints)` → `TuneResult`
6. `TuneResultView` binds to `MainViewModel.TuneResult`

## Key Entrypoints

- `App.xaml.cs` / `MainWindow.xaml.cs` — app startup
- `Services/TuneGeneratorService.cs` — orchestrates calculators
- `Services/CalculationHelpers.cs` — shared constants (`SpringHzToNmm = 0.019739`, baselines) + utilities (`EffectiveWtDist`, `GetSeasonGripFactor`, `ComputeEffectiveMaxSpeedKmh`, `L(key)` shorthand)
- `ViewModels/MainViewModel.cs` — all UI logic, calls `LoadSubViewModels()` in `SelectedCar` setter
- `ViewModels/CarSpecController.cs` — async car spec fetch coordination
- `Services/StorageService.cs` — also contains `SavedProfile` class (not in Models/)
- `Services/Fh6DatabaseService.cs` — embedded SQLite DB, loads all part tables on startup
- `Services/PartDisplayNameResolver.cs` — resolves `DbUpgradePart` → localized display name using `Data_UpgradePart.PartName` + `Localization/ru.json` / `en.json`
- `ViewModels/SwapsViewModel.cs` — engine swaps, forced induction (hidden for stock engines without stock FI)
- `ViewModels/EnginePartsViewModel.cs` — camshaft, displacement, valves, pistons, fuel, ignition, exhaust, intake, flywheel, manifold, oil cooling, restrictor, intercooler
- `ViewModels/MotorPartsViewModel.cs` — motor swaps + motor upgrades (electric cars only)
- `ViewModels/SuspensionViewModel.cs` — spring dampers, brakes, tire compounds, anti-roll bars
- `ViewModels/TransmissionViewModel.cs` — transmission, clutch, driveline, differential
- `ViewModels/TiresWheelsViewModel.cs` — tire compounds, widths, profiles, rims, track spacing
- `ViewModels/AeroVisualViewModel.cs` — rear wings, weight reduction, chassis stiffness
- `Models/SelectedParts.cs` — holds all selected part IDs, `SetCarData()` sets EngineId/DrivetrainId/CarBodyOrdinal
- `Models/PartOption.cs` — `Id` + `DisplayName` + `IsStock` for ComboBox binding
- `Converters/Converters.cs` — contains `CountToVisibilityConverter` (hides empty ComboBoxes)

## Calculator Patterns

- Each `Calculate*` method signature: `(CarCard, TrackInfo, TuningConstraints, TuneResult, Dictionary<string,string> explanations)` — side effects only to `TuneResult` and `Explanations`.
- Calculators are now DB-driven / physics-first: springs from target natural frequency using `List_SpringDamperPhysics` bounds, ARB from `List_AntiSwayPhysics`, aero from `List_AeroPhysics`, diff/gearing/brakes from drivetrain DB parts, tire pressure from `List_TireCompound`. Synthetic/test cars without DB records fall back to plausible physics-based defaults.
- Aero convergence: `CalculateAero` called up to 3× iteratively (speed-dependent downforce).
- Post-validation: `GearingCalculator.PostValidateAndRecalculate()` runs geometric gear fix + max-speed reconvergence (up to 3 iterations).
- `CalculationHelpers.EffectiveWtDist()`: if `WeightDistributionFront == 50.0` (default, never set), engine position overrides: Front→55, Mid→48, Rear→40.

## Test Pattern

Tests use `TuneMaster.Tests/Helpers/CarFactory.cs` (preset cars, tracks, `RelaxedConstraints()`):
```csharp
var result = new TuneGeneratorService().Generate(
    CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());
```
**Test isolation**: `TestingEnvironment` class calls `ForzaPaths.SetTestRoot()` to redirect `%APPDATA%` paths to a temp dir (disposed after test).

## Profile Persistence

- `SavedProfile` (Car + Track + Constraints + LastResult) serialized as indented JSON to `%APPDATA%\ForzaTuneMaster\profiles\<name>.json`.
- Profile names sanitized by replacing `Path.GetInvalidFileNameChars()` with underscores.
- Current `ProfileVersion = "v2.0"` (in `StorageService.cs:12`).
- `ForzaPaths` static class centralizes all `%APPDATA%` paths.

## UI Conventions

- **Language**: Russian. Add text in `Localization/ru.json` + same key in `en.json`.
- **Game strings**: Selected Forza Horizon 6 string tables (`Upgrades`, `List_DriveType`, `List_Aspiration`, `List_PartManufacturer`, `List_EngineConfig`, `List_EnginePlacement`, `List_CarMake`) are extracted from the official `.str` archives and embedded as `Localization/GameStrings.en.json` and `Localization/GameStrings.ru.json`. `LocalizationService` merges them at runtime using flattened keys like `Upgrades_IDS_Name_78`. Regenerate them with `tools/extract_game_strings.py`.
- **Part display names** use exact game strings via `PartDisplayNameResolver` whenever a reliable mapping exists (engine/motor names from `Data_Engine`/`Data_Motor`; drivetrain swaps; all upgrade categories from `Upgrades` `IDS_Name_*`). Fallback to `"Stock <Category>"` / `"<Category> Stage N"` only when no game string is available.
- **Unit conversion**: XAML binds to `*Display` properties. `UnitValueConverter` (MultiBinding) with `ConverterParameter`: `"pressure"`, `"spring"`, `"height"`, `"speed"`, `"mass"`, `"power"`.
- **Canonical storage**: Power→HP, Speed→km/h, Mass→kg, Spring→N/mm, Pressure→bar, Height→mm. Conversion only in `*Display` properties.
- **Enum ↔ RadioButton**: `EnumToBoolConverter` TwoWay; `ConvertBack` returns `Binding.DoNothing` for unchecked.
- **Numeric input**: `NumericBehavior.IsNumeric` attached property (normalises comma→dot).
- **AWD differential**: `DiffFrontAccel/Decel` and `CenterDiffBias` are `double?` (non-null only for AWD). `HasAWDFrontDiff` controls XAML visibility.
- **Theme**: `Resources/DarkTheme.xaml`. Never set `Height` on buttons — styles use auto-sizing via `Padding`.
- **`[JsonIgnore]`** on all computed properties in model classes.

## Key State Flags in MainViewModel

- `_isLoadingProfile` — suppresses async wiki/AI spec fetches during profile deserialization.
- `CarCard.AllowGearCalculation` / `OnlyFinalDriveCalculation` — gates full gear ratio output.
- `CarCard.CdA` — computed from `Cd × FrontalAreaM2` or estimated.

## API Keys

`Services/ApiKeys.cs` reads env vars `FH6_CEREBRAS_API_KEY` / `FH6_OPENROUTER_API_KEY` with hardcoded fallbacks. `.gitignore` excludes `**/Services/ApiKeys.cs`; use `ApiKeys.cs.example` as template. **Bug**: `ApiKeys.OpenRouter` error message says "Cerebras API key not found" (copy-paste error).

## Part DB Keying (CRITICAL — Ordinal ≠ CarBodyId)

The embedded SQLite uses **two different key schemes** you MUST distinguish:

| Key type | Value | Example | Tables |
|---|---|---|---|
| `Ordinal` | `Data_Car.Id` (no `* 1000`) | `247` | `List_UpgradeEngine`, `List_UpgradeRearWing`, `List_UpgradeSpringDamper`, `List_UpgradeBrakes`, `List_UpgradeAntiSway{Front,Rear}`, `List_UpgradeTireCompound`, `List_UpgradeRimSize{Front,Rear}` |
| `CarBodyId` | `Data_Car.Id * 1000` | `247000` | `List_UpgradeCarBodyWeight`, `List_UpgradeCarBodyChassisStiffness`, `List_UpgradeCarBodyTireWidth{Front,Rear}` |
| `EngineID` | from swap's `EngineID` | `1` | All `List_UpgradeEngine*` tables (camshaft, displacement, valves, etc.) |
| `DrivetrainID` | specific drivetrain config from `List_UpgradeDrivetrain` | varies | `List_UpgradeDrivetrain*` tables |

**Always use `car.CarDbId` for Ordinal lookups and `car.CarBodyId` for CarBodyId lookups.** Never use `parts.CarBodyOrdinal` (= CarBodyId) as Ordinal — this is the most common bug (was the root cause of empty dropdowns).

**Drivetrain parts** (transmission, clutch, driveline, differential) are keyed by the car's specific `DrivetrainID` from `List_UpgradeDrivetrain` (stock entry or selected drivetrain swap), **not** by `Data_Car.DriveTypeID`. `Data_Car.DriveTypeID` only tells you FWD/RWD/AWD.
- `SelectedParts.DrivetrainSwapPartId` controls the selected drivetrain swap. When it changes, `Car.DriveTypeID` and `Car.DriveType` are updated, and transmission/differential parts are reloaded for the new `DrivetrainID`.

## Gotchas

- `.sln` only contains the WPF app — `dotnet test` at solution level won't see test/validator projects.
- `CLAUDE.md` exists but contains stale claims (e.g., references to `HtmlAgilityPack` dependency that doesn't exist in any `.csproj`, forward-slash paths). Cross-reference with this file.
- `TuningConstraints` min/max setters auto-correct paired bounds via `SetMinMax()` / `SetMaxMin()` using `[CallerMemberName]` — never bypass setters.
- `LaunchControlRpm` populated only for Drag discipline.
- `TuningConstraints` now exposes `ApplyPhysicsBounds(CarCard, SelectedParts, Fh6DatabaseService)`; `MainViewModel` calls it after loading sub-VMs and on part changes so slider bounds match the selected car's actual DB physics.
- `CarPartNames` table only has `ID, PartName` columns (cosmetic parts only: bumper, wing, exhaust). Upgrade part names come from `Data_UpgradePart.PartName` + localization; missing localization falls back to the raw `PartName`.
- Test project: 563 total tests, 556 passed, 7 skipped (CarDatabaseService cache tests — no test cache), 0 failed.
