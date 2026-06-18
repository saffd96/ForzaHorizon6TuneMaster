# План рефакторинга: Part-Select Tuning

## Цель
Заменить старые модули ввода (CarCardView с 28 дропдаунами + TuningConstraintsView с 16 слайдерами) на 4 новых модуля выбора деталей из БД. Расчёт ведётся по физическим характеристикам выбранных деталей (давление, жёсткость пружин, передаточные числа и т.д.), а не по ползункам constraints.

### Что остаётся
- Выбор авто (поиск + список)
- TrackInfoView (discipline + season)
- TuneResultView (результаты расчёта)
- PowerCurveView + GearChartView (графики)
- **Auto-generate при смене детали** (как сейчас)

### Что уходит
- `Views/CarCardView.xaml` + `.cs` — удалить
- `Views/TuningConstraintsView.xaml` + `.cs` — удалить
- `Models/TuningConstraints.cs` — удалить целиком
- `Services/UnitConverter.cs` — удалить (значения из БД уже в metric)
- Все unit-конвертеры из XAML — удалить

---

## Фаза 1 — Подготовка инфраструктуры

### 1.1. `Models/PartOption.cs` (НОВЫЙ)
```csharp
public record PartOption
{
    public int Id { get; init; }
    public string DisplayName { get; init; }  // готовое читаемое имя
    public bool IsStock { get; init; }        // Level == 0
}
```

VM-и используют `ObservableCollection<PartOption>` — прибилинг в ComboBox.

### 1.2. `Models/SelectedParts.cs` (НОВЫЙ)
Все PartId вынесены в отдельную модель вместо CarCard:
- Swaps: EngineSwapPartId, ForcedInductionPartId, RearWingPartId, WeightReductionPartId, ChassisStiffnessPartId
- Engine: CamshaftPartId, DisplacementPartId, ValvesPartId, PistonsPartId, FuelSystemPartId, IgnitionPartId, ExhaustPartId, IntakePartId, FlywheelPartId, ManifoldPartId, OilCoolingPartId, RestrictorPartId, IntercoolerPartId
- Suspension: SpringDamperPartId, TireCompoundPartId, TireWidthFrontPartId, TireWidthRearPartId, BrakePartId, AntiSwayFrontPartId, AntiSwayRearPartId, **TireProfilePartId**
- Transmission: TransmissionPartId, ClutchPartId, DrivelinePartId, DifferentialPartId
- **RimStyle: RimStyleFrontPartId, RimStyleRearPartId** — влияет на массу диска
- Resolved: EngineId, DrivetrainId, CarBodyOrdinal
- Событие `PartsChanged` — для связи VM и триггера auto-generate
- **TotalMass вычисляется здесь**: `CurbWeight + sum(MassDiff всех PartId через Fh6DatabaseService)`
- Выставляет `TotalMass` и `WeightDistributionFront` на CarCard через событие/коллбэк

### 1.3. `Services/PartDisplayNameResolver.cs` (НОВЫЙ)
- Singleton через `Fh6DatabaseService.Instance`
- **Словарь категорий** (хардкод, ~25 записей):
  ```csharp
  private static readonly Dictionary<Type, string> CategoryMap = new()
  {
      { typeof(DbUpgradeCamshaft), "Camshaft" },
      { typeof(DbUpgradeBrakes),   "Brakes" },
      { typeof(DbUpgradeExhaust),  "Exhaust" },
      // ...
  };
  ```
- Метод `Resolve(DbUpgradePart part, string makeName)`:
  1. Определяет категорию из `CategoryMap` по `part.GetType()`
  2. Смотрит `Fh6DatabaseService.GetCarPartName(category, part.Level)` → `OptionalName1`
  3. Если не null: `"{makeName} {OptionalName1}"` (напр. "Toyota Sport Exhaust")
  4. Иначе: `"{makeName} {category} Stage {part.Level}"` (напр. "Toyota Camshaft Stage 2")
  5. Для Stock (Level==0): `"Stock {category}"` через ru.json/en.json
- VM использует резолвер при формировании `List<PartOption>` для ComboBox

### 1.4. `Fh6DatabaseService` — добавить:
- `LoadCarPartNames()` — загрузить таблицу `CarPartNames`, индексировать `(PartsString, Level) → OptionalName1`
- `GetCarPartName(string partsString, int level)` → `string?`
- `GetRimStyleFront(ordinal)` / `GetRimStyleRear(ordinal)` — новые методы для `List_UpgradeRimStyleFront/Rear`
- RimStyle таблицы — загрузить в LoadAllTables

---

## Фаза 2 — Сокращение старых файлов

