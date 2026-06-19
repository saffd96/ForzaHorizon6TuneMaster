using System.Collections.Concurrent;
using System.Data;
using System.IO;
using System.Reflection;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace Forza_Horizon_6_Tune_Master.Services;

public class Fh6DatabaseService
{
    public static Fh6DatabaseService Instance { get; } = new();

    private readonly ConcurrentDictionary<int, DbCar> _cars = new();
    private readonly ConcurrentDictionary<int, DbCarMake> _carMakes = new();
    private readonly ConcurrentDictionary<int, DbCarBody> _carBodies = new();
    private readonly ConcurrentDictionary<int, DbEngine> _engines = new();
    private readonly ConcurrentDictionary<int, DbMotor> _motors = new();
    private readonly ConcurrentDictionary<int, DbTorqueCurve> _torqueCurves = new();
    private readonly ConcurrentDictionary<int, DbSpringDamperPhysics> _springDamperPhysics = new();
    private readonly ConcurrentDictionary<int, DbAeroPhysics> _aeroPhysics = new();
    private readonly ConcurrentDictionary<int, DbAntiSwayPhysics> _antiSwayPhysics = new();
    private readonly ConcurrentDictionary<int, DbTireCompound> _tireCompounds = new();

    private readonly ConcurrentDictionary<int, List<DbUpgradeEngine>> _engineSwapsByOrdinal = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeCamshaft>> _camshaftsByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeValves>> _valvesByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeDisplacement>> _displacementByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradePistons>> _pistonsByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeFuelSystem>> _fuelSystemsByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeIgnition>> _ignitionsByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeExhaust>> _exhaustsByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeIntake>> _intakesByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeFlywheel>> _flywheelsByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeManifold>> _manifoldsByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeOilCooling>> _oilCoolingsByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeRestrictor>> _restrictorsByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeTurboSingle>> _turbosSingleByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeTurboTwin>> _turbosTwinByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeCSC>> _cscByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeDSC>> _dscByEngineId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeIntercooler>> _intercoolersByEngineId = new();

    private readonly ConcurrentDictionary<int, List<DbUpgradeTireCompound>> _tireCompoundsByOrdinal = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeSpringDamper>> _springDampersByOrdinal = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeBrakes>> _brakesByOrdinal = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeTransmission>> _transmissionsByDrivetrainId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeClutch>> _clutchesByDrivetrainId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeDriveline>> _drivelinesByDrivetrainId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeDifferential>> _differentialsByDrivetrainId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeAntiSwayFront>> _antiSwayFrontByOrdinal = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeAntiSwayRear>> _antiSwayRearByOrdinal = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeRearWing>> _rearWingsByOrdinal = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeFrontBumper>> _frontBumpersByCarBodyId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeRearBumper>> _rearBumpersByCarBodyId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeSideSkirt>> _sideSkirtsByCarBodyId = new();
    // Rim "appearance" reduces to rim mass: stock mass per car (by MediaName) and the
    // distinct aftermarket wheel masses available in the catalog. ConcurrentDictionary
    // because InitializeAsync can run from several threads (parallel tests) before the
    // _initialized guard is set — a plain Dictionary corrupts under concurrent writes.
    private readonly ConcurrentDictionary<string, double> _stockWheelMassByMedia = new();
    private volatile List<double> _wheelMassOptions = new();
    private volatile List<DbWheel> _allWheelOptions = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeWeightReduction>> _weightReductionsByCarBodyId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeChassisStiffness>> _chassisStiffnessByCarBodyId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeTireWidthFront>> _tireWidthsFrontByCarBodyId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeTireWidthRear>> _tireWidthsRearByCarBodyId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeRimFront>> _rimsFrontByOrdinal = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeRimRear>> _rimsRearByOrdinal = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeTireAspectRatioFront>> _tireAspectRatiosFrontByCarBodyId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeTireAspectRatioRear>> _tireAspectRatiosRearByCarBodyId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeTrackSpacingFront>> _trackSpacingsFrontByCarBodyId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeTrackSpacingRear>> _trackSpacingsRearByCarBodyId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeMotorSwap>> _motorSwapsByOrdinal = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeMotorPart>> _motorPartsByMotorId = new();
    private readonly ConcurrentDictionary<int, List<DbUpgradeDrivetrain>> _drivetrainsByOrdinal = new();
    // ConcurrentDictionary: InitializeAsync can run from several threads before the
    // _initialized guard is set; a plain Dictionary corrupts under concurrent writes.
    private readonly ConcurrentDictionary<string, DbUpgradePartInfo> _upgradePartInfoByTableName = new();

    // CarPartNames lookup: (PartsString, Level) → OptionalName1
    private readonly ConcurrentDictionary<(string PartsString, int Level), string> _carPartNames = new();

    private volatile bool _initialized;
    private static readonly object _initLock = new();

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        // Double-checked lock: several callers (app startup, parallel tests) may invoke this
        // at once. The bare _initialized check is not atomic, so without this guard multiple
        // threads ran LoadAllTables concurrently and corrupted the lookup collections.
        lock (_initLock)
        {
            if (_initialized) return;

            try
            {
                Batteries_V2.Init();
                byte[] dbBytes = LoadEmbeddedDb();
                using var conn = OpenEmbeddedDb(dbBytes);
                LoadAllTables(conn);

                _initialized = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка загрузки БД: {ex.Message}\n\n{ex.StackTrace}",
                    "Ошибка инициализации", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        await Task.CompletedTask;
    }

    private static byte[] LoadEmbeddedDb()
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream("DUMPER.fh6_db.sqlite")
            ?? throw new InvalidOperationException("Embedded resource 'DUMPER.fh6_db.sqlite' not found. " +
                "Ensure the .sqlite file is added as <EmbeddedResource> in .csproj.");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }

    private static SqliteConnection OpenEmbeddedDb(byte[] dbBytes)
    {
        string path = Path.Combine(Path.GetTempPath(), "ForzaTuneMaster.sqlite");
        if (!File.Exists(path))
            File.WriteAllBytes(path, dbBytes);
        var conn = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        conn.Open();
        return conn;
    }

    private void LoadAllTables(SqliteConnection conn)
    {
        LoadCarMake(conn);
        LoadCars(conn);
        LoadEngines(conn);
        LoadCarBodies(conn);
        LoadTorqueCurves(conn);
        LoadMotors(conn);
        LoadSpringDamperPhysics(conn);
        LoadAeroPhysics(conn);
        LoadAntiSwayPhysics(conn);
        LoadBaseTireCompounds(conn);
        LoadUpgradePartInfo(conn);
        LoadEngineSwaps(conn);
        LoadCamshafts(conn);
        LoadValves(conn);
        LoadDisplacement(conn);
        LoadPistons(conn);
        LoadFuelSystems(conn);
        LoadIgnitions(conn);
        LoadExhausts(conn);
        LoadIntakes(conn);
        LoadFlywheels(conn);
        LoadManifolds(conn);
        LoadOilCoolings(conn);
        LoadRestrictors(conn);
        LoadTurbosSingle(conn);
        LoadTurbosTwin(conn);
        LoadCSC(conn);
        LoadDSC(conn);
        LoadIntercoolers(conn);
        LoadTireCompounds(conn);
        LoadSpringDampers(conn);
        LoadBrakes(conn);
        LoadDrivetrains(conn);
        LoadTransmissions(conn);
        LoadClutches(conn);
        LoadDrivelines(conn);
        LoadDifferentials(conn);
        LoadAntiSwayFront(conn);
        LoadAntiSwayRear(conn);
        LoadRearWings(conn);
        LoadFrontBumpers(conn);
        LoadRearBumpers(conn);
        LoadSideSkirts(conn);
        LoadWeightReductions(conn);
        LoadChassisStiffness(conn);
        LoadTireWidthsFront(conn);
        LoadTireWidthsRear(conn);
        LoadRimsFront(conn);
        LoadRimsRear(conn);
        LoadTireAspectRatiosFront(conn);
        LoadTireAspectRatiosRear(conn);
        LoadTrackSpacingsFront(conn);
        LoadTrackSpacingsRear(conn);
        LoadMotorSwaps(conn);
        LoadMotorParts(conn);
        LoadWheels(conn);
        LoadCarPartNames(conn);
    }

