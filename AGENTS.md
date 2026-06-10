# Forza Horizon 6 Tune Master

.NET 8 WPF app that generates car tunes from vehicle specs + track discipline + user constraints.

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
- **MainViewModel** (~1226 lines) — single ViewModel, constructs services inline (no DI). Implements `INotifyPropertyChanged` directly.
- **No DI**: `MainWindow.DataContext = new MainViewModel()`. Custom `RelayCommand` with `Raise()` for manual `CanExecuteChanged`.
- **INPC**: Custom `NotifyBase` with `Set<T>(ref field, value)`. No CommunityToolkit.
- **Root namespace**: `Forza_Horizon_6_Tune_Master` (underscores, not dots).
- **Empty dirs**: `Services/Abstractions/` and `Data/` are unused.

## Data Flow

1. User fills `CarCardView` + selects discipline + season in `TrackInfoView`
2. Clicks "Сгенерировать" → `MainViewModel.GenerateCommand` → `TuneGeneratorService.Generate(Car, Track, Constraints)` → `TuneResult`
3. `TuneResultView` binds to `MainViewModel.TuneResult`

## Key Entrypoints

- `App.xaml.cs` / `MainWindow.xaml.cs` — app startup
- `Services/TuneGeneratorService.cs` — orchestrates calculators
- `Services/CalculationHelpers.cs` — shared constants (`SpringHzToNmm = 0.019739`, baselines) + utilities (`EffectiveWtDist`, `GetSeasonGripFactor`, `ComputeEffectiveMaxSpeedKmh`, `L(key)` shorthand)
- `ViewModels/MainViewModel.cs` — all UI logic
- `ViewModels/CarSpecController.cs` — async car spec fetch coordination
- `Services/StorageService.cs` — also contains `SavedProfile` class (not in Models/)

## Calculator Patterns

- Each `Calculate*` method signature: `(CarCard, TrackInfo, TuningConstraints, TuneResult, Dictionary<string,string> explanations)` — side effects only to `TuneResult` and `Explanations`.
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
- Current `ProfileVersion = "v1.41"` (in `StorageService.cs:11`).
- `ForzaPaths` static class centralizes all `%APPDATA%` paths.

## UI Conventions

- **Language**: Russian. Add text in `Localization/ru.json` + same key in `en.json`.
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

## Gotchas

- `.sln` only contains the WPF app — `dotnet test` at solution level won't see test/validator projects.
- `CLAUDE.md` exists but contains stale claims (e.g., references to `HtmlAgilityPack` dependency that doesn't exist in any `.csproj`, forward-slash paths). Cross-reference with this file.
- `TuningConstraints` min/max setters auto-correct paired bounds via `SetMinMax()` / `SetMaxMin()` using `[CallerMemberName]` — never bypass setters.
- `LaunchControlRpm` populated only for Drag discipline.
