using System;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class UnitConverter
{
    internal static double TirePressureToDisplay(double bar, bool imperial) => imperial ? Math.Round(bar * 14.504, 1) : bar;
    internal static double TirePressureFromDisplay(double val, bool imperial) => imperial ? Math.Round(val / 14.504, 2) : val;

    // The game's kgf/mm spring readout is scaled x10 (e.g. 40-200 N/mm shows as ~41-204),
    // so the kgf/mm display multiplies the true kgf/mm value by 10 to match the in-game numbers.
    internal static double SpringToDisplay(double nmm, SpringUnit unit) => unit switch
    {
        SpringUnit.KgfMm => Math.Round(nmm / 9.807 * 10.0, 2),
        SpringUnit.LbsIn => Math.Round(nmm * 5.710, 1),
        _                => Math.Round(nmm, 1)
    };
    internal static double SpringFromDisplay(double val, SpringUnit unit) => unit switch
    {
        SpringUnit.KgfMm => val / 10.0 * 9.807,
        SpringUnit.LbsIn => val / 5.710,
        _                => val
    };

    internal static double RideHeightToDisplay(double mm, bool imperial) => imperial ? Math.Round(mm / 25.4, 1) : mm;
    internal static double RideHeightFromDisplay(double val, bool imperial) => imperial ? Math.Round(val * 25.4, 0) : val;

    internal static double SpeedToDisplay(double kmh, bool imperial) => imperial ? Math.Round(kmh * 0.6214, 1) : kmh;
    internal static double SpeedFromDisplay(double val, bool imperial) => imperial ? val / 0.6214 : val;

    internal static double MassToDisplay(double kg, bool imperial) => imperial ? Math.Round(kg * 2.2046, 1) : kg;
    internal static double MassFromDisplay(double val, bool imperial) => imperial ? val / 2.2046 : val;

    internal static double TorqueToDisplay(double nm, bool imperial) => imperial ? Math.Round(nm * 0.73756, 1) : nm;
    internal static double TorqueFromDisplay(double val, bool imperial) => imperial ? val / 0.73756 : val;

    internal static double LengthToDisplay(double mm, bool imperial) => imperial ? Math.Round(mm / 25.4, 1) : mm;
    internal static double LengthFromDisplay(double val, bool imperial) => imperial ? val * 25.4 : val;

    internal static double AeroToDisplay(double kgf, bool imperial) => imperial ? Math.Round(kgf * 2.2046, 0) : Math.Round(kgf, 0);
    internal static double AeroFromDisplay(double val, bool imperial) => imperial ? Math.Round(val / 2.2046, 0) : Math.Round(val, 0);

    internal static double PowerToDisplay(double hp, PowerUnit unit) => unit switch
    {
        PowerUnit.KW => Math.Round(hp * 0.7457, 1),
        PowerUnit.PS => Math.Round(hp * 1.01387, 1),
        _            => hp
    };
    internal static double PowerFromDisplay(double val, PowerUnit unit) => unit switch
    {
        PowerUnit.KW => val / 0.7457,
        PowerUnit.PS => val / 1.01387,
        _            => val
    };
}
