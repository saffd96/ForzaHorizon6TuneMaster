using System;
using Microsoft.Data.Sqlite;
var db = "U:\\Forza Horizon 6 Tune Master\\DUMPER\\fh6_db.sqlite";
using var conn = new SqliteConnection($"Data Source={db};Mode=ReadOnly");
conn.Open();

// Which DrivetrainIDs have Level 11?
Console.WriteLine("=== Level 11 DrivetrainID distribution ===");
var cmd = conn.CreateCommand();
cmd.CommandText = "SELECT DrivetrainID, COUNT(*) FROM List_UpgradeDrivetrainTransmission WHERE Level=11 GROUP BY DrivetrainID ORDER BY DrivetrainID LIMIT 30;";
using (var r = cmd.ExecuteReader()) {
    while (r.Read()) {
        Console.WriteLine($"  DrivetrainID={r[0],5}  Count={r[1]}");
    }
}

// Total distinct DrivetrainIDs for Level 11
Console.WriteLine("\n=== Level 11 distinct DrivetrainIDs count ===");
var cmd2 = conn.CreateCommand();
cmd2.CommandText = "SELECT COUNT(DISTINCT DrivetrainID) FROM List_UpgradeDrivetrainTransmission WHERE Level=11;";
Console.WriteLine($"  {cmd2.ExecuteScalar()} distinct DrivetrainIDs");

// Also show distinct Level 6 DrivetrainIDs
Console.WriteLine("\n=== Level 6 distinct DrivetrainIDs count ===");
var cmd3 = conn.CreateCommand();
cmd3.CommandText = "SELECT COUNT(DISTINCT DrivetrainID) FROM List_UpgradeDrivetrainTransmission WHERE Level=6;";
Console.WriteLine($"  {cmd3.ExecuteScalar()} distinct DrivetrainIDs");

// Which levels have which IsStock?
Console.WriteLine("\n=== Every Level's majority IsStock ===");
var cmd4 = conn.CreateCommand();
cmd4.CommandText = "SELECT Level, IsStock, COUNT(*) as cnt FROM List_UpgradeDrivetrainTransmission GROUP BY Level, IsStock ORDER BY Level, cnt DESC;";
using (var r4 = cmd4.ExecuteReader()) {
    while (r4.Read()) {
        Console.WriteLine($"  Level={r4[0],2}  IsStock={r4[1],5}  Count={r4[2],5}");
    }
}
