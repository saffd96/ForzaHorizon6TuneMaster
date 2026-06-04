# Forza Horizon 6 Tune Master

.NET 8 WPF desktop app that generates car tunes from car data + track discipline + user constraints.

## Build

```powershell
dotnet build            # Debug
dotnet build -c Release # Release
```

Windows + .NET 8 SDK required (WPF is Windows-only). No test project, CI, linter, or formatter config.

## Architecture

Single project, no DI — `MainWindow` does `DataContext = new MainViewModel()` which constructs `TuneGeneratorService` and `StorageService` inline.

**Key file**: `Services/TuneGeneratorService.cs` — single instance method `Generate(car, track, constraints)` calls private static methods per subsystem. All output is `Clamp(v, min, max)`. Heavy switch-on-`Discipline` (and `DriveType`) throughout.

**Models**: `CarCard`, `TrackInfo`, `TuningConstraints` derive from `NotifyBase`. `TuneResult` is a plain POCO.

**Constraint display**: XAML binds to `*Display` properties on `MainViewModel` (e.g., `TirePressureFrontMinDisplay`), which convert units. Never bind directly to raw `TuningConstraints` properties in views.

## Decimal separator bug

`App.xaml.cs` sets WPF culture to `en-US` for binding purposes. But `ExportText()` writes values via `$"{value}"` which uses the **current thread culture** (Russian → comma `,` as separator). `ImportText()` parses with `InvariantCulture` (expects `.`). **Export produces `3,8` but import expects `3.8`**. Fix format strings in export if this becomes an issue.

## Unit system

3 global toggles on `MainViewModel`:
- `MeasurementSystem` (Metric/Imperial) — affects pressure, height, speed, mass, torque, wheelbase, track
- `PowerUnit` (HP/PS/KW)
- `SpringUnit` (kgf·mm/N·mm/lb·in)

CarCardView input fields bind to `PowerDisplay`, `MassDisplay`, `TorqueDisplay`, `SpeedDisplay`, `WheelbaseDisplay`, `FrontTrackDisplay`, `RearTrackDisplay` — all on `MainViewModel`, unit-converted round-trip.

TuneResultView uses `UnitValueConverter` (multi-binding) with `ConverterParameter`: `"pressure"`, `"spring"`, `"height"`, `"speed"`, `"mass"`, `"power"`.

## Weight distribution quirk

`EffectiveWtDist()`: if `WeightDistributionFront` is within 2% of 50/50, engine position overrides it (Front→55, Mid→48, Rear→40). Many formulas call this instead of using raw `WeightDistributionFront`.

## Profile storage

JSON files at `%APPDATA%\ForzaTuneMaster\profiles\*.json`. Serialized with `System.Text.Json` + `JsonStringEnumConverter`.

## Export/Import text

Export: `[Car]`, `[Track]`, `[Constraints]`, `[Result]` sections + explanations. Import reads only `[Car]` + `[Constraints]` then calls `GenerateTune()` once. Uses `SafeDouble(val)` (InvariantCulture) — see decimal separator bug above.

## AWD diff fields

`DiffFrontAccel/Decel` and `CenterDiffBias` are `double?`. Only non-null for AWD. `HasAWDFrontDiff` controls XAML visibility.

## UI language

All labels and status messages are in Russian. If adding UI text, use Russian.

## AI Car Specs via Cerebras

`Services/AiCarSpecService.cs` — fetches wheelbase, track, Cd, frontal area from car name/year via Cerebras API.

- API key hardcoded as `const string CerebrasApiKey`
- Model fallback: `gpt-oss-120b` → `zai-glm-4.7`
- `temperature: 0`, `top_p: 1`
- Request: `POST https://api.cerebras.ai/v1/chat/completions` with `Authorization: Bearer {key}`
- Response parsed with `JsonNamingPolicy.SnakeCaseLower` — maps `wheelbase_mm` → `WheelbaseMm`
- Returns `AiCarSpecResponse` with `WheelbaseMm`, `FrontTrackMm`, `RearTrackMm`, `Cd`, `FrontalAreaM2`, `EstimatedFields` (HashSet of snake_case field names)

`MainViewModel.FetchAiCarSpecsCommand` triggers the fetch. Busy overlay (`IsBusy` / `BusyMessage`) blocks the UI during fetch. ⚠ indicator shown for fields in `EstimatedFields`. Card layout: "ХАРАКТЕРИСТИКИ" section contains geometry + aero data + aero checkboxes; "ДЕТАЛИ" section contains upgrades only.

## Dependencies

NuGet packages: `System.Text.Json 8.0.5`. `ImplicitUsings` enabled, `Nullable` enabled.
