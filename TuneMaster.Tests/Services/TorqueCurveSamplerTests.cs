using Forza_Horizon_6_Tune_Master.Services;
using Xunit;

namespace TuneMaster.Tests.Services;

public class TorqueCurveSamplerTests
{
    private const double MaxRpm = 8000;

    // 96-point curve rising to a peak then falling off. `falloffSharpness` controls how steeply
    // torque drops between the peak and redline — the crossover step should respond to that shape.
    private static double[] BuildCurve(double peakFractionOfMax, double falloffSharpness)
    {
        var curve = new double[96];
        int peakIdx = (int)(peakFractionOfMax * (curve.Length - 1));
        for (int i = 0; i < curve.Length; i++)
        {
            if (i <= peakIdx)
            {
                double t = peakIdx == 0 ? 1 : (double)i / peakIdx;
                curve[i] = 200 + 300 * t; // rises 200 -> 500
            }
            else
            {
                double t = (double)(i - peakIdx) / (curve.Length - 1 - peakIdx);
                curve[i] = 500 * (1 - falloffSharpness * t); // falls off after the peak
            }
        }
        return curve;
    }

    [Fact]
    public void SolveCrossoverStep_PeakyEngine_PrefersWiderStep()
    {
        // Sharp fall-off right up to redline (peaky turbo character).
        var peaky = BuildCurve(peakFractionOfMax: 0.55, falloffSharpness: 0.85);
        // Flat curve holding torque almost to redline (torquey/diesel character).
        var flat = BuildCurve(peakFractionOfMax: 0.55, falloffSharpness: 0.10);

        double shiftRpm = MaxRpm * 0.95;
        double stepMin = 0.68, stepMax = 0.86;

        double? peakyStep = TorqueCurveSampler.SolveCrossoverStep(peaky, MaxRpm, shiftRpm, stepMin, stepMax);
        double? flatStep = TorqueCurveSampler.SolveCrossoverStep(flat, MaxRpm, shiftRpm, stepMin, stepMax);

        Assert.NotNull(peakyStep);
        Assert.NotNull(flatStep);
        Assert.InRange(peakyStep!.Value, stepMin, stepMax);
        Assert.InRange(flatStep!.Value, stepMin, stepMax);
        Assert.True(peakyStep < flatStep,
            $"Peaky engine step ({peakyStep}) should be smaller (wider drop) than flat engine step ({flatStep})");
    }

    [Fact]
    public void SolveCrossoverStep_NoForceDipAtShiftPoint()
    {
        var curve = BuildCurve(peakFractionOfMax: 0.55, falloffSharpness: 0.85);
        double shiftRpm = MaxRpm * 0.95;
        double stepMin = 0.68, stepMax = 0.86;

        double? step = TorqueCurveSampler.SolveCrossoverStep(curve, MaxRpm, shiftRpm, stepMin, stepMax);
        Assert.NotNull(step);

        double torqueBeforeShift = TorqueCurveSampler.SampleTorqueNm(curve, MaxRpm, shiftRpm);
        double torqueAfterShift = TorqueCurveSampler.SampleTorqueNm(curve, MaxRpm, shiftRpm * step!.Value);
        double forceBefore = torqueBeforeShift; // ratioCurrent cancels in the comparison
        double forceAfter = torqueAfterShift * step.Value;

        // Either the crossover was found exactly inside the envelope (near-equal force), or the
        // ideal step was outside [stepMin, stepMax] and we're pinned to whichever bound gets
        // closest — either way, force after should not be far below force before.
        Assert.True(forceAfter >= forceBefore * 0.95,
            $"Force after shift ({forceAfter:F1}) dropped more than 5% below force before shift ({forceBefore:F1})");
    }

    [Theory]
    [InlineData(null, 8000, 7000, 0.6, 0.9)]
    [InlineData(new double[] { 1 }, 8000, 7000, 0.6, 0.9)] // too short
    public void SolveCrossoverStep_MissingCurve_ReturnsNull(double[]? curve, double maxRpm, double shiftRpm,
        double stepMin, double stepMax)
    {
        Assert.Null(TorqueCurveSampler.SolveCrossoverStep(curve, maxRpm, shiftRpm, stepMin, stepMax));
    }

    [Fact]
    public void SolveCrossoverStep_DegenerateEnvelope_ReturnsNull()
    {
        var curve = BuildCurve(0.55, 0.5);
        Assert.Null(TorqueCurveSampler.SolveCrossoverStep(curve, MaxRpm, MaxRpm * 0.95, 0.8, 0.8));
        Assert.Null(TorqueCurveSampler.SolveCrossoverStep(curve, MaxRpm, MaxRpm * 0.95, 0.9, 0.8));
    }

    [Fact]
    public void SampleTorqueNm_InterpolatesBetweenPoints()
    {
        var curve = new double[] { 0, 100, 200, 300 };
        // maxRpm=3000 -> points at 0, 1000, 2000, 3000 rpm
        Assert.Equal(100, TorqueCurveSampler.SampleTorqueNm(curve, 3000, 1000), 3);
        Assert.Equal(150, TorqueCurveSampler.SampleTorqueNm(curve, 3000, 1500), 3);
        Assert.Equal(0, TorqueCurveSampler.SampleTorqueNm(curve, 3000, -500), 3); // clamps to start
        Assert.Equal(300, TorqueCurveSampler.SampleTorqueNm(curve, 3000, 5000), 3); // clamps to end
    }
}
