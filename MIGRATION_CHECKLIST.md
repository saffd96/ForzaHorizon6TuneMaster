# Migration Plan: Forza Horizon 6 Tune Master → SQLite-Backed

Date: 2026-06-17
Goal: Replace all empirical data (manual inputs, enum upgrades, wiki/AI parsing) with data from FH6 SQLite database (`fh6_db.sqlite` embedded in .exe).

**Цель**: убрать wiki-скрейпинг, AI-вызовы и все числовые инпуты. Только дропдауны из `fh6_db.sqlite`.

---

## Архитектура

**Текущее:**
```
Wiki Fandom ──→ CarDatabaseService ──→ List<CarData> ──→ CarSpecController
AI (OpenRouter) ──→ AiCarSpecService ───→ CarCard (wheelbase, Cd, etc.)
Manual input ──────────────────────────→ CarCard (power, torque, mass, tires)
Enum upgrades (Sport/Race/etc.) ───────→ Calculators (hardcoded formulas)
```

**Целевое:**
```
Embedded .sqlite → sqlite3_deserialize → named in-memory DB (ForzaTuneMasterDB)
                                               │
                          Fh6DatabaseService ───┘  (loads all into Dictionary at startup)
                               │
                ┌──────────────┼──────────────┐
                ▼              ▼              ▼
        PopulateCarFromDb  Upgrade dropdowns  PowerCalculator
        (CarCard fields)   (ObservableColl)   (torque curve)
                │              │              │
                └──────────────┼──────────────┘
                               ▼
                       Calculators (DB bounds)
                               │
                               ▼
                         TuneResult
```

**Key principle**: NEVER accept user-typed numbers for specs. Everything comes from DB dropdowns + computed from torque curves.

---

## Blockers (проверено ДО начала)

- [x] **List_PartsStrings** EXISTS (136 rows) — коды "BumperFa", не display names. Для частей движка используем `Data_Engine.EngineName`; остальные — `"{TableName} Lvl{Level}"`.
- [x] **Data_CarBody** колонки: `Wheelbase`, `ModelFrontTrackOuter`, `ModelRearTrackOuter`, `ModelFrontRideHeight`, `ModelRearRideHeight` — **все в метрах** (Wheelbase=2.329 для Car 247). `Id = Car.Id × 1000` (247→247000). **Нет колонок Cd или FrontalArea**.
- [x] **CurbWeight** × 100 = kg (11.56661 × 100 = 1156.66 кг). **WeightDistribution** = ratio 0–1 (0.51 = 51% front) — ×100 в коде.
- [x] **EngineOverrideCurveID** — **НЕ СУЩЕСТВУЕТ** в Data_Car. Кривая всегда из camshaft.
- [x] **Data_UpgradePartCategory**: Car parts=Ordinal(CarId), Weight parts=CarBodyId(CarId×1000).
- [x] **List_SpringDamperPhysics** EXISTS (DefSpringRate N/mm=29.7, DefRideHeight m=0.1185).
- [x] **List_BrakesPhysics** **НЕ СУЩЕСТВУЕТ** — данные на List_UpgradeBrakes (GameFrictionScaleBraking, BrakeBiasSlider, BrakeTorqueSlider, Front/RearBrakeSizeMM, Front/RearBrakeTorqueClamp).
- [x] **List_AeroPhysics** EXISTS (DownforceSliderMin/Max, DragSliderMin/Max).
- [x] **List_GearRatio** **НЕ СУЩЕСТВУЕТ** — GearRatio0–10 колонки на List_UpgradeDrivetrainTransmission. GearRatio0=-3.168 (drag reverse), GearRatio7–10=-1 (unused).
- [x] **Turbo/SC coexistence**: 19 двигателей имеют оба Single+Twin, 45 — CSC+DSC. Решение: **один dropdown** со всеми FI-опциями (Single, Twin, CSC, DSC, none).

---

## Phase 0 — Database Infrastructure

**Создать:**
- [ ] `Services/DbSchema.cs` — C# records для всех таблиц БД
- [ ] `Services/Fh6DatabaseService.cs` — синглтон, загрузка в Dictionary при старте

