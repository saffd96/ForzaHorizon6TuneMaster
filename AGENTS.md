# Forza Horizon 6 Tune Master

.NET 8 WPF app that generates car tunes from car data + track discipline + user constraints.

## Build & Test

```powershell
dotnet build                              # Debug build (all 3 projects)
dotnet build -c Release                   # Release build
dotnet test                               # Run all xUnit tests (~500+)
```

Windows + .NET 8 SDK required (WPF is Windows-only).

## Projects

| Project | Type |
|---|---|
| `Forza Horizon 6 Tune Master/` | WPF app (WinExe, `net8.0-windows`) |
| `TuneMaster.Tests/` | xUnit test project (`net8.0-windows`) |
| `TuneValidator/` | Console app (`net8.0`) — manually-run formula verification |

Tests use `CarFactory` helpers (`TuneMaster.Tests/Helpers/CarFactory.cs`) for preset cars, tracks, and constraints. Test pattern: `new TuneGeneratorService().Generate(car, track, constraints)`.

## Architecture

No DI — `MainWindow` sets `DataContext = new MainViewModel()` which constructs services inline. `TuneGeneratorService` is a single-instance wrapper around ~1380 lines of private static methods.

**Key services:**
- `TuneGeneratorService.Generate(car, track, constraints)` — all output is `Clamp(v, min, max)`. Heavy `switch`-on-`Discipline` (and `DriveType`) throughout.
- `StorageService` — profiles as JSON at `%APPDATA%\ForzaTuneMaster\profiles\*.json`
- `LocalizationService` — singleton, reads embedded `Localization/{ru,en}.json`. All UI labels use keyed strings (`T(key)` or `LocExtension` XAML markup).
- `AiCarSpecService` — fetches wheelbase/track/Cd/frontal area via Cerebras/OpenRouter APIs. Models fallback: `gpt-oss-120b` → `zai-glm-4.7`. Response class: `AiCarSpecsResponse`.
- `WikiCarSpecService` — parses Forza Fandom wiki `{{CarInfobox}}`, cached at `%APPDATA%\ForzaTuneMaster\specs\*.json`.

**Models:** `CarCard`, `TrackInfo`, `TuningConstraints` derive from `NotifyBase` (custom INPC, no CommunityToolkit). `TuneResult` is a plain POCO.

## Key Patterns

- **Enum ↔ RadioButton**: `EnumToBoolConverter` with `TwoWay`; `ConvertBack` returns `Binding.DoNothing` for unchecked.
- **Unit display**: `UnitValueConverter` (multi-binding) with `ConverterParameter`: `"pressure"`, `"spring"`, `"height"`, `"speed"`, `"mass"`, `"power"`.
- **Numeric input**: `NumericBehavior.IsNumeric` attached property on TextBox (normalises comma→dot).
- **RelayCommand**: Simple `ICommand` wrapper with `Raise()` for `CanExecuteChanged`. Constructor overloads for `Action` and `Action<object?>`.
- **[JsonIgnore]** on all computed properties in model classes to avoid serialisation errors.
- **UI offset**: XAML binds to `*Display` properties on `MainViewModel` (e.g., `TirePressureFrontMinDisplay`), which convert units. Never bind directly to raw `TuningConstraints` properties in views.
- **AWD differential**: `DiffFrontAccel/Decel` and `CenterDiffBias` are `double?` (non-null only for AWD). `HasAWDFrontDiff` controls XAML visibility.
- **UI language**: Russian. If adding UI text, use Russian in `Localization/ru.json` and add a matching key in `en.json`.
- **Compact layout**: `MainWindow.xaml.cs` adjusts font resources when width < 1200px.

## Formula Quirks

- **Weight distribution override** (`EffectiveWtDist`): if `WeightDistributionFront` equals the default 50.0 (never explicitly set by user), engine position overrides it: Front→55, Mid→48, Rear→40. Many formulas call this instead of raw `WeightDistributionFront`.
- **SuspensionUpgrade** (`Stock, Street, Sport, Race, Rally, Drift, Offroad`) multiplies spring/damper/ride-height values. Race=1.10x, Street=0.88x, Rally varies by offroad discipline (0.85/0.55), Drift=0.85x.
- **Spring constant**: `k = 0.019739 × Hz² × mass × wtDist` (includes ÷1000 ÷2 for FH6 units).
- **Final drive**: targets `MaxSpeedKmh` at 95% MaxRPM through top gear ratio.
- **Drag-limited speed**: `CarCard.MaxSpeedKmh` is computed from Cd × FrontalArea (body drag only, no wing). Wing drag added in `TuneGeneratorService` during generation.
- **`TuneResult.ActualMaxSpeedKmh`** is computed from FD × top gear × tire circumference — reflects achieved top speed with the tune.
- **LaunchControlRpm** is populated only for Drag discipline.
- **Tire pressure**: base varies by `TireType` (Slick=2.24 bar, Sport=2.07, Offroad=2.00, etc.), adjusted for mass, weight distribution, power, profile, and rim diameter.
- **Gear ratios**: if `GearCount == 1` (e.g., electric), single gear + FD targets `MaxSpeedKmh` directly. For multi-gear, first/top ratios vary by discipline, intermediate ratios are geometric progression.
- **Caster**: only positive allowed (app uses `CasterMin=1`).

## Dependencies

NuGet: `System.Text.Json 8.0.5`. `ImplicitUsings` enabled, `Nullable` enabled.

Test deps: `xunit 2.5.3`, `xunit.runner.visualstudio 2.5.3`, `Microsoft.NET.Test.Sdk 17.8.0`, `coverlet.collector 6.0.0`.

## API Keys

`Services/ApiKeys.cs` reads env vars `FH6_CEREBRAS_API_KEY` / `FH6_OPENROUTER_API_KEY` with hardcoded fallbacks. `.gitignore` also honours `**/Services/ApiKeys.cs` pattern. `Services/ApiKeys.cs.example` shows the expected structure for creating a local copy.
