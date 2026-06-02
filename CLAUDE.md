# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
# Build
dotnet build "Forza Horizon 6 Tune Master/Forza Horizon 6 Tune Master.csproj"

# Run
dotnet run --project "Forza Horizon 6 Tune Master/Forza Horizon 6 Tune Master.csproj"

# Release build
dotnet publish "Forza Horizon 6 Tune Master/Forza Horizon 6 Tune Master.csproj" -c Release -r win-x64 --self-contained
```

No test project exists. No linter is configured. The only NuGet dependency is `System.Text.Json 8.0.5`.

## Architecture

WPF .NET 8, MVVM, single ViewModel (`MainViewModel`) bound to all UserControl views via `DataContext` propagated from `MainWindow`.

**Data flow:**
1. User fills `CarCardView` + selects discipline in `TuneResultView`
2. Clicks "Сгенерировать" → `MainViewModel.GenerateCommand` → `TuneGeneratorService.Generate(Car, Track, Constraints)` → returns `TuneResult`
3. `TuneResultView` binds directly to `MainViewModel.TuneResult`

**Canonical storage units.** All model properties store metric values:
- Power → HP (`Car.PowerHP`)
- Speed → km/h (`Car.MaxSpeedKmh`)
- Mass → kg (`Car.TotalMass`)
- Spring rates → N/mm (`TuneResult.SpringFront/Rear`) — formula: k = 4π²/2000 × f² × m_corner
- Tire pressure → bar
- Heights → mm

Unit conversion happens only in display: `MainViewModel` exposes `PowerDisplay`, `SpeedDisplay`, `MassDisplay` (computed properties with TwoWay conversion), and `PowerFieldLabel`, `SpeedFieldLabel`, `MassFieldLabel` for dynamic labels. `TuneResultView` uses `UnitValueConverter` (MultiValueConverter) with `MeasurementSystem`, `SpringUnit`, or `PowerUnit` as the second binding value.

## Key Patterns

**INPC without CommunityToolkit.** All models inherit `NotifyBase` (`Models/NotifyBase.cs`) which provides `Set<T>(ref field, value)` and `OnPropertyChanged()`. `MainViewModel` implements `INotifyPropertyChanged` directly.

**Enum ↔ RadioButton binding.** `EnumToBoolConverter` in `Converters/Converters.cs` supports TwoWay. Usage:
```xml
<RadioButton IsChecked="{Binding Discipline, Converter={StaticResource E2B}, ConverterParameter={x:Static m:Discipline.Road}}"/>
```
ConvertBack returns `Binding.DoNothing` for the unchecked case so only the checked button sets the property.

**Unit-aware display (MultiBinding).** `UnitValueConverter` takes `values[0]` = double and `values[1]` = one of `UnitSystem`, `SpringUnit`, or `PowerUnit` enum (determined by `ConverterParameter`). Example:
```xml
<MultiBinding Converter="{StaticResource UV}" ConverterParameter="spring">
    <Binding Path="TuneResult.SpringFront"/>
    <Binding Path="SpringUnit"/>
</MultiBinding>
```

**NumericBehavior** (`Converters/Converters.cs`) is an attached property that restricts TextBox input to valid numbers and normalises comma→dot. Applied as `cv:NumericBehavior.IsNumeric="True"`.

**RelayCommand.** Simple `ICommand` wrapper with `Raise()` to manually fire `CanExecuteChanged`. Load/Delete commands gate on `SelectedProfile != null` via `Func<bool>` in constructor.

**Profile persistence.** `StorageService` saves `SavedProfile` (Car + Track + Constraints + LastResult) as indented JSON to `%APPDATA%\ForzaTuneMaster\profiles\<name>.json`. Spaces in names are stored as underscores on disk and reversed on load. `[JsonIgnore]` must be applied to all computed properties on model classes to avoid serialisation errors.

## TuneGeneratorService

Pure static-method calculation service. Each `Calculate*` method takes `(CarCard car, TrackInfo track, TuningConstraints c, TuneResult r, Dictionary<string,string> ex)` and writes directly to `r` and `ex`. Adding a new discipline requires updating every `switch` statement in this file. Spring rate formula: `k = mass * wdF * (2πf)² / 2000` (N/mm per spring; constant 0.019739). Final drive formula targets `MaxSpeedKmh` at 95% MaxRPM through the top gear ratio.

## Enums (Models/Enums.cs)

8 disciplines: `Road, Touge, Rally, CrossCountry, Drift, Drag, Eliminator, Street`  
Unit enums: `UnitSystem {Metric, Imperial}`, `PowerUnit {HP, PS, KW}`, `SpringUnit {KgfMm, NMm, LbsIn}`

## Theme

`Resources/DarkTheme.xaml` defines all brushes and shared styles (`SectionHeader`, `FormLabel`, `ValueDisplay`, `ParamLabel`, `SectionCard`, `ParamCard`, `PrimaryButton`, `SecondaryButton`, `DangerButton`). Never set `Height` on buttons directly — the styles use auto-sizing via `Padding` only.