**Изменить:**
- [ ] `.csproj` — `<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.5" />`
- [ ] `.csproj` — `<EmbeddedResource Include="DUMPER\fh6_db.sqlite" />`
- [ ] `App.xaml.cs` — `_ = Fh6DatabaseService.Instance.InitializeAsync();`
- [ ] `Services/ForzaPaths.cs` — `DbPath` (":memory:" — DB живёт в RAM)

### DbSchema records

```csharp
DbCar {
    int Id, Year, MakeID, string DisplayName, string MediaName,
    int ClassID, EnginePlacementID, PowertrainID, NumGears, DriveTypeID,
    double CurbWeight, WeightDistribution,
    int FrontTireWidthMM, FrontTireAspect, FrontWheelDiameterIN,
    int RearTireWidthMM,  RearTireAspect,  RearWheelDiameterIN,
    string MakeName,
    double SimPeakPower, SimPeakAngVel, SimPeakTorque, SimPeakTorqueAngVel,
    double SimRedlineAngVel, GameTorqueScale,
    double BodyAeroLongitudinalDrag, BodyAeroForwardDownforceFront, BodyAeroForwardDownforceRear,
    int Displacement, CylinderID, AspirationTypeId
}
// NOTE: CurbWeight × 100 = kg | WeightDistribution 0-1 → ×100 = %
// NOTE: EngineOverrideCurveID DOES NOT EXIST — removed
```

```csharp
DbEngine { int EngineID, double EngineMassKg, string MediaName, int ConfigID,
    int CylinderID, double Compression, int VariableTimingID, int AspirationID_Stock,
    double StockBoostBar, MomentInertia, EngineGraphingMaxTorque, EngineGraphingMaxPower,
    string EngineName, int EngineRotation, int Carbureted, int Diesel, int Rotary }
// DB column name: "EngineMass-kg" — use backtick quoting
```

```csharp
DbTorqueCurve { int TorqueCurveID, double TorqueScale, int NumTorqueValues, double[] V, double ZeroThrottleTorqueScale }
DbMotor { int MotorID, double MotorMassKg, string MediaName, double MomentInertia,
    double MotorGraphingMaxTorque, MotorGraphingMaxPower, string MotorName,
    double RedlineRPM, int NumRPMEntriesArray, int TorqueCurveFullThrottleID, double BatteryCapacity }
DbUpgradePart (base) { int Id, Level, IsStock, ManufacturerID, double MassDiff, int Price, double? WeightDistDiff, DragScale, TorqueScale }
```

Derived records (каждый добавляет свои поля к DbUpgradePart):

