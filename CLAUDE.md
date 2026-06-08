# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
# Build
dotnet build "Forza Horizon 6 Tune Master/Forza Horizon 6 Tune Master.csproj"

# Run
dotnet run --project "Forza Horizon 6 Tune Master/Forza Horizon 6 Tune Master.csproj"

# Run tests
dotnet test TuneMaster.Tests/TuneMaster.Tests.csproj

# Release build
dotnet publish "Forza Horizon 6 Tune Master/Forza Horizon 6 Tune Master.csproj" -c Release -r win-x64 --self-contained
```

No linter is configured. NuGet dependencies: `System.Text.Json 8.0.5`, `HtmlAgilityPack` (wiki parsing).

## Architecture

WPF .NET 8, MVVM, single ViewModel (`MainViewModel`, ~1500 lines) bound to all UserControl views via `DataContext` propagated from `MainWindow`. `TuneValidator/` is a separate utility project for offline validation.

**Data flow:**
1. User fills `CarCardView` (car specs) + selects discipline in `TuneResultView`
2. `CarDatabaseService` populates the car picker by scraping Forza Fandom wiki (~650 cars, cached daily to `%APPDATA%\ForzaTuneMaster\fh6_cars_fandom.json`)
3. Selecting a car triggers async spec fetch: `WikiCarSpecService` → parses HTML for powertrain/weight/gear data → `AiCarSpecService` fills any remaining geometry fields via LLM (OpenRouter/Cerebras with daily cache)
4. Clicking "Сгенерировать" → `MainViewModel.GenerateCommand` → `TuneGeneratorService.Generate(Car, Track, Constraints)` → populates `TuneResult` and `TuneResult.Explanations`
5. `TuneResultView` binds directly to `MainViewModel.TuneResult`

**Three data persistence layers:**
- `%APPDATA%\ForzaTuneMaster\profiles\` — user-saved tune profiles (JSON)
- `%APPDATA%\ForzaTuneMaster\specs\` — wiki spec cache per car
- `%APPDATA%\ForzaTuneMaster\specs_cache\` — AI geometry cache; `specs_overrides.json` holds manual corrections

## Canonical Storage Units

All model properties store metric values internally:
- Power → HP (`Car.PowerHP`)
- Speed → km/h (`Car.MaxSpeedKmh`, hard-capped at 700)
- Mass → kg (`Car.TotalMass`)
- Spring rates → N/mm (`TuneResult.SpringFront/Rear`)
- Tire pressure → bar
- Heights → mm

Unit conversion happens only at display time. `MainViewModel` exposes `PowerDisplay`, `SpeedDisplay`, `MassDisplay` (TwoWay with conversion) and their label companions. Constraint display properties follow the same pattern: e.g. `TirePressureFrontMinDisplay`. `TuneResultView` uses `UnitValueConverter` (MultiValueConverter) where `ConverterParameter` is one of `"pressure"`, `"spring"`, `"height"`, `"speed"`, `"mass"`, `"power"`.

## Key Patterns

**INPC without CommunityToolkit.** All models inherit `NotifyBase` (`Models/NotifyBase.cs`) — provides `Set<T>(ref field, value)` and `OnPropertyChanged()`. `MainViewModel` implements `INotifyPropertyChanged` directly.

**Enum ↔ RadioButton binding.** `EnumToBoolConverter` in `Converters/Converters.cs` supports TwoWay. ConvertBack returns `Binding.DoNothing` for the unchecked case so only the checked button fires.
```xml
<RadioButton IsChecked="{Binding Discipline, Converter={StaticResource E2B}, ConverterParameter={x:Static m:Discipline.Road}}"/>
```

**Unit-aware display (MultiBinding).** `UnitValueConverter` takes `values[0]` = double and `values[1]` = unit enum, dispatched by `ConverterParameter`:
```xml
<MultiBinding Converter="{StaticResource UV}" ConverterParameter="spring">
    <Binding Path="TuneResult.SpringFront"/>
    <Binding Path="SpringUnit"/>
