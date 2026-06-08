# Forza Horizon 6 Tune Master

.NET 8 WPF app that generates car tunes from car data + track discipline + user constraints.

## Build & Test

```powershell
dotnet build                                      # Debug build (all 3 projects)
dotnet build -c Release                           # Release build
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj  # Run xUnit tests (~400)
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj --filter "FullyQualifiedName~TuneGeneratorService"  # Single test class
```

Windows + .NET 8 SDK required (WPF is Windows-only).

## Projects

| Project | Type | In `.sln`? |
|---|---|---|
| `Forza Horizon 6 Tune Master/` | WPF app (WinExe, `net8.0-windows`) | Yes |
| `TuneMaster.Tests/` | xUnit (`net8.0-windows`, `UseWPF=true`) | **No** — run its `.csproj` directly |
| `TuneValidator/` | Console harness (`net8.0`) | **No** — manually-run formula verification |

## Key Entrypoints & Sizes

- `Services/TuneGeneratorService.cs` (~1645 lines) — single class, all private static methods. Output clamped everywhere. Heavy `switch`-on-`Discipline` (and `DriveType`).
- `ViewModels/MainViewModel.cs` (~1470 lines) — single ViewModel, constructs services inline (no DI).
- `Models/CarCard.cs` (~410 lines) — `NotifyBase`, includes computed `MaxSpeedKmh` from Cd × FrontalArea.
- `Models/TuneResult.cs` (~86 lines) — plain POCO (no `NotifyBase`). `[JsonIgnore]` on `Car` and `Track`.
- `Models/TuningConstraints.cs` (~215 lines) — `NotifyBase`, `SetMinMax` ensures min ≤ max automatically. `[JsonPropertyOrder]` on all properties.

## Test Pattern

Tests use `TuneMaster.Tests/Helpers/CarFactory.cs` which provides preset cars (`DefaultCar()`, `FWDStockCar()`, `ElectricCar()`, etc.), tracks, and constraints (`RelaxedConstraints()` with wide bounds). Standard test:

```csharp
var result = new TuneGeneratorService().Generate(
    CarFactory.DefaultCar(), CarFactory.DefaultTrack(), CarFactory.RelaxedConstraints());
```

## Architecture

- **No DI**: `MainWindow.DataContext = new MainViewModel()` — services constructed inline.
- **INPC**: Custom `NotifyBase` (`Models/NotifyBase.cs`) with `Set<T>(ref field, value)`. No CommunityToolkit.
- **Enums** (`Models/Enums.cs`): 8 disciplines, `DriveType`, `TireType` (9 values), `SuspensionUpgrade` (7 values), `DifferentialUpgrade` (7 values), `SpringUnit {KgfMm, NMm, LbsIn}`.
- **Root namespace**: `Forza_Horizon_6_Tune_Master` (underscores, not dots).

**Key services:**
- `StorageService` — profiles as JSON at `%APPDATA%\ForzaTuneMaster\profiles\*.json`
- `LocalizationService` — singleton, reads embedded `Localization/{ru,en}.json`. All UI labels via `T(key)` or `LocExtension`.
- `AiCarSpecService` — fetches wheelbase/track/Cd/frontal area via Cerebras/OpenRouter APIs. Fallback: `gpt-oss-120b` → `zai-glm-4.7`.
- `WikiCarSpecService` — parses Forza Fandom wiki `{{CarInfobox}}`, cached at `%APPDATA%\ForzaTuneMaster\specs\*.json`.

## UI Conventions

- **Language**: Russian. Add UI text in `Localization/ru.json` + matching key in `en.json`.
- **Unit conversion**: XAML binds to `*Display` properties on MainViewModel (e.g., `TirePressureFrontMinDisplay`), never to raw model properties. `UnitValueConverter` (multi-binding) with `ConverterParameter`: `"pressure"`, `"spring"`, `"height"`, `"speed"`, `"mass"`, `"power"`.
- **Enum ↔ RadioButton**: `EnumToBoolConverter` TwoWay; `ConvertBack` returns `Binding.DoNothing` for unchecked.
- **Numeric input**: `NumericBehavior.IsNumeric` attached property (normalises comma→dot).
- **AWD differential**: `DiffFrontAccel/Decel` and `CenterDiffBias` are `double?` (non-null only for AWD). `HasAWDFrontDiff` controls XAML visibility.
- **Compact layout**: `MainWindow.xaml.cs` adjusts font resources when width < 1200px.
- **Theme**: `Resources/DarkTheme.xaml` defines all brushes/shared styles. Never set `Height` on buttons — styles use auto-sizing via `Padding`.
- **`[JsonIgnore]`** on all computed properties in model classes to avoid serialisation errors.

## Formula Quirks

- **EffectiveWtDist**: if `WeightDistributionFront` == 50.0 (default, never explicitly set), engine position overrides: Front→55, Mid→48, Rear→40.
- **SuspensionUpgrade multiplier**: Race=1.10×, Sport=1.00× (not in existing list!), Street=0.88×, Rally varies (offroad disc. 0.85, else 0.55), Drift=0.85×.
- **Spring constant**: `k = 0.02012 × Hz² × mass × wtDist` (N/mm; includes ÷1000÷2 for FH6 units).
- **Tire circumference**: `π × (rimDiameter + 2 × tireWidth × tireProfile / 100 / 25.4) × 0.0254` (meters).
- **Final drive**: targets `MaxSpeedKmh` at 95% MaxRPM through top gear ratio.
- **Drag-limited speed**: `CarCard.MaxSpeedKmh` from Cd × FrontalArea (body only). Wing drag added in TuneGeneratorService.
- **`TuneResult.ActualMaxSpeedKmh`**: FD × top gear × tire circumference — actual achieved top speed.
- **LaunchControlRpm**: populated only for Drag discipline.
- **Tire pressure**: base varies by TireType (Slick=2.24 bar, Sport=2.07, Offroad=2.00, etc.), adjusted for mass, wtDist, power, profile, rim diameter.
- **Gear ratios**: `GearCount == 1` → single gear + FD targets `MaxSpeedKmh`. Multi-gear: first/top vary by discipline, intermediates are geometric progression.
- **Caster**: only positive (app uses `CasterMin=1`).

## API Keys

`Services/ApiKeys.cs` reads env vars `FH6_CEREBRAS_API_KEY` / `FH6_OPENROUTER_API_KEY` with **hardcoded fallback keys** in code. `.gitignore` honours `**/Services/ApiKeys.cs`. `Services/ApiKeys.cs.example` shows expected structure. **Bug**: `ApiKeys.OpenRouter` error message says "Cerebras API key not found" (copy-paste error).

## Gotchas

- `.sln` only contains the WPF app — `dotnet test` at solution level won't see test/validator projects; use their `.csproj` paths.
- `Services/Abstractions/` and `Data/` directories exist but are empty.
- No CI/CD pipeline files in the repo.