| Record | Extra fields |
|---|---|
| `DbUpgradeEngine` | `int Ordinal, EngineID` |
| `DbUpgradeCamshaft` | `int EngineID, RedlineRPM, StallRPM, TorqueCurveMaxRPM, NumRPMEntriesArray, TorqueCurveFullThrottleID` |
| `DbUpgradeValves` | `int EngineID, double TorqueScale` |
| `DbUpgradeDisplacement` | `int EngineID, double TorqueScale, int Disp` |
| `DbUpgradePistons` | `int EngineID, double TorqueScale` |
| `DbUpgradeFuelSystem` | `int EngineID, double TorqueScale, int PartsStringID` |
| `DbUpgradeIgnition` | `int EngineID, double TorqueScale` |
| `DbUpgradeExhaust` | `int EngineID, double TorqueScale, int PartsStringID` |
| `DbUpgradeIntake` | `int EngineID, double TorqueScale` |
| `DbUpgradeFlywheel` | `int EngineID, double MomentInertia` |
| `DbUpgradeManifold` | `int EngineID, double TorqueScale` |
| `DbUpgradeOilCooling` | `int EngineID, double TorqueScale` |
| `DbUpgradeRestrictor` | `int EngineID, double TorqueScale` |
| `DbUpgradeTurboSingle` | `int EngineID, double MaxScale, PowerMaxScale, MinScale, PowerMinScale, RobScale, TorqueDropOffRPM0/1, TorqueDropOffScale0/1, MomentInertia` |
| `DbUpgradeTurboTwin` | same as Single |
| `DbUpgradeCSC` | `int EngineID, double ZeroRPMScale, RedlineRPMScale, RobScale, TorqueDropOffRPM0/1, TorqueDropOffScale0/1` |
| `DbUpgradeDSC` | same as CSC |
| `DbUpgradeIntercooler` | `int EngineID, double MaxScaleScale` |
| `DbUpgradeTireCompound` | `int Ordinal, double DefaultPressureFront, DefaultPressureRear` |
| `DbUpgradeSpringDamper` | `int Ordinal, int PhysicsID` |
| `DbUpgradeBrakes` | `int Ordinal, double GameFrictionScaleBraking, BrakeTorqueSlider, BrakeBiasSlider, FrontBrakeSizeMM, RearBrakeSizeMM, FrontBrakeTorqueClamp, RearBrakeTorqueClamp` — **нет PhysicsID** |
| `DbUpgradeTransmission` | `int Ordinal, double GearRatio0..GearRatio10, FinalDriveRatio, int GearCount` — **нет List_GearRatio** |
| `DbUpgradeDifferential` | `int Ordinal, double FrontLimitedSlipTorqueAccel, Decel, RearLimitedSlipTorqueAccel, Decel, double CenterBias, RelVelClamp` |
| `DbUpgradeAntiSwayFront/Rear` | `int Ordinal, int AntiSwayPhysicsID` |
| `DbUpgradeRearWing` | `int Ordinal, int PhysicsID` |
| `DbUpgradeWeightReduction` | `int CarBodyId (CarId×1000), double Mass, InitialMass, CMHeightM` — MassDiff = Mass - InitialMass |
| `DbUpgradeChassisStiffness` | `int Ordinal` |
| `DbTireWidthFront/Rear` | `int Ordinal, int TireWidthMM` |
| `DbCarBody` | `int Id (CarId×1000), double WheelbaseM, ModelFrontTrackOuterM, ModelRearTrackOuterM, ModelFrontRideHeightM, ModelRearRideHeightM` — **все метры** |

### Fh6DatabaseService API

```csharp
public class Fh6DatabaseService {
    public static Fh6DatabaseService Instance { get; }

    // Startup
    public async Task InitializeAsync();

    // Cars
    public List<DbCar> GetAllCars();
    public DbCar? GetCar(int carId);
    public DbCarBody? GetCarBody(int carBodyId);  // pass carId * 1000

    // Engines
    public DbEngine? GetEngine(int engineId);
    public DbMotor? GetMotor(int motorId);
    public DbTorqueCurve? GetTorqueCurve(int torqueCurveID);

    // Upgrades by Ordinal (CarId)
    public List<DbUpgradeEngine> GetEngineSwaps(int ordinal);
    public List<DbUpgradeTireCompound> GetTireCompounds(int ordinal);
    public List<DbUpgradeSpringDamper> GetSpringDampers(int ordinal);
    public List<DbUpgradeBrakes> GetBrakes(int ordinal);
    public List<DbUpgradeTransmission> GetTransmissions(int ordinal);
    public List<DbUpgradeDifferential> GetDifferentials(int ordinal);
    public List<DbUpgradeAntiSwayFront> GetAntiSwayFront(int ordinal);
    public List<DbUpgradeAntiSwayRear> GetAntiSwayRear(int ordinal);
    public List<DbUpgradeRearWing> GetRearWings(int ordinal);
    public List<DbUpgradeCarBodyChassisStiffness> GetChassisStiffness(int ordinal);
    public List<DbTireWidthFront> GetTireWidthsFront(int ordinal);
    public List<DbTireWidthRear> GetTireWidthsRear(int ordinal);
    public List<DbUpgradeRim> GetRimsFront(int ordinal);
    public List<DbUpgradeRim> GetRimsRear(int ordinal);
    public List<DbUpgradeWeightReduction> GetWeightReductions(int carBodyId);  // CarId × 1000

    // Upgrades by EngineId
    public List<DbUpgradeCamshaft> GetCamshafts(int engineId);
    public List<DbUpgradeValves> GetValves(int engineId);
    public List<DbUpgradeDisplacement> GetDisplacement(int engineId);
    public List<DbUpgradePistonsCompression> GetPistons(int engineId);
    public List<DbUpgradeFuelSystem> GetFuelSystems(int engineId);
    public List<DbUpgradeIgnition> GetIgnition(int engineId);
    public List<DbUpgradeExhaust> GetExhaust(int engineId);
    public List<DbUpgradeIntake> GetIntake(int engineId);
    public List<DbUpgradeFlywheel> GetFlywheels(int engineId);
    public List<DbUpgradeManifold> GetManifolds(int engineId);
    public List<DbUpgradeOilCooling> GetOilCooling(int engineId);
    public List<DbUpgradeRestrictorPlate> GetRestrictors(int engineId);
    public List<DbUpgradeTurboSingle> GetTurbosSingle(int engineId);
    public List<DbUpgradeTurboTwin> GetTurbosTwin(int engineId);
    public List<DbUpgradeCSC> GetCSC(int engineId);
    public List<DbUpgradeDSC> GetDSC(int engineId);
    public List<DbUpgradeIntercooler> GetIntercoolers(int engineId);
}
```

