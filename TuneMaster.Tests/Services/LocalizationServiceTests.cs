using Forza_Horizon_6_Tune_Master.Services;
using TuneMaster.Tests;

namespace TuneMaster.Tests.Services;

[Collection("FileSystem")]
public class LocalizationServiceTests : IDisposable
{
    private readonly TestingEnvironment _testEnv = new();

    public void Dispose()
    {
        _testEnv.Dispose();
    }
    [Fact]
    public void Instance_IsSingleton()
    {
        var a = LocalizationService.Instance;
        var b = LocalizationService.Instance;
        Assert.Same(a, b);
    }

    [Fact]
    public void T_ReturnsKey_WhenMissing()
    {
        var result = LocalizationService.Instance.T("NonExistentKey_XYZ");
        Assert.Equal("NonExistentKey_XYZ", result);
    }

    [Fact]
    public void T_ReturnsValue_ForKeyExists()
    {
        var result = LocalizationService.Instance.T("ProfileDefaultName");
        Assert.NotNull(result);
        Assert.NotEqual("ProfileDefaultName", result);
    }

    [Fact]
    public void T_WithArgs_FormatsCorrectly()
    {
        var result = LocalizationService.Instance.T("StatusTuneGenerated", "TestMake", "TestModel", "Road  •  12:00");
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void TryGet_ExistingKey_ReturnsTrue()
    {
        var found = LocalizationService.Instance.TryGet("ProfileDefaultName", out var value);
        Assert.True(found);
        Assert.NotNull(value);
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var found = LocalizationService.Instance.TryGet("NonExistentKey_XYZ", out var value);
        Assert.False(found);
        Assert.Equal("NonExistentKey_XYZ", value);
    }

    [Fact]
    public void Indexer_Works()
    {
        var svc = LocalizationService.Instance;
        var result = svc["ProfileDefaultName"];
        Assert.Equal(svc.T("ProfileDefaultName"), result);
    }

    [Fact]
    public void SetLanguage_ToEnglish_ReturnsTrue()
    {
        var result = LocalizationService.Instance.SetLanguage("en");
        Assert.True(result);
        Assert.Equal("en", LocalizationService.Instance.CurrentLanguage);
    }

    [Fact]
    public void SetLanguage_ToRussian_ReturnsTrue()
    {
        var result = LocalizationService.Instance.SetLanguage("ru");
        Assert.True(result);
        Assert.Equal("ru", LocalizationService.Instance.CurrentLanguage);
    }

    [Fact]
    public void SetLanguage_SameLanguage_ReturnsTrue()
    {
        LocalizationService.Instance.SetLanguage("en");
        var result = LocalizationService.Instance.SetLanguage("en");
        Assert.True(result);
    }

    [Fact]
    public void SetLanguage_ToInvalid_ReturnsFalse()
    {
        var result = LocalizationService.Instance.SetLanguage("xx");
        Assert.False(result);
    }

    [Fact]
    public void IsFallbackKey_ExistingInCurrent_ReturnsFalse()
    {
        LocalizationService.Instance.SetLanguage("ru");
        var result = LocalizationService.Instance.IsFallbackKey("ProfileDefaultName");
        Assert.False(result);
    }

    [Fact]
    public void T_Overload_WithArgs_Works()
    {
        var result = LocalizationService.Instance.T("StatusSpecsLoaded", "Toyota", "Supra");
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void LanguageLoadFailed_Event_FiresOnInvalid()
    {
        var svc = LocalizationService.Instance;
        var failed = false;
        svc.LanguageLoadFailed += (_, _) => failed = true;

        svc.SetLanguage("xx");

        Assert.True(failed);
    }
}