### 2.1. `CarCard.cs` — вырезать (~250 строк)
Удалить:
- Все 30 PartId свойств + события `EngineSwapPartIdChanged`, `OnPartChanged`, `OnEngineSwapChanged`
- `SumSelectedMassDiffs()`, `PartMassDiff()` — **переехало в SelectedParts**
- Старые enum-свойства: `TireType`, `SuspensionUpgrade`, `DifferentialUpgrade`, `BrakesUpgrade`
- `EngineType`, `AspirationType`, `FuelType`, `PowertrainType`, `AntiLag`
- `PowerPeakRPM`, `TorquePeakRPM`, `_peakRpmFactors`, `_torquePeakRpmFactors`, `EstimatePeakRPM()`
- `ShowAntiLag`, `ShowAspiration`, `IsElectricPowertrain`, `SuspensionAllowsAdvancedTuning`
- `HasFrontARB`, `HasRearARB`, `HasAnyARB`, `HasRearAero`, `HasFrontAero`

Оставить:
- `CarDbId`, `EngineDbId`, `CarBodyId`, `DriveTypeID`
- `Name`, `Make`, `Model`, `Year`
- `TotalMass`, `CurbWeightKg` (просто поле; TotalMass выставляется SelectedParts через событие)
- `WeightDistributionFront`
- `EnginePosition`, `DriveType`
- `GearCount`, `MaxAvailableGearCount`, `AllowGearCalculation`, `OnlyFinalDriveCalculation`
- `FrontTireWidth`, `FrontTireProfile` (`int`), `RearTireWidth`, `RearTireProfile`, `FrontRimDiameter`, `RearRimDiameter`
- `Wheelbase`, `FrontTrack`, `RearTrack`
- `Cd`, `FrontalAreaM2`, `CdABodyEstimate`, `MaxSpeedKmh`
- `PowerHP`, `TorqueNm`, `MaxRPM`, `CachedTorqueCurveNm`, `CachedPowerCurveHP`

### 2.2. `TuningConstraints.cs` — удалить целиком
### 2.3. `Views/CarCardView.xaml` + `.cs` — удалить
### 2.4. `Views/TuningConstraintsView.xaml` + `.cs` — удалить

---

## Фаза 3 — Новые ViewModel-и

Общее для всех VM:
- Каждая VM создаёт `ObservableCollection<PartOption>`, обновляя список при изменении ключевого параметра (carBodyId / engineId / drivetrainId)
- По умолчанию выбирается `IsStock == true` (Level == 0)
- При выборе детали → `SelectedParts.PartsChanged?.Invoke()` → триггер `GenerateTune()`
- Имя из `PartDisplayNameResolver.Resolve(part, makeName)`

### 3.1. `ViewModels/SwapsViewModel.cs`
- 5 ObservableCollection: EngineSwap, ForcedInduction, RearWing, WeightReduction, ChassisStiffness
- Загрузка при смене CarCard (ordinal = CarBodyId)
- При смене EngineSwap: меняет `SelectedParts.EngineId` → `PartsChanged` → EnginePartsVM подхватывает

### 3.2. `ViewModels/EnginePartsViewModel.cs`
- 13 ObservableCollection: camshaft, displacement, valves, pistons, fuel, ignition, exhaust, intake, flywheel, manifold, oil, restrictor, intercooler
- Подписан на `SelectedParts.PartsChanged`; при изменении `EngineId` — перезагружает все списки
- По умолчанию: выбирает Level==0

### 3.3. `ViewModels/SuspensionViewModel.cs`
- 9 ObservableCollection: SpringDamper, TireCompound, TireWidthFront, TireWidthRear, Brakes, AntiSwayFront, AntiSwayRear, **TireProfile**, **RimStyleFront**, **RimStyleRear**

### 3.4. `ViewModels/TransmissionViewModel.cs`
- 4 ObservableCollection: Transmission, Clutch, Driveline, Differential

---

## Фаза 4 — Новые XAML Views

### 4.1. `Views/SwapsView.xaml`
### 4.2. `Views/EnginePartsView.xaml`
### 4.3. `Views/SuspensionView.xaml`
### 4.4. `Views/TransmissionView.xaml`

Каждый — UserControl с DataContext, привязанным к соответствующей VM.
Каждый ComboBox привязан к `ObservableCollection<PartOption>` + `SelectedItem` → PartOption.Id → SelectedParts.*PartId.

---

## Фаза 5 — MainViewModel + Calculator-ы

### 5.1. MainViewModel — сократить (~700 строк удалить, ~250 добавить)
Удалить:
- `_constraints`, `Constraints`, `_constraints.PropertyChanged`
- Все `Available*` коллекции (30 полей), `PopulateUpgradeOptions`, `SelectDefaultParts`
- `MapDbToOldEnums`, `MapPartIdsToEnums`, `MapEngineType`, `MapDisplayStrings`
- `OnEngineSwapPartIdChanged(value)`, `ResetEnginePartsToStock`
- `NotifyConstraintDisplayProperties` (16 OnPropertyChanged)
- Все `*Display` constraint-свойства (TirePressureDisplay и т.д.)
- `PressureUnit`, `SpringUnit`, `HeightUnit`, `SpeedUnit`, `MassUnit`, `PowerUnit`