### InitializeAsync — in-memory DB loading

```csharp
async Task InitializeAsync() {
    byte[] bytes = LoadFromEmbeddedResource();  // DUMPER.fh6_db.sqlite

    // Step 1: Open named in-memory DB via raw SQLitePCL
    var db = SQLitePCL.raw.sqlite3_open(
        "file:ForzaTuneMasterDB?mode=memory&cache=shared");

    // Step 2: Deserialize bytes directly into memory (zero temp files)
    SQLitePCL.raw.sqlite3_deserialize(
        db, "main", bytes, bytes.Length, bytes.Length,
        SQLITE_DESERIALIZE_READONLY | SQLITE_DESERIALIZE_FREEONCLOSE);

    // Step 3: Open SqliteConnection to the same named in-memory DB
    var conn = new SqliteConnection(
        "Data Source=ForzaTuneMasterDB;Mode=Memory;Cache=Shared");
    conn.Open();

    // Step 4: Load ALL data into Dictionaries (queries via conn)
    _cars = QueryDict<DbCar>(conn, "SELECT * FROM Data_Car", MapCar);
    _torqueCurves = QueryDict<DbTorqueCurve>(conn, "SELECT * FROM TorqueCurve", ...);
    _engineSwapsByOrdinal = QueryGrouped<DbUpgradeEngine>(conn, "...", ...);
    // ... ~25 tables

    conn.Close();  // Dictionaries retain all data
}
```

### ForzaPaths.cs

```csharp
public static string DbPath => ":memory:";  // DB lives in RAM only
```

---

## Phase 1 — Replace Car List (Wiki → SQLite)

- [ ] **`Services/CarDatabaseService.cs`** — полная перезапись
  - [ ] Удалить: `CommentRegex`, `RowRegex`, `FetchFromWikiAsync`, `ParseWikitext`, `ParseCarRow`, `SaveCache`, `LoadCache`, `DeleteCache`, `IsCacheStale`, HttpClient (~150 строк)
  - [ ] Новый `LoadCarDatabaseAsync()` — список машин из `Fh6Db.GetAllCars()`
  - [ ] Имена: `$"{Year} {MakeName} {DisplayName}".Trim()`
  - [ ] `RefreshAsync` = `LoadCarDatabaseAsync`, `DeleteCache` = no-op

- [ ] **`Models/CarCard.cs`** — добавить поля
  - [ ] `[JsonIgnore] public int CarDbId { get; set; }`
  - [ ] `[JsonIgnore] public int EngineDbId { get; set; }`
  - [ ] `[JsonIgnore] public int CarBodyId { get; set; }`
  - [ ] `[JsonIgnore] public double[]? CachedTorqueCurveNm { get; set; }`
  - [ ] `[JsonIgnore] public double[]? CachedPowerCurveHP { get; set; }`
  - [ ] `private double _curbWeightKg` — DB-масса (не сериализовать)
  - [ ] `TotalMass.get` — если `_curbWeightKg > 0`: `return _curbWeightKg + SumSelectedMassDiffs()`

