using System.IO;
using Forza_Horizon_6_Tune_Master.Services;

namespace TuneMaster.Tests;

public sealed class TestingEnvironment : IDisposable
{
    public string RootPath { get; }

    private readonly IDisposable _rootSetter;

    public TestingEnvironment()
    {
        RootPath = Path.Combine(Path.GetTempPath(), "FH6Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(RootPath);
        _rootSetter = ForzaPaths.SetTestRoot(RootPath);
    }

    public void Dispose()
    {
        _rootSetter.Dispose();
        try { Directory.Delete(RootPath, recursive: true); } catch { }
    }
}