Добавить:
- `SelectedParts _parts` + свойство
- `SwapsViewModel SwapsVM`, `EnginePartsViewModel EnginePartsVM`, `SuspensionViewModel SuspensionVM`, `TransmissionViewModel TransmissionVM`
- Подписка `_parts.PartsChanged += OnModelChanged` (auto-generate)
- `_parts.SetCar(CarCard car)` — инициализация CarBodyOrdinal/EngineId/DrivetrainId, загрузка дефолтных деталей
- `PowerCalculator.Calculate(Car, SelectedParts, Fh6DatabaseService)` — теперь внутри `GenerateTune()`, а не в CarCard
- В `GenerateTune()`: `_generator.Generate(Car, Track, _parts)` вместо `Generate(Car, Track, _constraints)`

### 5.2. Calculator-ы — обновить сигнатуры
```
Было:  Calculate(CarCard, TrackInfo, TuningConstraints, TuneResult, Dictionary<string,string>)
Стало: Calculate(CarCard, TrackInfo, SelectedParts, Fh6DatabaseService, TuneResult, Dictionary<string,string>)
```

Изменения по калькуляторам:
- **TireCalculator**: `GetTireCompound(parts.TireCompoundPartId).FrontTirePressure` + корректировка по сезону
- **SuspensionCalculator**: `GetSpringDamper(parts.SpringDamperPartId)` → `GetSpringDamperPhysics(id).DefSpringRate`, `DefDampenBumpRate`, `DefDampenReboundRate`, `DefRideHeight`
- **AlignmentCalculator**: не зависит от constraints — остаётся алгоритмической (camber/toe/caster)
- **BrakeCalculator**: `GetBrakes(parts.BrakePartId).BrakeBiasSlider`, `FrontBrakeTorqueClamp`
- **DifferentialCalculator**: `GetDifferential(parts.DifferentialPartId).FrontLimitedSlipTorqueAccel/Decel`, `Rear...`, `Center...`
- **GearingCalculator**: `GetTransmission(parts.TransmissionPartId).GearRatios`, `FinalDriveRatio`, `NumGears`
- **AeroCalculator**: `GetRearWing(parts.RearWingPartId)` → `GetAeroPhysics(id).DefaultTuneSlider`, `Downforce0`, `Drag0`
- **PowerCalculator**: вызывается из `GenerateTune()`, читает DB напрямую через `Fh6DatabaseService` и `SelectedParts`

---

## Фаза 6 — MainWindow.xaml

Новая компоновка:
```
┌─────────────────────────────────┬──────────────────┐
│ ScrollViewer (левая колонка)    │ TuneResultView    │
│  ├── CarSearchView              │ PowerCurveView    │
│  ├── TrackInfoView              │ GearChartView     │
│  ├── SwapsView                  │                   │
│  ├── EnginePartsView            │                   │
│  ├── SuspensionView             │                   │
│  └── TransmissionView           │                   │
│ [Сгенерировать кнопка]          │                   │
└─────────────────────────────────┴──────────────────┘
```

Все старые строки CarCardView + TuningConstraintsView удаляются из XAML.

---

## Фаза 7 — Профили

`StorageService.SavedProfile`:
```csharp
public string ProfileVersion { get; set; } = "v2.0";
public string Name { get; set; }
public CarCard Car { get; set; }
public TrackInfo Track { get; set; }
public SelectedParts Parts { get; set; }  // ← NEW (вместо Constraints)
public TuneResult? LastResult { get; set; }
```

При десериализации профилей версии < "v2.0": `Parts` = default (все IsStock/Level==0), `Constraints` игнорируется.

---

## Фаза 8 — Удаление конвертеров

- Удалить `Services/UnitConverter.cs`
- Убрать `UnitValueConverter` MultiBinding из XAML
- TuneResult отображает значения напрямую: давление в bar, пружины в N/mm, высота в mm, скорость в km/h, масса в kg, мощность в HP

---

## Фаза 9 — Тесты

- Адаптировать `CarFactory.cs` (убрать TuningConstraints, добавить SelectedParts)
- 14 pre-existing ошибок не трогаем
- Проверка:

```powershell
dotnet build
dotnet test TuneMaster.Tests\TuneMaster.Tests.csproj
dotnet build -c Release
```

---

## Итоговая статистика

| Файл | Добавлено | Удалено |
|------|-----------|---------|
| `PartOption.cs` | ~10 | — |
| `SelectedParts.cs` | ~120 | — |
| `PartDisplayNameResolver.cs` | ~150 | — |
| 4 VM (Swaps/Engine/Suspension/Trans) | ~600 | — |
| 4 XAML Views | ~440 | — |
| MainViewModel.cs | ~250 | ~700 |
| CarCard.cs | ~10 | ~250 |
| TuningConstraints.cs | — | ~200 |
| CarCardView (.xaml+.cs) | — | ~200 |
| TuningConstraintsView (.xaml+.cs) | — | ~200 |
| StorageService.cs | ~30 | ~20 |
| UnitConverter.cs | — | ~100 |
| Calculator-ы | ~100 | ~50 |
| Fh6DatabaseService (добавки) | ~60 | — |
| **Итого** | **~1770** | **~1720** |
| **Чистое изменение** | | **~+50 строк (в основном новые VM/Views)** |
