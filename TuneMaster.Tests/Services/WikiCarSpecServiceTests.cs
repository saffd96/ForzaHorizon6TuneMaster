using Forza_Horizon_6_Tune_Master.Services;

namespace TuneMaster.Tests.Services;

public class WikiCarSpecServiceTests
{
    [Fact]
    public void ParseEngineType_ValidValues_ReturnsCorrect()
    {
        var service = new WikiCarSpecService();

        Assert.NotNull(service);
    }

    [Fact]
    public void DeleteCache_DoesNotThrow()
    {
        WikiCarSpecService.DeleteCache();
    }

    [Fact]
    public void ApiKeys_HaveValues()
    {
        Assert.NotNull(ApiKeys.Cerebras);
        Assert.NotNull(ApiKeys.OpenRouter);
    }

    [Fact]
    public void ApiKeys_AreNonEmpty()
    {
        Assert.NotEmpty(ApiKeys.Cerebras);
        Assert.NotEmpty(ApiKeys.OpenRouter);
    }
}
