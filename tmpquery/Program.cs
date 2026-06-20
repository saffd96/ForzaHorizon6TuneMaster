using System;
using Microsoft.Data.Sqlite;
var db = "U:\\Forza Horizon 6 Tune Master\\DUMPER\\fh6_db.sqlite";
using var conn = new SqliteConnection($"Data Source={db};Mode=ReadOnly");
conn.Open();

void Q(string title, string sql)
{
    Console.WriteLine($"\n=== {title} ===");
    try
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        int cc = r.FieldCount;
        for (int i = 0; i < cc; i++) Console.Write($"{(i > 0 ? " | " : "")}{r.GetName(i),-28}");
        Console.WriteLine("\n" + new string('-', cc * 30));
        int rows = 0;
        while (r.Read())
        {
            for (int i = 0; i < cc; i++)
            {
                var v = r.IsDBNull(i) ? "NULL" : r.GetValue(i)?.ToString()?.TrimEnd();
                Console.Write($"{(i > 0 ? " | " : "")}{v,-28}");
            }
            Console.WriteLine();
            rows++;
            if (rows >= 50) { Console.WriteLine("... (truncated)"); break; }
        }
        Console.WriteLine($"({rows} rows)");
    }
    catch (Exception ex) { Console.WriteLine($"ERROR: {ex.Message}"); }
}

// Raw data for a few known cars
Q("Raw car data (first 10)", @"
SELECT Id, MediaName, Year, CurbWeight, PerformanceIndex,
       DriveTypeID, EnginePlacementID,
       [QuarterMileTime-sec], [QuarterMileSpeed-mph],
       [TopSpeed-mph], SimPeakPower, SimPeakTorque,
       MaxUpgradeWizardPerfRating, PI
FROM Data_Car WHERE IsDrivable=1 AND Id <= 10
");

// Known muscle/drag cars
Q("Known drag cars", @"
SELECT Id, MediaName, Year, CurbWeight, PerformanceIndex,
       DriveTypeID, EnginePlacementID,
       [QuarterMileTime-sec], [QuarterMileSpeed-mph],
       [TopSpeed-mph], SimPeakPower,
       MaxUpgradeWizardPerfRating
FROM Data_Car WHERE IsDrivable=1 AND MediaName LIKE '%Shelby%' OR MediaName LIKE '%GT500%' OR MediaName LIKE '%Hellcat%' OR MediaName LIKE '%Demon%' OR MediaName LIKE '%Challenger%' OR MediaName LIKE '%Viper%' OR MediaName LIKE '%GTR%' OR MediaName LIKE '%Supra%' OR MediaName LIKE '%Corvette%' OR MediaName LIKE '%Camaro%' OR MediaName LIKE '%Mustang%' OR MediaName LIKE '%Chiron%' OR MediaName LIKE '%Veyron%' OR MediaName LIKE '%Drag%'
LIMIT 30");

// Look at List_UpgradeTireCompound levels
Q("Tire compound upgrade levels",
  @"SELECT * FROM List_UpgradeTireCompound LIMIT 30");

// List_TireCompound - look for drag tires
Q("Tire compounds",
  @"SELECT TireCompoundID, DisplayName, IsOffroad, CompoundStiffness, TireRollResistance, TorqueFreeLongFrictionScaleAccel0, TorqueFreeLatFrictionScale FROM List_TireCompound ORDER BY TireCompoundID");

// Check what transmission levels map to what (drag transmissions)
Q("Transmission high levels",
  @"SELECT DrivetrainID, Level, IsStock, NumGears, FinalDriveRatio, GearRatio0, GearRatio1, GearRatio2
    FROM List_UpgradeDrivetrainTransmission WHERE Level >= 8 ORDER BY DrivetrainID, Level LIMIT 50");

// Look for specific drag parts in List_UpgradeSpringDamper
Q("Spring/Damper upgrades",
  @"SELECT * FROM List_UpgradeSpringDamper LIMIT 20");

// Look at weight reduction tables
Q("CarBodyWeight",
  @"SELECT * FROM List_UpgradeCarBodyWeight LIMIT 20");

// Top cars with quarter mile times (base game stats)
Q("Best production drag cars (with QM data)",
  @"SELECT Id, MediaName, Year, CurbWeight,
           ROUND([QuarterMileTime-sec],3) as QM_s,
           ROUND([QuarterMileSpeed-mph],0) as QM_mph,
           ROUND([Time:0-60-sec],3) as T060,
           ROUND([TopSpeed-mph],0) as TSpd,
           ROUND(SimPeakPower,0) as HP,
           DriveTypeID, EnginePlacementID,
           ROUND(PerformanceIndex,0) as PI
    FROM Data_Car 
    WHERE IsDrivable=1 AND [QuarterMileTime-sec] > 0 AND [QuarterMileTime-sec] < 20
    ORDER BY [QuarterMileTime-sec] ASC LIMIT 30");