- [ ] **`ViewModels/CarSpecController.cs`** — убрать wiki/AI, добавить DB
  - [ ] Удалить: `_wikiSpecService`, `_aiCarSpecService`, `_wikiSpecsCts`, `_aiEstimatedFields`
  - [ ] Удалить: `IsSettingAiSpecs`, `AiSpecStatusMessage`, `IsFetchingAiSpecs`, `NeedsCarSelectionHighlight`
  - [ ] Удалить: `FetchAndApplyWikiSpecsAsync()`, `FetchAiCarSpecsAsync()`, `ClearAiSpecStatus()`, `ClearAiEstimatedFields()`, `ClearCache()`, `ClearAiCache()`, `OnCarPropertyChanged()`
  - [ ] Добавить: `PopulateCarFromDb(CarData, CarCard)` — заполнение из Data_Car + Data_CarBody
  - [ ] `SelectCar()` — заменить `FetchAndApplyWikiSpecsAsync()` на `PopulateCarFromDb()`

```csharp
// PopulateCarFromDb — ключевая логика
void PopulateCarFromDb(CarData carData, CarCard car) {
    var dbCar = Fh6Db.GetCar(carData.Id);
    if (dbCar == null) return;

    car.CarDbId = dbCar.Id;
    car.Make = dbCar.MakeName;
    car.Model = dbCar.DisplayName;
    car.Year = dbCar.Year;
    car.EnginePosition = (EnginePosition)dbCar.EnginePlacementID;
    car.DriveType = MapDriveType(dbCar.DriveTypeID);
    car.GearCount = dbCar.NumGears;

    // Stock engine
    var swaps = Fh6Db.GetEngineSwaps(dbCar.Id);
    var stock = swaps.FirstOrDefault(e => e.IsStock);
    if (stock != null) car.EngineDbId = stock.EngineID;

    // Geometry: Data_CarBody in METERS, convert to mm ×1000
    var body = Fh6Db.GetCarBody(dbCar.Id * 1000);  // Id = CarId × 1000
    if (body != null) {
        car.Wheelbase = body.WheelbaseM * 1000;
        car.FrontTrack = body.ModelFrontTrackOuterM * 1000;
        car.RearTrack = body.ModelRearTrackOuterM * 1000;
    }

    // Weight
    car.WeightDistributionFront = dbCar.WeightDistribution * 100;  // 0-1 → %
    car._curbWeightKg = dbCar.CurbWeight * 100;  // ×100 = kg

    NotifyCarDisplayProperties?.Invoke();
}
```

- [ ] **`ViewModels/MainViewModel.cs`** — убрать AI/wiki команды
  - [ ] Удалить: `RefreshCarDatabaseCommand`, `ClearCacheCommand`, `ClearAiCacheCommand`, `FetchAiCarSpecsCommand`
  - [ ] Удалить: `IsFetchingAiSpecs`, `AiSpecStatusMessage`, `HasAiSpecStatus`, `IsWheelbaseAiEstimated`, `IsFrontTrackAiEstimated`, `HasAnyAiEstimatedField`
  - [ ] Удалить методы: `DoRefreshCarDatabase()`, `DoClearCache()`, `DoClearAiCache()`, `DoFetchAiCarSpecs()`
  - [ ] В `LoadProfile()` — убрать `AiEstimatedFields` блок
  - [ ] В `SaveProfile()` — убрать `AiEstimatedFields` параметр

- [ ] **`Views/CarCardView.xaml`** — убрать AI/wiki UI
  - [ ] Удалить строки 68–91: кнопки кэша/обновления
  - [ ] Удалить строки 421–427: AI-предупреждение (⚠)
  - [ ] Удалить строки 428–439: кнопка «AI»

---

## Phase 2 — Dynamic Upgrade Dropdowns

Самая большая фаза. Каждый enum-селектор заменяется на ComboBox из `Fh6DatabaseService`.

- [ ] **`Models/CarCard.cs`** — заменить enum-поля на `int?` PartId
  - [ ] Удалить: `_tireType`, `_suspensionUpgrade`, `_differentialUpgrade`, `_brakesUpgrade` (и свойства)
  - [ ] Добавить ~30 `int?` PartId-полей:

