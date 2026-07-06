# Physics-based gear ratio spacing (torque-curve crossover)

Status: approved for implementation
Date: 2026-07-06

## Problem

`GearingCalculator.BuildDisciplineRatios` currently builds the ratio ladder as a
smooth, position-interpolated ramp between a discipline's `stepMin` and
`stepMax` (e.g. Drag: 0.74 → 0.86). The step size has no relationship to the
car's actual torque curve — a peaky turbo engine and a flat-torque diesel get
the same shape of ratio ladder, just differing in `firstGear` height. The
only place the engine's power delivery is consulted at all is:

- `CalcRecommendedGearCount`'s `engineFactor`, a coarse ±12% nudge derived
  from `TorquePeakRPM / MaxRPM` (two scalar points, not the curve shape).
- `RpmDropFix`, a post-hoc patch that only intervenes when a shift would drop
  RPM more than ~35% below `PowerPeakRPM` — reactive, not the primary
  mechanism, and only catches the worst violations.

Every `CarCard` produced by `PowerCalculator.Calculate` already carries a full
96-point torque curve (`CachedTorqueCurveNm`, sampled evenly across
`0..MaxRPM`) — real per-engine shape data that the gearing code ignores
entirely.

## Goal

Make gear ratio spacing follow from the car's actual torque curve, using the
standard motorsport "equal wheel-force at the shift point" criterion, so a
shift neither loses tractive force nor wastes ratio spread. Discipline
character (first-gear height, and the `stepMin`/`stepMax` envelope) is
preserved as the outer bound within which the physics answer is clamped —
it stops being the *source* of the step and becomes a *guardrail* around it.

## Physics

Wheel force in a gear is proportional to `Torque(engineRPM) × gearRatio`
(final drive, tire radius, and drivetrain efficiency are shared constants
across gears and cancel out of the comparison). At the instant of an
upshift, wheel speed is continuous, so if the shift happens at engine RPM
`e1` going from ratio `r1` to `r2` (`r2 < r1`), the post-shift RPM is:

```
e2 = e1 × (r2 / r1)
```

Define the step `x = r2 / r1 ∈ (0, 1)`. "No force dip at the shift" means:

```
Torque(e1) × r1 = Torque(e2) × r2
Torque(e1)       = Torque(e1 × x) × x        (r1 cancels)
```

This is the crossover equation. It always has the trivial root `x = 1` (no
shift at all); the non-trivial root `x* < 1` is the widest step this engine
can take on this shift without the wheel force dropping below what it had
right before shifting:

- Steep torque fall-off approaching the shift point (peaky engine) → `x*`
  further from 1 (bigger RPM drop is safe, because torque recovers quickly
  as RPM falls back toward the torque peak).
- Flat curve near the shift point (torquey/diesel-like engine) → `x*` closer
  to 1 (torque doesn't recover on the way down, so only a small drop is
  safe).

`e1` (the assumed shift RPM) is the same absolute value for every shift in
this model — `car.MaxRPM × CalculationHelpers.RevLimitFraction`, matching the
`targetRpmFraction` already used elsewhere in `CalculateGearing`. Because
`e1` is constant and the curve doesn't change gear to gear, `x*` is a single
number per car (not per gear pair) — a uniform geometric ladder is the
physically "ideal" shape for this criterion. Real-world and in-game
constraints (minimum practical step, discipline feel, turbo-lag margin,
short-strip spacing) still apply — they clamp `x*` into `[stepMin, stepMax]`
exactly as those bounds are computed today (including
`ApplyAspirationStepAdjustment` / `ApplyDragDistanceSpacing`).

## Solving x*

`x*` is found by clamped bisection, restricted to the discipline's own
`[stepMin, stepMax]` envelope (no need to search outside it, since the
result is clamped there anyway):

1. Sample `Torque(e1)` and, for a candidate `x`, `Torque(e1 × x)` via linear
   interpolation over `CachedTorqueCurveNm` (96 points evenly spaced over
   `0..MaxRPM`).
2. Evaluate `g(x) = Torque(e1 × x) × x`.
3. If `g(stepMin) - Torque(e1)` and `g(stepMax) - Torque(e1)` have the same
   sign, no crossover exists inside the envelope — clamp to whichever bound
   minimizes `|g(x) - Torque(e1)|`.
4. Otherwise bisect for the sign change (fixed iteration cap, e.g. 40, or
   until the bracket width is below `1e-4`).

## Where it lives

New file `Services/TorqueCurveSampler.cs`, two pure, independently testable
functions:

```csharp
internal static class TorqueCurveSampler
{
    internal static double SampleTorqueNm(double[]? curve, double maxRpm, double rpm);
    internal static double? SolveCrossoverStep(double[]? curve, double maxRpm, double shiftRpm,
        double stepMin, double stepMax);
}
```