</MultiBinding>
```

**Localization markup extension.** Use `{l:Loc key}` in XAML for all user-facing strings. Supports an optional `Format` parameter. `LocalizationService` is a singleton backed by language JSON files in `Localization/`; `SetLanguage()` swaps culture and persists user preference.

**NumericBehavior** (`Converters/Converters.cs`) is an attached property restricting TextBox input to valid numbers and normalising comma→dot. Applied as `cv:NumericBehavior.IsNumeric="True"`.

**RelayCommand.** Simple `ICommand` wrapper with `Raise()` to manually fire `CanExecuteChanged`. Load/Delete commands gate on `SelectedProfile != null` via `Func<bool>` in constructor.

**Profile persistence.** `StorageService` serialises `SavedProfile` (Car + Track + Constraints + LastResult + `AiEstimatedFields` list) as indented JSON. Spaces in profile names are stored as underscores. Apply `[JsonIgnore]` to all computed properties on model classes to avoid serialisation errors. `ForzaPaths` is a static class centralising all `%APPDATA%` paths; it exposes `SetTestRoot()` returning `IDisposable` for test isolation.

**Constraint min/max invariants.** `TuningConstraints` setters call `SetMinMax()` / `SetMaxMin()` helpers that auto-correct the paired bound using `[CallerMemberName]` to derive the counterpart property name. Never bypass these setters.

**`_isLoadingProfile` flag.** `MainViewModel` sets this during profile deserialization to suppress async wiki/AI spec fetches that would overwrite the loaded values.

## TuneGeneratorService

Pure static methods — no state. Each `Calculate*` method signature is `(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string,string> ex)` and writes side effects only to `r` and `ex`. Key points:

- **Spring rate formula**: `k = mass × wdF × (2πf)² / 2000` (constant 0.019739, result in N/mm)
- **Final drive**: Newton-Raphson targeting `MaxSpeedKmh` at 95% MaxRPM through top gear ratio
- **Aero convergence**: `CalculateAero()` is called 3× iteratively to converge on speed-dependent downforce
- **Discipline switch in every method** — adding a new discipline requires updating every `switch`
- **Aspiration affects multipliers** throughout: power delivery, launch RPM floor, spring/damper/ARB factors, differential
- **Dynamic camber caps** based on mass/PTW ratio; soft-squash (proportional) clamping rather than hard clip
- **Tire model**: 9 tire types × 3 properties (grip, thermal sensitivity, wear resistance)
- `TuneResult.Explanations` (`Dictionary<string,string>`) is populated by each method with human-readable justifications shown in the UI

## CarCard — Computed Properties

- `PowerPeakRPM` / `TorquePeakRPM`: derived from `MaxRPM` × engine-type percentage curves; electric motors use 45% / 0%
- `FrontWheelDiameterInch`, `RearWheelDiameterInch`, `DrivenWheelDiameterInch`: drive-type dependent
- `CdA`: uses explicit `Cd × FrontalAreaM2` when both set, otherwise estimates from mass + tire profile
- `HasExplicitWeightDistribution`: preserves engine-position defaults during deserialization

## Enums (`Models/Enums.cs`)

Disciplines: `Road, Touge, Rally, CrossCountry, Drift, Drag, Street`  
Unit enums: `UnitSystem {Metric, Imperial}`, `PowerUnit {HP, PS, KW}`, `SpringUnit {KgfMm, NMm, LbsIn}`  
Other: `FuelType {Gasoline, Diesel}` (affects torque curve shape in TuneGeneratorService)

## Converters (`Converters/Converters.cs`)

Beyond `EnumToBoolConverter`, `UnitValueConverter`, `NumericBehavior`:  
`NullToVisibilityConverter`, `BoolToVisibilityConverter`, `InverseBoolToVisibilityConverter`, `InverseBoolConverter`, `EqualityConverter`, `EqualityVisibilityConverter`, `AddOneConverter`, `GenericEnumLabelConverter`, `PowertrainTypeLabelConverter`, `AspirationTypeLabelConverter`, `DateDisplayConverter`

## Theme

`Resources/DarkTheme.xaml` defines all brushes and shared styles: `SectionHeader`, `FormLabel`, `ValueDisplay`, `ParamLabel`, `SectionCard`, `ParamCard`, `PrimaryButton`, `SecondaryButton`, `DangerButton`. Never set `Height` on buttons — styles use auto-sizing via `Padding` only.