| Группа | Поля |
|---|---|
| **Engine** | `EngineSwapPartId`, `CamshaftPartId`, `DisplacementPartId`, `ValvesPartId`, `PistonsPartId`, `FuelSystemPartId`, `IgnitionPartId`, `ExhaustPartId`, `IntakePartId`, `FlywheelPartId`, `ManifoldPartId`, `OilCoolingPartId`, `RestrictorPartId`, **`ForcedInductionPartId`** (один dropdown: Single\|Twin\|CSC\|DSC\|none), `IntercoolerPartId` |
| **Chassis** | `TireCompoundPartId`, `SpringDamperPartId`, `BrakesPartId`, `DifferentialPartId`, `TransmissionPartId`, `ClutchPartId`, `DrivelinePartId` |
| **Aero/ARB** | `RearWingPartId`, `ArbFrontPartId`, `ArbRearPartId` |
| **Wheels** | `TireWidthFrontPartId`, `TireWidthRearPartId`, `RimFrontPartId`, `RimRearPartId` |
| **Body** | `WeightReductionPartId`, `ChassisStiffnessPartId` |

  - [ ] Computed: `HasFrontARB`, `HasRearARB`, `HasRearAero`, `SuspensionAllowsAdvancedTuning`, `EngineType` (из `Data_Engine.CylinderID+ConfigID`), `AspirationType` (`Data_AspirationType`: 1=Natural, 2=SingleTurbo, 3=TwinTurbo, 4=PD, 5=Centrifugal, 6=Electric)
  - [ ] `SumSelectedMassDiffs()` — суммирует MassDiff всех выбранных частей через `Fh6Db`
  - [ ] Заменить `TurboPartId` + `SuperchargerPartId` на единый `ForcedInductionPartId`

- [ ] **`ViewModels/MainViewModel.cs`** — коллекции апгрейдов
  - [ ] Добавить ~20 `ObservableCollection<DbUpgradePart>` (AvailableTireCompounds, AvailableSuspension, ...)
  - [ ] `PopulateUpgradeOptions(CarCard car)` — вызывается при выборе машины; заполняет все коллекции из Fh6Db
  - [ ] `SelectDefaultParts(CarCard car)` — выбрать `IsStock=true` по умолчанию
  - [ ] `OnCarPartChanged` PropertyChanged handler — пересчёт массы/мощности
  - [ ] Engine Swap Cascade: смена `EngineSwapPartId` → reload всех engine-parts для нового `EngineID`

- [ ] **`Views/CarCardView.xaml`** — новый раздел апгрейдов
  - [ ] Убрать: `TireTypeCombo`, `SuspensionCombo`, `BrakesCombo`, `DiffCombo`, ARB-чекбоксы, Aero-чекбоксы
  - [ ] Убрать: все числовые TextBox (power, torque, mass, tires, geometry, RPM)
  - [ ] Добавить ComboBox-группы по категориям:
    - **Engine**: swap + camshaft + displacement + valves + pistons + fuel + ignition + exhaust + intake + flywheel + manifold + oil + restrictor + **FI (один dropdown)** + intercooler
    - **Drivetrain**: transmission + clutch + differential
    - **Tires & Wheels**: compound + width front/rear + rims front/rear
    - **Suspension**: spring/damper + anti-sway front/rear
    - **Brakes**
    - **Aero**: rear wing
    - **Weight & Body**: weight reduction + chassis stiffness
  - [ ] `DisplayMemberPath="DisplayName"`, `SelectedValue="{Binding Car.*PartId}"`

---

## Phase 3.1 — Calculator Rewrites (DB Bounds)

- [ ] **`TireCalculator.cs`** — давление из `List_UpgradeTireCompound.DefaultPressureFront/Rear` (PSI→bar ÷14.504), ширина из `TireWidthMM`
- [ ] **`SuspensionCalculator.cs`**
  - [ ] `EstimateCGHeight()` заменить на `List_UpgradeCarBodyWeight.CMHeight` (м→мм)
  - [ ] Spring rate min/max из `List_SpringDamperPhysics` (DefSpringRate=29.7 N/mm, min/max колонки)
  - [ ] Ride height базовый из `Data_CarBody.ModelFrontRideHeightM/RearRideHeightM` (м→мм)
