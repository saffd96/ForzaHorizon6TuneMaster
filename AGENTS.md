# Forza Horizon 6 Tune Master

.NET 8 WPF app that generates car tunes from car data + track discipline + user constraints.

## Build & Test

```powershell
dotnet build                                                   # Debug build (all 3 projects)
dotnet build -c Release                                        # Release build (stripped debug symbols)
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj           # Run xUnit tests (~24 files)
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj --filter "FullyQualifiedName~TuneGeneratorService"  # Single test class
```

Windows + .NET 8 SDK required (WPF is Windows-only). Only NuGet dep: `System.Text.Json 8.0.5`.

## Projects

| Project | Type | In `.sln`? |
|---|---|---|
| `Forza Horizon 6 Tune Master/` | WPF app (WinExe, `net8.0-windows`) | Yes |
| `TuneMaster.Tests/` | xUnit (`net8.0-windows`, `UseWPF=true`) | **No** — run `.csproj` directly |
| `TuneValidator/` | Console harness (`net8.0`) | **No** — manually-run formula verification |

## Entrypoints

- `Services/TuneGeneratorService.cs` (~1650 lines) — single class, all private static `Calculate*` methods. 3 `switch` on `Discipline`; DriveType used via switch expressions throughout. Adding a discipline requires touching every `switch`.
- `ViewModels/MainViewModel.cs` (~1350 lines) — single ViewModel, constructs services inline (no DI).
- `Models/CarCard.cs` (~375 lines), `Models/TuneResult.cs` (~70 lines), `Models/TuningConstraints.cs` (~160 lines).

## Data Flow

1. User fills `CarCardView` + selects discipline + season in `TrackInfoView`
2. Clicks "Сгенерировать" → `MainViewModel.GenerateCommand` → `TuneGeneratorService.Generate(Car, Track, Constraints)` → `TuneResult`
3. `TuneResultView` binds to `MainViewModel.TuneResult` (header shows discipline + season + drive type)

## Test Pattern

Tests use `TuneMaster.Tests/Helpers/CarFactory.cs` (preset cars, tracks, `RelaxedConstraints()`):
```csharp
var result = new TuneGeneratorService().Generate(
    CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());
```

## Architecture

- **No DI**: `MainWindow.DataContext = new MainViewModel()` — services constructed inline.
- **INPC**: Custom `NotifyBase` with `Set<T>(ref field, value)`. No CommunityToolkit.
- **Enums** (`Models/Enums.cs`): 7 disciplines (`Road, Touge, Rally, CrossCountry, Drift, Drag, Street`), `DriveType`, `Season` (4), `TireType` (9), `SuspensionUpgrade` (7), `DifferentialUpgrade` (7), `SpringUnit {KgfMm, NMm, LbsIn}`.
- **Root namespace**: `Forza_Horizon_6_Tune_Master` (underscores, not dots).
- **RelayCommand** (`ViewModels/RelayCommand.cs`): simple `ICommand` wrapper with `Raise()` for manual `CanExecuteChanged`.
- **Canonical storage units** (models store metric; conversion only in `*Display` properties):
  - Power → HP, Speed → km/h, Mass → kg, Spring → N/mm, Pressure → bar, Height → mm
- **Profile persistence**: `StorageService` saves `SavedProfile` as indented JSON to `%APPDATA%\ForzaTuneMaster\profiles\<name>.json`. Spaces in names stored as underscores on disk. Current `ProfileVersion` = `"v1.4"` (was `"1.3"` before Season support). Displayed as `VersionTag` in top-right corner.
- **Key services**: `LocalizationService` (singleton, embedded `Localization/{ru,en}.json`), `AiCarSpecService` (Cerebras/OpenRouter), `WikiCarSpecService` (Forza Fandom wiki parser, cached at `%APPDATA%\ForzaTuneMaster\specs\*.json`).
- `Services/Abstractions/` and `Data/` exist but are empty.

## UI Conventions

- **Language**: Russian. Add UI text in `Localization/ru.json` + matching key in `en.json`.
- **Unit conversion**: XAML binds to `*Display` properties on MainViewModel (e.g., `TirePressureFrontMinDisplay`). `UnitValueConverter` (MultiBinding) with `ConverterParameter`: `"pressure"`, `"spring"`, `"height"`, `"speed"`, `"mass"`, `"power"`.
- **Enum ↔ RadioButton**: `EnumToBoolConverter` TwoWay; `ConvertBack` returns `Binding.DoNothing` for unchecked.
- **Numeric input**: `NumericBehavior.IsNumeric` attached property (normalises comma→dot).
- **AWD differential**: `DiffFrontAccel/Decel` and `CenterDiffBias` are `double?` (non-null only for AWD). `HasAWDFrontDiff` controls XAML visibility.
- **Theme**: `Resources/DarkTheme.xaml`. Never set `Height` on buttons — styles use auto-sizing via `Padding`.
- **`[JsonIgnore]`** on all computed properties in model classes.

## Formula Quirks

- **EffectiveWtDist**: if `WeightDistributionFront` == 50.0 (default, never set), engine position overrides: Front→55, Mid→48, Rear→40.
- **SuspensionUpgrade multiplier**: Race=1.10×, Sport=1.00×, Street=0.88×, Rally varies (offroad disc. 0.85, else 0.55), Drift=0.85×.
- **Spring constant**: `k = 0.02012 × Hz² × mass × wtDist` (N/mm).
- **Tire circumference**: `π × (rimDiameter + 2 × tireWidth × tireProfile / 100 / 25.4) × 0.0254` (meters).
- **Final drive**: targets `MaxSpeedKmh` at 95% MaxRPM through top gear.
- **Drag-limited speed**: `CarCard.MaxSpeedKmh` from Cd×FrontalArea (body only). Wing drag added in `TuneGeneratorService`.
- **LaunchControlRpm**: populated only for Drag.
- **Tire pressure**: base varies by TireType (Slick=2.24 bar, Sport=2.07, Offroad=2.00, etc.), adjusted for mass, wtDist, power, profile, rim diameter.
- **Gear ratios**: `GearCount == 1` → single gear + FD targets MaxSpeedKmh. Multi-gear: first/top vary by discipline, intermediates geometric.
- **Caster**: only positive (app uses `CasterMin=1`).
- **Season grip factor**: `GetSeasonGripFactor()` — Summer=1.00, Spring=0.93, Autumn=0.88, Winter=0.78. Affects springs (×0.50 weight), ARB (×0.65), dampers (×0.32), diff accel (−`(1−grip)×18`), tire pressure (±0.034 bar cold/hot split).
- **Season selector** (`TrackInfoView.xaml:186-206`): 4 RadioButtons with `EnumToBoolConverter` binding `Track.Season`. Season appended to auto-profile name; `SaveProfile()` deletes stale file on season switch.

## API Keys

`Services/ApiKeys.cs` reads env vars `FH6_CEREBRAS_API_KEY` / `FH6_OPENROUTER_API_KEY` with hardcoded fallback keys. `.gitignore` excludes `**/Services/ApiKeys.cs`; use `ApiKeys.cs.example` as template. **Bug**: `ApiKeys.OpenRouter` error message says "Cerebras API key not found" (copy-paste error).

## Gotchas

- `.sln` only contains the WPF app — `dotnet test` at solution level won't see test/validator projects.
- `CLAUDE.md` exists but contains stale claims ("No test project exists" is false — tests exist). Cross-reference with this file.
- No CI/CD pipeline files in repo.
- `.claude/settings.local.json` permits `dotnet build *` — agent permissions only.
