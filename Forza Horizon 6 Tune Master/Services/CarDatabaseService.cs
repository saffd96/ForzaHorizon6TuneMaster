using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

// Projects the embedded SQLite car table (Fh6DatabaseService) into the UI's CarData list.
// The car list comes from the bundled DB, not from the network — the old web/cache load path
// (and its CarLoadResult.FromCache / WebErrorMessage flags) is gone.
public class CarDatabaseService
{
    public Task<List<CarData>> LoadCarDatabaseAsync()
    {
        var db = Fh6DatabaseService.Instance;
        var cars = db.GetAllCars()
            .Select(c =>
            {
                var makeName = db.GetReadableMakeName(c.MakeID);
                var manuCode = db.GetManufacturerCode(c.MakeID);
                var model = ResolveModelName(c.MediaName, manuCode);
                return new CarData
                {
                    Id = c.Id,
                    Year = c.Year,
                    Make = makeName,
                    Model = model,
                    PI = c.PI
                };
            })
            .ToList();

        return Task.FromResult(cars);
    }

    private static readonly Regex _camelSplit = new("(?<=[a-z])(?=[A-Z])", RegexOptions.Compiled);

    private static string ResolveModelName(string mediaName, string manuCode)
    {
        if (string.IsNullOrEmpty(mediaName)) return "";
        var prefix = manuCode + "_";
        var name = mediaName.StartsWith(prefix)
            ? mediaName.Substring(prefix.Length).Replace("_", " ")
            : mediaName.Replace("_", " ");
        return _camelSplit.Replace(name, " ");
    }
}