- [ ] **`DifferentialCalculator.cs`** — убрать `GetPowerDeliveryFactors()`. Accel/Decel из `Front/RearLimitedSlipTorqueAccel/Decel`. CenterDiffBias для AWD.
- [ ] **`BrakeCalculator.cs`** — `BrakeBiasSlider` и `GameFrictionScaleBraking` из `List_UpgradeBrakes` (нет отдельной physics-таблицы)
- [ ] **`AeroCalculator.cs`** — диапазоны из `List_AeroPhysics` по `RearWing.PhysicsID`
- [ ] **`GearingCalculator.cs`**
  - [ ] Убрать Newton-Raphson для финальной передачи
  - [ ] `GearRatio0..10` + `FinalDriveRatio` из `List_UpgradeDrivetrainTransmission`
  - [ ] GearRatio0=-3.168 (drag reverse), GearRatio7–10=-1 (unused)
  - [ ] Упростить/убрать `PostValidateAndRecalculate()`
- [ ] **`CalculationHelpers.cs`** — убрать `EstimateCGHeight()`, добавить overload `ComputeEffectiveMaxSpeedKmh(car, PowerCurveResult)`

---

## Phase 3.2 — PowerCalculator (NEW)

- [ ] **`Services/PowerCalculator.cs`** (новый)
  - [ ] `PowerCurveResult` record: `RpmPoints[]`, `TorqueNm[]`, `PowerHP[]`, `PeakPowerHP`, `PeakTorqueNm`, `PeakPowerRPM`, `PeakTorqueRPM`
  - [ ] `Compute(CarCard car)` — dispatch по PowertrainType (ICE/EV)
  - [ ] `ComputeIce(car, db)`:
    1. Camshaft → `TorqueCurveFullThrottleID` → `DbTorqueCurve` (V[], TorqueScale, NumTorqueValues)
    2. Произведение TorqueScale от всех engine-частей (Valves, Displacement, Pistons, FuelSystem, Ignition, Exhaust, Intake, Manifold, OilCooling, Restrictor)
    3. FI scaling: `Turbo.MaxScale` или `SC.RedlineRPMScale` (проверить 4 таблицы через `ForcedInductionPartId`)
    4. Lerp по кривой для каждого RPM (шаг 100): `torque = lerp(V) × TorqueScale × scaleProduct × fiScale`; `powerHP = Nm × RPM / 9549`
    5. `EngineOverrideCurveID` не существует — всегда из camshaft
  - [ ] `ComputeEv(car, db)` — Motor → TorqueCurve (без TorqueScale от частей)
  - [ ] `GetPartScale(int? partId, db)` — helper

- [ ] **`TuneGeneratorService.cs`** — в начале `Generate()`: `PowerCalculator.Compute(car)` → `car.PowerHP`, `car.TorqueNm`, `car.MaxRPM`

---

## Phase 3.3 — Power/Torque Graphs (Опционально)

- [ ] Решить нужны ли графики
- [ ] Если да: OxyPlot.Wpf, `Views/PowerCurveView.xaml`, LineSeries из `PowerCurveResult`

---

## Phase 4 — Profile v2.0 Migration

- [ ] **`Services/StorageService.cs`**
  - [ ] `ProfileVersion` → `"v2.0"`
  - [ ] Сериализовать все `int?` PartId-поля (**НЕ** `[JsonIgnore]`)
  - [ ] При загрузке: валидировать PartId (если не существует в DB → fallback на stock)
  - [ ] Backward compat v1.41: маппинг старых enum на stock parts через `MapOldEnumToStockPart()`
  - [ ] Для старых профилей: power/torque/mass оставить как было (пользовательские значения)
  - [ ] Regenerate tune если LastResult существует

---

## Phase 5 — Remove Legacy Services

- [ ] **Удалить файлы:**
  - [ ] `Services/WikiCarSpecService.cs`
  - [ ] `Services/AiCarSpecService.cs`
  - [ ] `Services/ApiKeys.cs`
  - [ ] `Services/ApiKeys.cs.example`