`SolveCrossoverStep` returns `null` when it can't produce a physically
meaningful answer (curve missing/too short, `maxRpm <= 0`, or
`stepMin >= stepMax`) — the caller falls back to today's behavior, it never
throws.

## Integration point

`GearingCalculator.BuildDisciplineRatios` gets one new optional parameter:

```csharp
internal static List<double> BuildDisciplineRatios(double first, double stepMin, double stepMax,
    int count, double topFloor = 0, double topCeiling = 0, double? resolvedStep = null)
```

- When `resolvedStep` is provided, every step in the raw ramp uses that
  constant value (replacing the current `stepMin + (stepMax-stepMin) * t`
  position interpolation).
- When `resolvedStep` is `null` (no curve data — e.g. hand-built `CarCard`s
  in unit tests that never ran `PowerCalculator.Calculate`), the existing
  position-interpolated ramp is used unchanged. This is the fallback path,
  not a new feature — it's what keeps every existing test passing.
- The log-space re-fit to the FD-reachable top-gear window (already in
  `BuildDisciplineRatios`) is untouched — it only perturbs the ladder when
  the physically-ideal ladder doesn't reach the required top gear (fixed
  gear count vs. required ratio spread), which is itself a real, unavoidable
  constraint (a 5-speed box on a very peaky engine *will* have some
  shift compromise — that's not a bug to hide).
- `CalculateGearing` computes `resolvedStep` right after `stepMin`/`stepMax`
  are finalized (post aspiration/drag-distance adjustment) and passes it
  through. `RpmDropFix` and `PostValidateAndRecalculate` are untouched —
  they remain the safety net for whatever the final-drive-fitting pass or
  a capped gear count still can't avoid.

## Secondary change: `CalcRecommendedGearCount`

Today: `band = Clamp(bandDisc × engineFactor, 1.28, 1.62)`, where
`engineFactor` is the coarse ±12% correction from `TorquePeakRPM/MaxRPM`.

Replacement: call `TorqueCurveSampler.SolveCrossoverStep(car.CachedTorqueCurveNm,
car.MaxRPM, rpmShift, stepMin, stepMax)` — using the same `rpmShift` (`=
Math.Max(car.MaxRPM * RevLimitFraction, 1000.0)`) already computed in this
function, *not* the redline-capped `rpmForFirst` used later just for the
first-gear top-speed estimate. If it returns a value `x*`, set
`band = Clamp(1.0 / x*, 1.28, 1.62)` — same sanity bounds as today, just a
physically-derived source instead of the two-point heuristic. If it returns
`null`, keep today's `engineFactor` computation unchanged.

This makes gear-count and gear-spacing agree with each other (one physical
answer instead of two independent heuristics that could disagree). It is a
smaller, separable change — lands as its own commit after the primary
spacing change is verified.

## Out of scope

- `LaunchControlCalculator`'s `baseLaunch` heuristic (peak-RPM-fraction based)
  is not touched — a separate concern, not raised as broken during
  investigation of the launch-control question earlier in this session.
- No change to how `TorquePeakRPM`/`PowerPeakRPM` are derived, or to
  `PowerCalculator` curve generation.
- No change to discipline `firstGear` / `stepMin` / `stepMax` base values.
- No UI changes.

## Testing plan

- **Unit — `TorqueCurveSampler`**: synthetic curves (a sharp-falloff "peaky"
  shape and a flat "torquey" shape) confirm `SolveCrossoverStep` returns a
  step closer to `stepMin` for the peaky curve and closer to `stepMax` for
  the flat one, and returns `null` gracefully for missing/degenerate input.
- **Unit — `BuildDisciplineRatios`**: with `resolvedStep` supplied, every
  raw-ramp step equals that value (before the top-gear re-fit); with
  `resolvedStep` omitted, behavior is byte-for-byte the same as today.
- **Integration — real DB cars**: for a spread of real cars (reuse the
  probing approach from this session — peaky turbo, flat-torque, diesel,
  drag/road/drift disciplines), assert the post-upshift RPM at every gear
  stays at or above `TorquePeakRPM` (the original complaint) noticeably more
  often / by a wider margin than today, without `RpmDropFix` having to
  intervene.
- **Regression**: existing `GearingCalculator`/`DbIntegrationCalculatorTests`
  assertions (strictly descending ratios, non-empty, FD within
  `[FinalDriveMin, FinalDriveMax]`, `Gearing_DisciplineParams_AllUnique`,
  diesel first-gear, aspiration step adjustment) must keep passing unchanged,
  since `GetDisciplineGearParams` itself is not modified.