    private void LoadWheels(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MediaName,Mass,IsStock,ID,DisplayName,PartManufacturerID FROM List_Wheels";
        using var r = cmd.ExecuteReader();
        var masses = new SortedSet<double>();
        var all = new List<DbWheel>();
        while (r.Read())
        {
            string media = S(r, 0); double mass = D(r, 1); bool stock = B(r, 2);
            int id = I(r, 3); string displayName = S(r, 4); int manId = I(r, 5);
            if (stock) { if (!string.IsNullOrEmpty(media)) _stockWheelMassByMedia[media] = mass; }
            if (!stock && mass > 0) masses.Add(mass);
            if (!stock && displayName != "" && !displayName.StartsWith("_&"))
            {
                all.Add(new DbWheel { Id = id, DisplayName = displayName, Mass = mass, ManufacturerId = manId, IsStock = stock });
            }
        }
        _wheelMassOptions = masses.ToList();
        _allWheelOptions = all;
    }

    public double? GetStockWheelMass(string? mediaName) =>
        !string.IsNullOrEmpty(mediaName) && _stockWheelMassByMedia.TryGetValue(mediaName, out var m) ? m : null;

    public IReadOnlyList<double> GetWheelMassOptions() => _wheelMassOptions;

    public IReadOnlyList<DbWheel> GetAllAftermarketWheels() =>
        _allWheelOptions;

    public double? GetWheelMassById(int id)
    {
        var w = _allWheelOptions.FirstOrDefault(x => x.Id == id);
        return w?.Mass;
    }

    private static T Read<T>(SqliteDataReader r, int i) =>
        r.IsDBNull(i) ? default! : (T)r.GetValue(i);

    private static int I(SqliteDataReader r, int i) => r.IsDBNull(i) ? 0 : r.GetInt32(i);
    private static double D(SqliteDataReader r, int i) => r.IsDBNull(i) ? 0.0 : r.GetDouble(i);
    private static bool B(SqliteDataReader r, int i) => r.IsDBNull(i) ? false : r.GetInt32(i) != 0;
    private static string S(SqliteDataReader r, int i) => r.IsDBNull(i) ? "" : r.GetString(i);