- [ ] **Очистить локализацию** — удалить ~15 ключей из `ru.json` и `en.json`:
  `BusyLoadingCars`, `BusyRefreshingCars`, `BusyFetchingAi`, `StatusCarsNoConnection`, `StatusCarsLoadedFromCache`, `StatusCarsLoaded`, `StatusDbRefreshed`, `StatusDbRefreshError`, `StatusDbUpdateError`, `StatusCacheCleared`, `StatusAutoUpdateFailed`, `AiFetchButton`, `AiEstimatedTooltip`, `AiSpecEstimate`, `AiCacheCleared`, `AiCacheTooltip`, `RefreshDbTooltip`, `ClearCacheTooltip`, `StatusFirstSelectCar`, `StatusAiRequested`, `StatusAiReceived`, `StatusAiError`, `BusyLoadingSpecs`, `StatusSpecsLoaded`, `StatusSpecsError`, `CarsLoadingError`

---

## Итог файлов

| Действие | Файлы |
|---|---|
| **Создать** | `Services/DbSchema.cs`, `Services/Fh6DatabaseService.cs`, `Services/PowerCalculator.cs` |
| **Переписать** | `Services/CarDatabaseService.cs` |
| **Изменить** | `.csproj`, `App.xaml.cs`, `ForzaPaths.cs`, `TuneGeneratorService.cs`, `CalculationHelpers.cs`, `TireCalculator.cs`, `SuspensionCalculator.cs`, `DifferentialCalculator.cs`, `BrakeCalculator.cs`, `AeroCalculator.cs`, `GearingCalculator.cs`, `StorageService.cs`, `CarCard.cs`, `MainViewModel.cs`, `CarSpecController.cs`, `CarCardView.xaml`, `ru.json`, `en.json` |
| **Удалить** | `WikiCarSpecService.cs`, `AiCarSpecService.cs`, `ApiKeys.cs`, `ApiKeys.cs.example` |

## Проверка после каждой фазы

| Фаза | Критерий готовности |
|---|---|
| Phase 0 | `dotnet build` чистый; при старте приложение не падает |
| Phase 1 | Список машин из DB; кнопки AI/cache исчезли |
| Phase 2 | Выбор машины → все дропдауны заполнились; числовых инпутов нет |
| Phase 3 | Generate → разумный тюн; `dotnet test` проходит |
| Phase 4 | Сохранить/загрузить профиль → конфигурация сохраняется |
| Phase 5 | `dotnet build` чистый; нет ссылок на удалённые сервисы |

---

## Data Flow Diagrams

### Power/Torque (ICE)

```
CarCard.SelectedCar
  │
  ├── Fh6Db.GetEngine(Car.EngineDbId)
  ├── Fh6Db.GetCamshaft(Car.CamshaftPartId) → TorqueCurveFullThrottleID
  ├── Fh6Db.GetTorqueCurve(id) → V[], TorqueScale
  ├── Part scales: Valves × Displacement × Pistons × FuelSystem ×
  │              Ignition × Exhaust × Intake × Manifold × OilCooling × Restrictor
  ├── FI scale: Turbo.MaxScale or SC.RedlineRPMScale
  │
  ▼
For each RPM (0 → Redline, step 100):
  torque = lerp(V[idx], V[idx+1]) × curveScale × scaleProduct × fiScale
  powerHP = torque × RPM / 9549
  ▼
PeakTorqueNm, PeakPowerHP, PeakTorqueRPM, PeakPowerRPM
```

### Mass Calculation

```
Data_Car.CurbWeight × 100
  +
Σ MassDiff(EngineSwap, Camshaft, Displacement, Valves, Pistons,
           FuelSystem, Ignition, Exhaust, Intake, Flywheel,
           Manifold, OilCooling, Restrictor, ForcedInduction, Intercooler,
           TireCompound, SpringDamper, Brakes, Differential,
           Transmission, Clutch, Driveline, RearWing,
           ArbFront, ArbRear, TireWidthFront, TireWidthRear,
           RimFront, RimRear, WeightReduction, ChassisStiffness)
  ▼
CarCard.TotalMass
```

### Engine Swap Cascade

```
User changes EngineSwapPartId
  ▼
Fh6Db.GetEngineSwap(newId).EngineID
  ├── car.EngineDbId = newEngineId
  ├── Reload engine parts:
  │     AvailableCamshafts = Fh6Db.GetCamshafts(newEngineId)
  │     AvailableDisplacement = Fh6Db.GetDisplacement(newEngineId)
  │     ... все engine-parts
  └── Reset to stock → recompute power curve
```