    private void LoadCarMake(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ID,DisplayName,ManufacturerCode,CountryID,IconPath,IconPathLarge,IconPathBase,Profile FROM List_CarMake";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            _carMakes[I(r, 0)] = new DbCarMake
            {
                ID = I(r, 0), DisplayName = S(r, 1), ManufacturerCode = S(r, 2),
                CountryID = I(r, 3), IconPath = S(r, 4), IconPathLarge = S(r, 5),
                IconPathBase = S(r, 6), Profile = S(r, 7)
            };
        }
    }

    private void LoadCars(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Year,MakeID,DisplayName,ModelShort,MediaName,ClassID," +
            "EnginePlacementID,PowertrainID,DriveTypeID,CurbWeight,WeightDistribution,NumGears," +
            "FrontTireWidthMM,FrontTireAspect,FrontWheelDiameterIN,RearTireWidthMM," +
            "RearTireAspect,RearWheelDiameterIN,MakeName,SimPeakPower,SimPeakAngVel," +
            "SimPeakTorque,SimPeakTorqueAngVel,SimRedlineAngVel,GameTorqueScale," +
            "BodyAeroLongitudinalDrag,BodyAeroForwardDownforceFront," +
            "BodyAeroForwardDownforceRear,Displacement,CylinderID,AspirationTypeId," +
            "FrontStockRideHeight,RearStockRideHeight,PI," +
            "GameDragScale,GameDownforceScale,GameDownforceScaleOffroad," +
            "FrontDownforceClampKG,RearDownforceClampKG,[TopSpeed-mph],SimTopSpeed," +
            "SimBrakeDistance60MPH,SimBrakeDistance100MPH,BrakeProfile," +
            "Traction_Road,Traction_OffRoad,Traction_Snow FROM Data_Car WHERE IsDrivable=1";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            int id = I(r, 0);
            _cars[id] = new DbCar
            {
                Id = id, Year = I(r, 1), MakeID = I(r, 2),
                DisplayName = S(r, 3), ModelShort = S(r, 4), MediaName = S(r, 5),
                ClassID = I(r, 6), EnginePlacementID = I(r, 7), PowertrainID = I(r, 8),
                DriveTypeID = I(r, 9),
                CurbWeight = D(r, 10), WeightDistribution = D(r, 11), NumGears = I(r, 12),
                FrontTireWidthMM = I(r, 13), FrontTireAspect = I(r, 14),
                FrontWheelDiameterIN = I(r, 15), RearTireWidthMM = I(r, 16),
                RearTireAspect = I(r, 17), RearWheelDiameterIN = I(r, 18),
                MakeName = S(r, 19), SimPeakPower = D(r, 20), SimPeakAngVel = D(r, 21),
                SimPeakTorque = D(r, 22), SimPeakTorqueAngVel = D(r, 23),
                SimRedlineAngVel = D(r, 24), GameTorqueScale = D(r, 25),
                BodyAeroLongitudinalDrag = D(r, 26),
                BodyAeroForwardDownforceFront = D(r, 27),
                BodyAeroForwardDownforceRear = D(r, 28),
                Displacement = I(r, 29), CylinderID = I(r, 30),
                AspirationTypeId = I(r, 31),
                FrontStockRideHeight = D(r, 32), RearStockRideHeight = D(r, 33),
                PI = I(r, 34),
                GameDragScale = D(r, 35), GameDownforceScale = D(r, 36),
                GameDownforceScaleOffroad = D(r, 37),
                FrontDownforceClampKG = D(r, 38), RearDownforceClampKG = D(r, 39),
                TopSpeedMph = D(r, 40), SimTopSpeed = D(r, 41),
                SimBrakeDistance60Mph = D(r, 42), SimBrakeDistance100Mph = D(r, 43),
                BrakeProfileId = I(r, 44),
                TractionRoad = D(r, 45), TractionOffRoad = D(r, 46), TractionSnow = D(r, 47)
            };
        }
    }

    private void LoadCarBodies(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Height,Length,Width,Wheelbase,ModelWheelbase," +
            "ModelFrontTrackOuter,ModelRearTrackOuter," +
            "ModelFrontStockRideHeight,ModelRearStockRideHeight FROM Data_CarBody";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            _carBodies[I(r, 0)] = new DbCarBody
            {
                Id = I(r, 0), Height = D(r, 1), Length = D(r, 2), Width = D(r, 3),
                Wheelbase = D(r, 4), ModelWheelbase = D(r, 5),
                ModelFrontTrackOuter = D(r, 6), ModelRearTrackOuter = D(r, 7),
                ModelFrontStockRideHeight = D(r, 8), ModelRearStockRideHeight = D(r, 9)
            };
        }
    }

    private void LoadEngines(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT EngineID,[EngineMass-kg],MediaName,ConfigID,CylinderID," +
            "Compression,VariableTimingID,AspirationID_Stock,[StockBoost-bar]," +
            "MomentInertia,EngineGraphingMaxTorque,EngineGraphingMaxPower," +
            "EngineName,EngineRotation,Carbureted,Diesel, Rotary FROM Data_Engine";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            _engines[I(r, 0)] = new DbEngine
            {
                EngineID = I(r, 0), EngineMassKg = D(r, 1), MediaName = S(r, 2),
                ConfigID = I(r, 3), CylinderID = I(r, 4), Compression = D(r, 5),
                VariableTimingID = I(r, 6), AspirationID_Stock = I(r, 7),
                StockBoostBar = D(r, 8), MomentInertia = D(r, 9),
                EngineGraphingMaxTorque = D(r, 10), EngineGraphingMaxPower = D(r, 11),
                EngineName = S(r, 12), EngineRotation = I(r, 13),
                Carbureted = B(r, 14), Diesel = B(r, 15), Rotary = B(r, 16)
            };
        }
    }

    private void LoadTorqueCurves(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TorqueCurveID,TorqueScale,NumTorqueValues," +
            "ZeroThrottleTorqueScale FROM List_TorqueCurve";
        // v0..vNumTorqueValues-1 loaded separately
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            int id = I(r, 0);
            int numVals = I(r, 2);
            double[] v = new double[numVals];
            // Load v0..v(numVals-1) via a secondary query
            _torqueCurves[id] = new DbTorqueCurve
            {
                TorqueCurveID = id, TorqueScale = D(r, 1),
                NumTorqueValues = numVals, V = v,
                ZeroThrottleTorqueScale = D(r, 3)
            };
        }

        // Fill V arrays for each curve
        foreach (int id in _torqueCurves.Keys)
        {
            var curve = _torqueCurves[id];
            var vCmd = conn.CreateCommand();
            var cols = string.Join(",", Enumerable.Range(0, curve.NumTorqueValues).Select(i => $"v{i}"));
            vCmd.CommandText = $"SELECT {cols} FROM List_TorqueCurve WHERE TorqueCurveID={id}";
            using var vr = vCmd.ExecuteReader();
            if (vr.Read())
            {
                for (int i = 0; i < curve.NumTorqueValues; i++)
                    curve.V[i] = D(vr, i);
            }
        }
    }

    private void LoadMotors(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MotorID,[MotorMass-kg],MediaName,MomentInertia," +
            "MotorGraphingMaxTorque,MotorGraphingMaxPower,MotorName," +
            "RedlineRPM,NumRPMEntriesArray,TorqueCurveFullThrottleID," +
            "BatteryCapacity FROM Data_Motor";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            _motors[I(r, 0)] = new DbMotor
            {
                MotorID = I(r, 0), MotorMassKg = D(r, 1), MediaName = S(r, 2),
                MomentInertia = D(r, 3), MotorGraphingMaxTorque = D(r, 4),
                MotorGraphingMaxPower = D(r, 5), MotorName = S(r, 6),
                RedlineRPM = D(r, 7), NumRPMEntriesArray = I(r, 8),
                TorqueCurveFullThrottleID = I(r, 9), BatteryCapacity = D(r, 10)
            };
        }
    }

    private void LoadSpringDamperPhysics(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT SpringDamperPhysicsID,Ordinal,DefRideHeight,MinRideHeight," +
            "MaxRideHeight,DefSpringRate,MinSpringRate,MaxSpringRate," +
            "DefDampenBumpRate,MinDampenBumpRate,MaxDampenBumpRate," +
            "DefDampenReboundRate,MinDampenReboundRate,MaxDampenReboundRate," +
            "StaticToe,StaticCamber,Caster " +
            "FROM List_SpringDamperPhysics";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            _springDamperPhysics[I(r, 0)] = new DbSpringDamperPhysics
            {
                SpringDamperPhysicsID = I(r, 0), Ordinal = I(r, 1), DefRideHeight = D(r, 2),
                MinRideHeight = D(r, 3), MaxRideHeight = D(r, 4),
                DefSpringRate = D(r, 5), MinSpringRate = D(r, 6), MaxSpringRate = D(r, 7),
                DefDampenBumpRate = D(r, 8), MinDampenBumpRate = D(r, 9),
                MaxDampenBumpRate = D(r, 10), DefDampenReboundRate = D(r, 11),
                MinDampenReboundRate = D(r, 12), MaxDampenReboundRate = D(r, 13),
                StaticToe = D(r, 14), StaticCamber = D(r, 15), Caster = D(r, 16)
            };
        }
    }

    private void LoadAeroPhysics(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT AeroPhysicsID,Ordinal,DefaultTuneSlider,Drag0,Downforce0," +
            "Drag1,Downforce1,AngleZeroDownforce FROM List_AeroPhysics";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            _aeroPhysics[I(r, 0)] = new DbAeroPhysics
            {
                AeroPhysicsID = I(r, 0), Ordinal = I(r, 1), DefaultTuneSlider = D(r, 2),
                Drag0 = D(r, 3), Downforce0 = D(r, 4),
                Drag1 = D(r, 5), Downforce1 = D(r, 6),
                AngleZeroDownforce = D(r, 7)
            };
        }
    }

    private void LoadAntiSwayPhysics(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT AntiSwayPhysicsID,Ordinal,DefSwaybarStiffness,MinSwaybarStiffness," +
            "MaxSwaybarStiffness,SwaybarDamping,BeamStiffness FROM List_AntiSwayPhysics";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            _antiSwayPhysics[I(r, 0)] = new DbAntiSwayPhysics
            {
                AntiSwayPhysicsID = I(r, 0), Ordinal = I(r, 1),
                DefSwaybarStiffness = D(r, 2), MinSwaybarStiffness = D(r, 3),
                MaxSwaybarStiffness = D(r, 4), SwaybarDamping = D(r, 5),
                BeamStiffness = D(r, 6)
            };
        }
    }

    private void LoadBaseTireCompounds(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TireCompoundID,DisplayName,DefaultPressure,PressureIncreaseWithMassScaler," +
            "TorqueFreeLatFrictionScale,TorqueFreeLongFrictionScaleBrake," +
            "TorqueFreeLongFrictionScaleAccel0,TorqueFreeLongFrictionScaleAccel1," +
            "CompoundStiffness,IsOffroad FROM List_TireCompound";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            _tireCompounds[I(r, 0)] = new DbTireCompound
            {
                TireCompoundID = I(r, 0), DisplayName = S(r, 1),
                DefaultPressure = D(r, 2), PressureIncreaseWithMassScaler = D(r, 3),
                TorqueFreeLatFrictionScale = D(r, 4),
                TorqueFreeLongFrictionScaleBrake = D(r, 5),
                TorqueFreeLongFrictionScaleAccel0 = D(r, 6),
                TorqueFreeLongFrictionScaleAccel1 = D(r, 7),
                CompoundStiffness = D(r, 8), IsOffroad = I(r, 9)
            };
        }
    }

    private void LoadUpgradePartInfo(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,TableName,CategoryName,PartName,CameraName," +
            "OkIfNoStockPart,HasRequiresGraphics,HasManufacturerId FROM Data_UpgradePart";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var info = new DbUpgradePartInfo
            {
                Id = I(r, 0), TableName = S(r, 1), CategoryName = S(r, 2),
                PartName = S(r, 3), CameraName = S(r, 4),
                OkIfNoStockPart = B(r, 5), HasRequiresGraphics = B(r, 6),
                HasManufacturerId = B(r, 7)
            };
            _upgradePartInfoByTableName[info.TableName] = info;
        }
    }

    private static void AddToGroupedDict<T>(ConcurrentDictionary<int, List<T>> dict, int key, T item)
    {
        dict.AddOrUpdate(key, _ => [item], (_, list) => { list.Add(item); return list; });
    }

    // Engine parts
    private void LoadEngineSwaps(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Ordinal,Level,EngineID,IsStock,ManufacturerID,Price," +
            "MassDiff,WeightDistDiff,DragScale,WindInstabilityScale FROM List_UpgradeEngine";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_engineSwapsByOrdinal, I(r, 1), new DbUpgradeEngine
            {
                Id = I(r, 0), Ordinal = I(r, 1), Level = I(r, 2), EngineID = I(r, 3),
                IsStock = B(r, 4), ManufacturerID = I(r, 5), Price = I(r, 6),
                MassDiff = D(r, 7), WeightDistDiff = D(r, 8), DragScale = D(r, 9),
                WindInstabilityScale = D(r, 10)
            });
        }
    }

    private void LoadCamshafts(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,MassDiff,Price," +
            "RedlineRPM,StallRPM,TorqueCurveMaxRPM,NumRPMEntriesArray," +
            "TorqueCurveFullThrottleID FROM List_UpgradeEngineCamshaft";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_camshaftsByEngineId, I(r, 1), new DbUpgradeCamshaft
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), Price = I(r, 6),
                RedlineRPM = D(r, 7), StallRPM = D(r, 8), TorqueCurveMaxRPM = D(r, 9),
                NumRPMEntriesArray = I(r, 10), TorqueCurveFullThrottleID = I(r, 11)
            });
        }
    }

    private void LoadValves(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,MassDiff,Price,TorqueScale FROM List_UpgradeEngineValves";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_valvesByEngineId, I(r, 1), new DbUpgradeValves
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), Price = I(r, 6),
                TorqueScale = D(r, 7)
            });
        }
    }

    private void LoadDisplacement(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,Price,TorqueScale,Disp FROM List_UpgradeEngineDisplacement";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_displacementByEngineId, I(r, 1), new DbUpgradeDisplacement
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), Price = I(r, 5), TorqueScale = D(r, 6),
                Disp = I(r, 7), MassDiff = 0
            });
        }
    }

    private void LoadPistons(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,MassDiff,Price,TorqueScale FROM List_UpgradeEnginePistonsCompression";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_pistonsByEngineId, I(r, 1), new DbUpgradePistons
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), Price = I(r, 6),
                TorqueScale = D(r, 7)
            });
        }
    }

    private void LoadFuelSystems(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,PartsStringID," +
            "MassDiff,WeightDistDiff,DragScale,WindInstabilityScale,Price,TorqueScale " +
            "FROM List_UpgradeEngineFuelSystem";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_fuelSystemsByEngineId, I(r, 1), new DbUpgradeFuelSystem
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), PartsStringID = I(r, 5), MassDiff = D(r, 6),
                WeightDistDiff = D(r, 7), DragScale = D(r, 8),
                WindInstabilityScale = D(r, 9), Price = I(r, 10), TorqueScale = D(r, 11)
            });
        }
    }

    private void LoadIgnitions(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,PartsStringID," +
            "MassDiff,DragScale,WindInstabilityScale,Price,TorqueScale FROM List_UpgradeEngineIgnition";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_ignitionsByEngineId, I(r, 1), new DbUpgradeIgnition
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), PartsStringID = I(r, 5), MassDiff = D(r, 6),
                DragScale = D(r, 7), WindInstabilityScale = D(r, 8), Price = I(r, 9),
                TorqueScale = D(r, 10)
            });
        }
    }

    private void LoadExhausts(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,PartsStringID," +
            "MassDiff,WeightDistDiff,DragScale,WindInstabilityScale,Price,TorqueScale " +
            "FROM List_UpgradeEngineExhaust";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_exhaustsByEngineId, I(r, 1), new DbUpgradeExhaust
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), PartsStringID = I(r, 5), MassDiff = D(r, 6),
                WeightDistDiff = D(r, 7), DragScale = D(r, 8),
                WindInstabilityScale = D(r, 9), Price = I(r, 10), TorqueScale = D(r, 11)
            });
        }
    }

    private void LoadIntakes(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,PartsStringID," +
            "MassDiff,DragScale,WindInstabilityScale,Price,TorqueScale FROM List_UpgradeEngineIntake";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_intakesByEngineId, I(r, 1), new DbUpgradeIntake
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), PartsStringID = I(r, 5), MassDiff = D(r, 6),
                DragScale = D(r, 7), WindInstabilityScale = D(r, 8), Price = I(r, 9),
                TorqueScale = D(r, 10)
            });
        }
    }

    private void LoadFlywheels(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerId,MassDiff,Price," +
            "MomentInertia FROM List_UpgradeEngineFlywheel";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_flywheelsByEngineId, I(r, 1), new DbUpgradeFlywheel
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), Price = I(r, 6),
                MomentInertia = D(r, 7)
            });
        }
    }

    private void LoadManifolds(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,MassDiff,Price,TorqueScale FROM List_UpgradeEngineManifold";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_manifoldsByEngineId, I(r, 1), new DbUpgradeManifold
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), Price = I(r, 6),
                TorqueScale = D(r, 7)
            });
        }
    }

    private void LoadOilCoolings(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,MassDiff," +
            "WeightDistDiff,Price,TorqueScale FROM List_UpgradeEngineOilCooling";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_oilCoolingsByEngineId, I(r, 1), new DbUpgradeOilCooling
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), WeightDistDiff = D(r, 6),
                Price = I(r, 7), TorqueScale = D(r, 8)
            });
        }
    }

    private void LoadRestrictors(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,MassDiff,Price,TorqueScale FROM List_UpgradeEngineRestrictorPlate";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_restrictorsByEngineId, I(r, 1), new DbUpgradeRestrictor
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), Price = I(r, 6),
                TorqueScale = D(r, 7)
            });
        }
    }

    private void LoadTurbosSingle(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,MassDiff," +
            "WeightDistDiff,Price,MomentInertia,MaxScale,PowerMaxScale,MinScale," +
            "PowerMinScale,RobScale,TorqueDropOffRPM0,TorqueDropOffRPM1," +
            "TorqueDropOffScale0,TorqueDropOffScale1 FROM List_UpgradeEngineTurboSingle";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_turbosSingleByEngineId, I(r, 1), new DbUpgradeTurboSingle
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), WeightDistDiff = D(r, 6),
                Price = I(r, 7), MomentInertia = D(r, 8), MaxScale = D(r, 9),
                PowerMaxScale = D(r, 10), MinScale = D(r, 11), PowerMinScale = D(r, 12),
                RobScale = D(r, 13), TorqueDropOffRPM0 = D(r, 14),
                TorqueDropOffRPM1 = D(r, 15), TorqueDropOffScale0 = D(r, 16),
                TorqueDropOffScale1 = D(r, 17)
            });
        }
    }

    private void LoadTurbosTwin(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,MassDiff," +
            "WeightDistDiff,Price,MomentInertia,MaxScale,PowerMaxScale,MinScale," +
            "PowerMinScale,RobScale,TorqueDropOffRPM0,TorqueDropOffRPM1," +
            "TorqueDropOffScale0,TorqueDropOffScale1 FROM List_UpgradeEngineTurboTwin";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_turbosTwinByEngineId, I(r, 1), new DbUpgradeTurboTwin
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), WeightDistDiff = D(r, 6),
                Price = I(r, 7), MomentInertia = D(r, 8), MaxScale = D(r, 9),
                PowerMaxScale = D(r, 10), MinScale = D(r, 11), PowerMinScale = D(r, 12),
                RobScale = D(r, 13), TorqueDropOffRPM0 = D(r, 14),
                TorqueDropOffRPM1 = D(r, 15), TorqueDropOffScale0 = D(r, 16),
                TorqueDropOffScale1 = D(r, 17)
            });
        }
    }

    private void LoadCSC(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,PartsStringID," +
            "MassDiff,WeightDistDiff,DragScale,WindInstabilityScale,Price," +
            "ZeroRPMScale,RedlineRPMScale,RobScale,TorqueDropOffRPM0," +
            "TorqueDropOffRPM1,TorqueDropOffScale0,TorqueDropOffScale1 " +
            "FROM List_UpgradeEngineCSC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_cscByEngineId, I(r, 1), new DbUpgradeCSC
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), PartsStringID = I(r, 5), MassDiff = D(r, 6),
                WeightDistDiff = D(r, 7), DragScale = D(r, 8),
                WindInstabilityScale = D(r, 9), Price = I(r, 10),
                ZeroRPMScale = D(r, 11), RedlineRPMScale = D(r, 12),
                RobScale = D(r, 13), TorqueDropOffRPM0 = D(r, 14),
                TorqueDropOffRPM1 = D(r, 15), TorqueDropOffScale0 = D(r, 16),
                TorqueDropOffScale1 = D(r, 17)
            });
        }
    }

    private void LoadDSC(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,PartsStringID," +
            "MassDiff,WeightDistDiff,DragScale,WindInstabilityScale,Price," +
            "ZeroRPMScale,RedlineRPMScale,RobScale,TorqueDropOffRPM0," +
            "TorqueDropOffRPM1,TorqueDropOffScale0,TorqueDropOffScale1 " +
            "FROM List_UpgradeEngineDSC";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_dscByEngineId, I(r, 1), new DbUpgradeDSC
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), PartsStringID = I(r, 5), MassDiff = D(r, 6),
                WeightDistDiff = D(r, 7), DragScale = D(r, 8),
                WindInstabilityScale = D(r, 9), Price = I(r, 10),
                ZeroRPMScale = D(r, 11), RedlineRPMScale = D(r, 12),
                RobScale = D(r, 13), TorqueDropOffRPM0 = D(r, 14),
                TorqueDropOffRPM1 = D(r, 15), TorqueDropOffScale0 = D(r, 16),
                TorqueDropOffScale1 = D(r, 17)
            });
        }
    }

    private void LoadIntercoolers(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,EngineID,Level,IsStock,ManufacturerID,PartsStringID," +
            "MassDiff,WeightDistDiff,DragScale,WindInstabilityScale,Price,MaxScaleScale " +
            "FROM List_UpgradeEngineIntercooler";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_intercoolersByEngineId, I(r, 1), new DbUpgradeIntercooler
            {
                Id = I(r, 0), EngineID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), PartsStringID = I(r, 5), MassDiff = D(r, 6),
                WeightDistDiff = D(r, 7), DragScale = D(r, 8),
                WindInstabilityScale = D(r, 9), Price = I(r, 10),
                MaxScaleScale = D(r, 11)
            });
        }
    }

    // Chassis parts
    private void LoadTireCompounds(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Ordinal,Level,IsStock,ManufacturerID,Price,MassDiff," +
            "DragScale,WindInstabilityScale,TireCompoundID,FrontTirePressure,RearTirePressure," +
            "TireModelName FROM List_UpgradeTireCompound";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_tireCompoundsByOrdinal, I(r, 1), new DbUpgradeTireCompound
            {
                Id = I(r, 0), Ordinal = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), Price = I(r, 5), MassDiff = D(r, 6),
                DragScale = D(r, 7), WindInstabilityScale = D(r, 8),
                TireCompoundID = I(r, 9), FrontTirePressure = D(r, 10), RearTirePressure = D(r, 11),
                TireModelName = S(r, 12)
            });
        }
    }

    private void LoadSpringDampers(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Ordinal,Level,ManufacturerID,MassDiff,DragScale," +
            "WindInstabilityScale,Price,IsStock,FrontSpringDamperPhysicsID," +
            "RearSpringDamperPhysicsID FROM List_UpgradeSpringDamper";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_springDampersByOrdinal, I(r, 1), new DbUpgradeSpringDamper
            {
                Id = I(r, 0), Ordinal = I(r, 1), Level = I(r, 2),
                ManufacturerID = I(r, 3), MassDiff = D(r, 4), DragScale = D(r, 5),
                WindInstabilityScale = D(r, 6), Price = I(r, 7), IsStock = B(r, 8),
                FrontSpringDamperPhysicsID = I(r, 9), RearSpringDamperPhysicsID = I(r, 10)
            });
        }
    }

    private void LoadBrakes(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Ordinal,Level,IsStock,ManufacturerID,MassDiff,Price," +
            "FrontBrakeSizeMM,RearBrakeSizeMM,GameFrictionScaleBraking," +
            "BrakeTorqueSlider,BrakeBiasSlider,FrontBrakeTorqueClamp,RearBrakeTorqueClamp " +
            "FROM List_UpgradeBrakes";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_brakesByOrdinal, I(r, 1), new DbUpgradeBrakes
            {
                Id = I(r, 0), Ordinal = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), Price = I(r, 6),
                FrontBrakeSizeMM = D(r, 7), RearBrakeSizeMM = D(r, 8),
                GameFrictionScaleBraking = D(r, 9), BrakeTorqueSlider = D(r, 10),
                BrakeBiasSlider = D(r, 11), FrontBrakeTorqueClamp = D(r, 12),
                RearBrakeTorqueClamp = D(r, 13)
            });
        }
    }

    private void LoadDrivetrains(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT d.Id,d.Ordinal,d.DrivetrainID,d.PowertrainId,d.MassDiff,d.WeightDistDiff,d.Level," +
            "d.ManufacturerId,d.Price,d.IsStock,dt.DrivetypeID " +
            "FROM List_UpgradeDrivetrain d " +
            "JOIN Data_Drivetrain dt ON d.DrivetrainID = dt.DrivetrainID";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_drivetrainsByOrdinal, I(r, 1), new DbUpgradeDrivetrain
            {
                Id = I(r, 0), Ordinal = I(r, 1), DrivetrainID = I(r, 2), PowertrainId = I(r, 3),
                MassDiff = D(r, 4), WeightDistDiff = D(r, 5), Level = I(r, 6),
                ManufacturerID = I(r, 7), Price = I(r, 8), IsStock = B(r, 9), DriveTypeID = I(r, 10)
            });
        }
    }

    private void LoadTransmissions(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,DrivetrainID,Level,IsStock,ManufacturerID,MassDiff,Price," +
            "MomentInertia,GearShiftTime,RequiresClutchWhenShifting,FinalDriveRatio,NumGears," +
            "GearRatio0,GearRatio1,GearRatio2,GearRatio3,GearRatio4,GearRatio5," +
            "GearRatio6,GearRatio7,GearRatio8,CVTShiftTime,GearRatio9,GearRatio10 " +
            "FROM List_UpgradeDrivetrainTransmission";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_transmissionsByDrivetrainId, I(r, 1), new DbUpgradeTransmission
            {
                Id = I(r, 0), DrivetrainID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), Price = I(r, 6),
                FinalDriveRatio = D(r, 10), NumGears = I(r, 11),
                GearRatio0 = D(r, 12), GearRatio1 = D(r, 13), GearRatio2 = D(r, 14),
                GearRatio3 = D(r, 15), GearRatio4 = D(r, 16), GearRatio5 = D(r, 17),
                GearRatio6 = D(r, 18), GearRatio7 = D(r, 19), GearRatio8 = D(r, 20),
                GearRatio9 = D(r, 22), GearRatio10 = D(r, 23)
            });
        }
    }

    private void LoadClutches(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,DrivetrainID,Level,IsStock,ManufacturerID,MassDiff,Price," +
            "ClutchFriction FROM List_UpgradeDrivetrainClutch";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_clutchesByDrivetrainId, I(r, 1), new DbUpgradeClutch
            {
                Id = I(r, 0), DrivetrainID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), Price = I(r, 6),
                ClutchFriction = D(r, 7)
            });
        }
    }

    private void LoadDrivelines(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,DrivetrainId,Level,IsStock,ManufacturerID,MassDiff,Price," +
            "MomentInertia FROM List_UpgradeDrivetrainDriveline";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_drivelinesByDrivetrainId, I(r, 1), new DbUpgradeDriveline
            {
                Id = I(r, 0), DrivetrainId = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), Price = I(r, 6),
                MomentInertia = D(r, 7)
            });
        }
    }

    private void LoadDifferentials(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,DrivetrainID,Level,IsStock,ManufacturerID,MassDiff,Price," +
            "FrontLimitedSlipTorqueAccel,FrontLimitedSlipTorqueDecel," +
            "FrontLimitedSlipRelVelClamp,FrontLimitedSlipAccelDefInputTorque," +
            "RearLimitedSlipTorqueAccel,RearLimitedSlipTorqueDecel," +
            "RearLimitedSlipRelVelClamp,RearLimitedSlipAccelDefInputTorque," +
            "CenterLimitedSlipTorqueAccel,CenterLimitedSlipTorqueDecel," +
            "CenterLimitedSlipRelVelClamp,CenterLimitedSlipAccelDefInputTorque," +
            "RearToqueSplit " +
            "FROM List_UpgradeDrivetrainDifferential";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_differentialsByDrivetrainId, I(r, 1), new DbUpgradeDifferential
            {
                Id = I(r, 0), DrivetrainID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), Price = I(r, 6),
                FrontLimitedSlipTorqueAccel = D(r, 7),
                FrontLimitedSlipTorqueDecel = D(r, 8),
                RearLimitedSlipTorqueAccel = D(r, 11),
                RearLimitedSlipTorqueDecel = D(r, 12),
                CenterLimitedSlipTorqueAccel = D(r, 15),
                CenterLimitedSlipTorqueDecel = D(r, 16),
                RearToqueSplit = D(r, 19)
            });
        }
    }

    private void LoadAntiSwayFront(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Ordinal,Level,ManufacturerID,MassDiff,Price,IsStock," +
            "AntiSwayPhysicsID FROM List_UpgradeAntiSwayFront";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_antiSwayFrontByOrdinal, I(r, 1), new DbUpgradeAntiSwayFront
            {
                Id = I(r, 0), Ordinal = I(r, 1), Level = I(r, 2),
                ManufacturerID = I(r, 3), MassDiff = D(r, 4), Price = I(r, 5),
                IsStock = B(r, 6), AntiSwayPhysicsID = I(r, 7)
            });
        }
    }

    private void LoadAntiSwayRear(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Ordinal,Level,ManufacturerID,MassDiff,Price,IsStock," +
            "AntiSwayPhysicsID FROM List_UpgradeAntiSwayRear";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_antiSwayRearByOrdinal, I(r, 1), new DbUpgradeAntiSwayRear
            {
                Id = I(r, 0), Ordinal = I(r, 1), Level = I(r, 2),
                ManufacturerID = I(r, 3), MassDiff = D(r, 4), Price = I(r, 5),
                IsStock = B(r, 6), AntiSwayPhysicsID = I(r, 7)
            });
        }
    }

    private void LoadRearWings(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Ordinal,Level,IsStock,ManufacturerId,MassDiff,DragScale," +
            "WindInstabilityScale,Price,AeroPhysicsID FROM List_UpgradeRearWing";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_rearWingsByOrdinal, I(r, 1), new DbUpgradeRearWing
            {
                Id = I(r, 0), Ordinal = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), DragScale = D(r, 6),
                WindInstabilityScale = D(r, 7), Price = I(r, 8),
                AeroPhysicsID = I(r, 9)
            });
        }
    }

    private void LoadFrontBumpers(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,CarBodyID,Level,IsStock,ManufacturerId,MassDiff," +
            "DragScale,WindInstabilityScale,Price,AeroPhysicsID FROM List_UpgradeCarBodyFrontBumper";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_frontBumpersByCarBodyId, I(r, 1), new DbUpgradeFrontBumper
            {
                Id = I(r, 0), CarBodyId = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), DragScale = D(r, 6),
                WindInstabilityScale = D(r, 7), Price = I(r, 8), AeroPhysicsID = I(r, 9)
            });
        }
    }

    private void LoadRearBumpers(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,CarBodyID,Level,IsStock,ManufacturerId,MassDiff," +
            "DragScale,WindInstabilityScale,Price,BodyAeroForwardDownforceFront," +
            "BodyAeroForwardDownforceRear FROM List_UpgradeCarBodyRearBumper";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_rearBumpersByCarBodyId, I(r, 1), new DbUpgradeRearBumper
            {
                Id = I(r, 0), CarBodyId = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), DragScale = D(r, 6),
                WindInstabilityScale = D(r, 7), Price = I(r, 8),
                BodyAeroForwardDownforceFront = D(r, 9), BodyAeroForwardDownforceRear = D(r, 10)
            });
        }
    }

    private void LoadSideSkirts(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,CarBodyID,Level,IsStock,ManufacturerId,MassDiff," +
            "DragScale,WindInstabilityScale,Price FROM List_UpgradeCarBodySideSkirt";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_sideSkirtsByCarBodyId, I(r, 1), new DbUpgradeSideSkirt
            {
                Id = I(r, 0), CarBodyId = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), DragScale = D(r, 6),
                WindInstabilityScale = D(r, 7), Price = I(r, 8)
            });
        }
    }

    private void LoadWeightReductions(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,CarBodyId,Level,IsStock,ManufacturerID,Mass,Price," +
            "InitialMass,CMHeight,CMBackFront FROM List_UpgradeCarBodyWeight";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_weightReductionsByCarBodyId, I(r, 1), new DbUpgradeWeightReduction
            {
                Id = I(r, 0), CarBodyId = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), Mass = D(r, 5), Price = I(r, 6),
                InitialMass = D(r, 7), CMHeight = D(r, 8), CMBackFront = D(r, 9),
                MassDiff = D(r, 5) - D(r, 7) // Mass - InitialMass
            });
        }
    }

    private void LoadChassisStiffness(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,CarbodyId,Level,ManufacturerID,MassDiff,WeightDistDiff," +
            "Price,PartsStringID,CMHeightDiff,IsStock FROM List_UpgradeCarBodyChassisStiffness";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_chassisStiffnessByCarBodyId, I(r, 1), new DbUpgradeChassisStiffness
            {
                Id = I(r, 0), CarbodyId = I(r, 1), Level = I(r, 2),
                ManufacturerID = I(r, 3), MassDiff = D(r, 4), WeightDistDiff = D(r, 5),
                Price = I(r, 6), PartsStringID = I(r, 7), CMHeightDiff = D(r, 8),
                IsStock = B(r, 9)
            });
        }
    }

    private void LoadTireWidthsFront(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,CarBodyId,Level,IsStock,FrontTireWidth,Price,MassDiff," +
            "DragScale,WindInstabilityScale FROM List_UpgradeCarBodyTireWidthFront";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_tireWidthsFrontByCarBodyId, I(r, 1), new DbUpgradeTireWidthFront
            {
                Id = I(r, 0), CarBodyId = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                FrontTireWidth = I(r, 4), Price = I(r, 5), MassDiff = D(r, 6),
                DragScale = D(r, 7), WindInstabilityScale = D(r, 8)
            });
        }
    }

    private void LoadTireWidthsRear(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,CarBodyId,Level,IsStock,RearTireWidth,Price,MassDiff," +
            "DragScale,WindInstabilityScale FROM List_UpgradeCarBodyTireWidthRear";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_tireWidthsRearByCarBodyId, I(r, 1), new DbUpgradeTireWidthRear
            {
                Id = I(r, 0), CarBodyId = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                RearTireWidth = I(r, 4), Price = I(r, 5), MassDiff = D(r, 6),
                DragScale = D(r, 7), WindInstabilityScale = D(r, 8)
            });
        }
    }

    private void LoadRimsFront(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Ordinal,Level,IsStock,FrontWheelDiameter,Price,MassDiff," +
            "DragScale,WindInstabilityScale FROM List_UpgradeRimSizeFront";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_rimsFrontByOrdinal, I(r, 1), new DbUpgradeRimFront
            {
                Id = I(r, 0), Ordinal = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                FrontWheelDiameter = I(r, 4), Price = I(r, 5), MassDiff = D(r, 6),
                DragScale = D(r, 7), WindInstabilityScale = D(r, 8)
            });
        }
    }

    private void LoadRimsRear(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Ordinal,Level,IsStock,RearWheelDiameter,Price,MassDiff," +
            "DragScale,WindInstabilityScale FROM List_UpgradeRimSizeRear";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_rimsRearByOrdinal, I(r, 1), new DbUpgradeRimRear
            {
                Id = I(r, 0), Ordinal = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                RearWheelDiameter = I(r, 4), Price = I(r, 5), MassDiff = D(r, 6),
                DragScale = D(r, 7), WindInstabilityScale = D(r, 8)
            });
        }
    }

    private void LoadTireAspectRatiosFront(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,CarBodyId,FrontTireAspectRatioOffset,IsStock,Level,Price " +
            "FROM List_UpgradeCarBodyTireAspectRatioFront";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_tireAspectRatiosFrontByCarBodyId, I(r, 1), new DbUpgradeTireAspectRatioFront
            {
                Id = I(r, 0), CarBodyId = I(r, 1), FrontTireAspectRatioOffset = D(r, 2),
                IsStock = B(r, 3), Level = I(r, 4), Price = I(r, 5)
            });
        }
    }

    private void LoadTireAspectRatiosRear(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,CarBodyId,RearTireAspectRatioOffset,IsStock,Level,Price " +
            "FROM List_UpgradeCarBodyTireAspectRatioRear";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_tireAspectRatiosRearByCarBodyId, I(r, 1), new DbUpgradeTireAspectRatioRear
            {
                Id = I(r, 0), CarBodyId = I(r, 1), RearTireAspectRatioOffset = D(r, 2),
                IsStock = B(r, 3), Level = I(r, 4), Price = I(r, 5)
            });
        }
    }

    private void LoadTrackSpacingsFront(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,CarBodyId,Spacing,IsStock,Level,Price FROM List_UpgradeCarBodyTrackSpacingFront";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_trackSpacingsFrontByCarBodyId, I(r, 1), new DbUpgradeTrackSpacingFront
            {
                Id = I(r, 0), CarBodyId = I(r, 1), Spacing = D(r, 2),
                IsStock = B(r, 3), Level = I(r, 4), Price = I(r, 5)
            });
        }
    }

    private void LoadTrackSpacingsRear(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,CarBodyId,Spacing,IsStock,Level,Price FROM List_UpgradeCarBodyTrackSpacingRear";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_trackSpacingsRearByCarBodyId, I(r, 1), new DbUpgradeTrackSpacingRear
            {
                Id = I(r, 0), CarBodyId = I(r, 1), Spacing = D(r, 2),
                IsStock = B(r, 3), Level = I(r, 4), Price = I(r, 5)
            });
        }
    }

    private void LoadMotorSwaps(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,Ordinal,Level,MotorID,IsStock,ManufacturerID,Price,MassDiff,WeightDistDiff " +
            "FROM List_UpgradeMotor";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_motorSwapsByOrdinal, I(r, 1), new DbUpgradeMotorSwap
            {
                Id = I(r, 0), Ordinal = I(r, 1), Level = I(r, 2), MotorID = I(r, 3),
                IsStock = B(r, 4), ManufacturerID = I(r, 5), Price = I(r, 6),
                MassDiff = D(r, 7), WeightDistDiff = D(r, 8)
            });
        }
    }

    private void LoadMotorParts(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id,MotorID,Level,IsStock,ManufacturerID,MassDiff,WeightDistDiff,Price,TorqueScale " +
            "FROM List_UpgradeMotorParts";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            AddToGroupedDict(_motorPartsByMotorId, I(r, 1), new DbUpgradeMotorPart
            {
                Id = I(r, 0), MotorID = I(r, 1), Level = I(r, 2), IsStock = B(r, 3),
                ManufacturerID = I(r, 4), MassDiff = D(r, 5), WeightDistDiff = D(r, 6),
                Price = I(r, 7), TorqueScale = D(r, 8)
            });
        }
    }

    private void LoadCarPartNames(SqliteConnection conn)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT PartName FROM CarPartNames";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var key = S(r, 0);
                if (!string.IsNullOrEmpty(key))
                    _carPartNames[(key, 0)] = key;
            }
        }
        catch
        {
            // non-critical — PartDisplayNameResolver falls back to auto-generated names
        }
    }

    public string? GetCarPartName(string partsString, int level)
    {
        var key = (partsString, level);
        return _carPartNames.TryGetValue(key, out var val) ? val : null;
    }

    public DbUpgradePartInfo? GetUpgradePartInfo(string tableName) =>
        _upgradePartInfoByTableName.TryGetValue(tableName, out var info) ? info : null;

    // Public query methods
    public List<DbCar> GetAllCars() => _cars.Values.OrderBy(c => c.CarDisplayName).ToList();
    public DbCar? GetCar(int carId) => _cars.TryGetValue(carId, out var c) ? c : null;
    public DbCarMake? GetCarMake(int makeId) => _carMakes.TryGetValue(makeId, out var m) ? m : null;
    public string GetReadableMakeName(int makeId) => GetCarMake(makeId)?.IconPathBase ?? "";
    public string GetManufacturerCode(int makeId) => GetCarMake(makeId)?.ManufacturerCode ?? "";
    public DbCarBody? GetCarBody(int carBodyId) => _carBodies.TryGetValue(carBodyId, out var cb) ? cb : null;
    public DbEngine? GetEngine(int engineId) => _engines.TryGetValue(engineId, out var e) ? e : null;
    public DbMotor? GetMotor(int motorId) => _motors.TryGetValue(motorId, out var m) ? m : null;
    public DbTorqueCurve? GetTorqueCurve(int id) => _torqueCurves.TryGetValue(id, out var tc) ? tc : null;
    public DbSpringDamperPhysics? GetSpringDamperPhysics(int id) => _springDamperPhysics.TryGetValue(id, out var p) ? p : null;
    public DbAeroPhysics? GetAeroPhysics(int id) => _aeroPhysics.TryGetValue(id, out var a) ? a : null;
    public DbAntiSwayPhysics? GetAntiSwayPhysics(int id) => _antiSwayPhysics.TryGetValue(id, out var a) ? a : null;
    public DbTireCompound? GetTireCompound(int id) => _tireCompounds.TryGetValue(id, out var c) ? c : null;

    public List<DbUpgradeEngine> GetEngineSwaps(int ordinal) =>
        _engineSwapsByOrdinal.TryGetValue(ordinal, out var l) ? l : [];
    public List<DbUpgradeCamshaft> GetCamshafts(int engineId) =>
        _camshaftsByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeValves> GetValves(int engineId) =>
        _valvesByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeDisplacement> GetDisplacement(int engineId) =>
        _displacementByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradePistons> GetPistons(int engineId) =>
        _pistonsByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeFuelSystem> GetFuelSystems(int engineId) =>
        _fuelSystemsByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeIgnition> GetIgnition(int engineId) =>
        _ignitionsByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeExhaust> GetExhaust(int engineId) =>
        _exhaustsByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeIntake> GetIntake(int engineId) =>
        _intakesByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeFlywheel> GetFlywheels(int engineId) =>
        _flywheelsByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeManifold> GetManifolds(int engineId) =>
        _manifoldsByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeOilCooling> GetOilCooling(int engineId) =>
        _oilCoolingsByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeRestrictor> GetRestrictors(int engineId) =>
        _restrictorsByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeTurboSingle> GetTurbosSingle(int engineId) =>
        _turbosSingleByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeTurboTwin> GetTurbosTwin(int engineId) =>
        _turbosTwinByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeCSC> GetCSC(int engineId) =>
        _cscByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeDSC> GetDSC(int engineId) =>
        _dscByEngineId.TryGetValue(engineId, out var l) ? l : [];
    public List<DbUpgradeIntercooler> GetIntercoolers(int engineId) =>
        _intercoolersByEngineId.TryGetValue(engineId, out var l) ? l : [];

    public List<DbUpgradeTireCompound> GetTireCompounds(int ordinal) =>
        _tireCompoundsByOrdinal.TryGetValue(ordinal, out var l) ? l : [];
    public List<DbUpgradeSpringDamper> GetSpringDampers(int ordinal) =>
        _springDampersByOrdinal.TryGetValue(ordinal, out var l) ? l : [];
    public List<DbUpgradeBrakes> GetBrakes(int ordinal) =>
        _brakesByOrdinal.TryGetValue(ordinal, out var l) ? l : [];
    public List<DbUpgradeTransmission> GetTransmissions(int drivetrainId) =>
        _transmissionsByDrivetrainId.TryGetValue(drivetrainId, out var l) ? l : [];
    public List<DbUpgradeClutch> GetClutches(int drivetrainId) =>
        _clutchesByDrivetrainId.TryGetValue(drivetrainId, out var l) ? l : [];
    public List<DbUpgradeDriveline> GetDrivelines(int drivetrainId) =>
        _drivelinesByDrivetrainId.TryGetValue(drivetrainId, out var l) ? l : [];
    public List<DbUpgradeDifferential> GetDifferentials(int drivetrainId) =>
        _differentialsByDrivetrainId.TryGetValue(drivetrainId, out var l) ? l : [];
    public List<DbUpgradeAntiSwayFront> GetAntiSwayFront(int ordinal) =>
        _antiSwayFrontByOrdinal.TryGetValue(ordinal, out var l) ? l : [];
    public List<DbUpgradeAntiSwayRear> GetAntiSwayRear(int ordinal) =>
        _antiSwayRearByOrdinal.TryGetValue(ordinal, out var l) ? l : [];
    public List<DbUpgradeRearWing> GetRearWings(int ordinal) =>
        _rearWingsByOrdinal.TryGetValue(ordinal, out var l) ? l : [];
    public List<DbUpgradeFrontBumper> GetFrontBumpers(int carBodyId) =>
        _frontBumpersByCarBodyId.TryGetValue(carBodyId, out var l) ? l : [];
    public List<DbUpgradeRearBumper> GetRearBumpers(int carBodyId) =>
        _rearBumpersByCarBodyId.TryGetValue(carBodyId, out var l) ? l : [];
    public List<DbUpgradeSideSkirt> GetSideSkirts(int carBodyId) =>
        _sideSkirtsByCarBodyId.TryGetValue(carBodyId, out var l) ? l : [];
    public List<DbUpgradeWeightReduction> GetWeightReductions(int carBodyId) =>
        _weightReductionsByCarBodyId.TryGetValue(carBodyId, out var l) ? l : [];
    public List<DbUpgradeChassisStiffness> GetChassisStiffness(int carBodyId) =>
        _chassisStiffnessByCarBodyId.TryGetValue(carBodyId, out var l) ? l : [];
    public List<DbUpgradeTireWidthFront> GetTireWidthsFront(int carBodyId) =>
        _tireWidthsFrontByCarBodyId.TryGetValue(carBodyId, out var l) ? l : [];
    public List<DbUpgradeTireWidthRear> GetTireWidthsRear(int carBodyId) =>
        _tireWidthsRearByCarBodyId.TryGetValue(carBodyId, out var l) ? l : [];
    public List<DbUpgradeRimFront> GetRimsFront(int ordinal) =>
        _rimsFrontByOrdinal.TryGetValue(ordinal, out var l) ? l : [];
    public List<DbUpgradeRimRear> GetRimsRear(int ordinal) =>
        _rimsRearByOrdinal.TryGetValue(ordinal, out var l) ? l : [];
    public List<DbUpgradeTireAspectRatioFront> GetTireAspectRatiosFront(int carBodyId) =>
        _tireAspectRatiosFrontByCarBodyId.TryGetValue(carBodyId, out var l) ? l : [];
    public List<DbUpgradeTireAspectRatioRear> GetTireAspectRatiosRear(int carBodyId) =>
        _tireAspectRatiosRearByCarBodyId.TryGetValue(carBodyId, out var l) ? l : [];
    public List<DbUpgradeTrackSpacingFront> GetTrackSpacingsFront(int carBodyId) =>
        _trackSpacingsFrontByCarBodyId.TryGetValue(carBodyId, out var l) ? l : [];
    public List<DbUpgradeTrackSpacingRear> GetTrackSpacingsRear(int carBodyId) =>
        _trackSpacingsRearByCarBodyId.TryGetValue(carBodyId, out var l) ? l : [];
    public List<DbUpgradeMotorSwap> GetMotorSwaps(int ordinal) =>
        _motorSwapsByOrdinal.TryGetValue(ordinal, out var l) ? l : [];
    public List<DbUpgradeMotorPart> GetMotorParts(int motorId) =>
        _motorPartsByMotorId.TryGetValue(motorId, out var l) ? l : [];
    public List<DbUpgradeDrivetrain> GetDrivetrainSwaps(int ordinal) =>
        _drivetrainsByOrdinal.TryGetValue(ordinal, out var l) ? l : [];
    public DbUpgradeDrivetrain? GetStockDrivetrain(int ordinal) =>
        GetDrivetrainSwaps(ordinal).FirstOrDefault(p => p.IsStock) ?? GetDrivetrainSwaps(ordinal).FirstOrDefault();
    public DbUpgradeDrivetrain? GetDrivetrainSwapById(int id) =>
        _drivetrainsByOrdinal.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);

    // Single-item lookups by ID (used by CarCard.SumSelectedMassDiffs)
    public DbUpgradeEngine? GetEngineSwapById(int id) =>
        _engineSwapsByOrdinal.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeCamshaft? GetCamshaftById(int id) =>
        _camshaftsByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeDisplacement? GetDisplacementById(int id) =>
        _displacementByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeValves? GetValvesById(int id) =>
        _valvesByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradePistons? GetPistonsById(int id) =>
        _pistonsByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeFuelSystem? GetFuelSystemById(int id) =>
        _fuelSystemsByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeIgnition? GetIgnitionById(int id) =>
        _ignitionsByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeExhaust? GetExhaustById(int id) =>
        _exhaustsByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeIntake? GetIntakeById(int id) =>
        _intakesByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeFlywheel? GetFlywheelById(int id) =>
        _flywheelsByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeManifold? GetManifoldById(int id) =>
        _manifoldsByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeOilCooling? GetOilCoolingById(int id) =>
        _oilCoolingsByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeRestrictor? GetRestrictorById(int id) =>
        _restrictorsByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradePart? GetForcedInductionById(int id) =>
        _turbosSingleByEngineId.Values.SelectMany(l => l).Cast<DbUpgradePart>().Concat(
        _turbosTwinByEngineId.Values.SelectMany(l => l)).Concat(
        _cscByEngineId.Values.SelectMany(l => l)).Concat(
        _dscByEngineId.Values.SelectMany(l => l)).FirstOrDefault(p => p.Id == id);
    public DbUpgradeIntercooler? GetIntercoolerById(int id) =>
        _intercoolersByEngineId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeTireCompound? GetTireCompoundById(int id) =>
        _tireCompoundsByOrdinal.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeSpringDamper? GetSpringDamperById(int id) =>
        _springDampersByOrdinal.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeBrakes? GetBrakesById(int id) =>
        _brakesByOrdinal.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeDifferential? GetDifferentialById(int id)
    {
        foreach (var list in _differentialsByDrivetrainId.Values)
        {
            if (list == null) continue;
            foreach (var item in list)
            {
                if (item != null && item.Id == id) return item;
            }
        }
        return null;
    }
    public DbUpgradeTransmission? GetTransmissionById(int id) =>
        _transmissionsByDrivetrainId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeClutch? GetClutchById(int id) =>
        _clutchesByDrivetrainId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeDriveline? GetDrivelineById(int id) =>
        _drivelinesByDrivetrainId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeRearWing? GetRearWingById(int id) =>
        _rearWingsByOrdinal.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeFrontBumper? GetFrontBumperById(int id) =>
        _frontBumpersByCarBodyId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeRearBumper? GetRearBumperById(int id) =>
        _rearBumpersByCarBodyId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeSideSkirt? GetSideSkirtById(int id) =>
        _sideSkirtsByCarBodyId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeAntiSwayFront? GetArbFrontById(int id) =>
        _antiSwayFrontByOrdinal.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeAntiSwayRear? GetArbRearById(int id) =>
        _antiSwayRearByOrdinal.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeTireWidthFront? GetTireWidthFrontById(int id) =>
        _tireWidthsFrontByCarBodyId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeTireWidthRear? GetTireWidthRearById(int id) =>
        _tireWidthsRearByCarBodyId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeRimFront? GetRimFrontById(int id) =>
        _rimsFrontByOrdinal.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeRimRear? GetRimRearById(int id) =>
        _rimsRearByOrdinal.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeTireAspectRatioFront? GetTireAspectRatioFrontById(int id) =>
        _tireAspectRatiosFrontByCarBodyId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeTireAspectRatioRear? GetTireAspectRatioRearById(int id) =>
        _tireAspectRatiosRearByCarBodyId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeTrackSpacingFront? GetTrackSpacingFrontById(int id) =>
        _trackSpacingsFrontByCarBodyId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeTrackSpacingRear? GetTrackSpacingRearById(int id) =>
        _trackSpacingsRearByCarBodyId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeMotorSwap? GetMotorSwapById(int id) =>
        _motorSwapsByOrdinal.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeMotorPart? GetMotorPartById(int id) =>
        _motorPartsByMotorId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeWeightReduction? GetWeightReductionById(int id) =>
        _weightReductionsByCarBodyId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
    public DbUpgradeChassisStiffness? GetChassisStiffnessById(int id) =>
        _chassisStiffnessByCarBodyId.Values.SelectMany(l => l).FirstOrDefault(p => p.Id == id);
}
