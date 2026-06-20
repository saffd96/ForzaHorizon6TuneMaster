using System;
using Forza_Horizon_6_Tune_Master.Models;

namespace Forza_Horizon_6_Tune_Master.Services;

internal static class UnitConverter
{
    internal static double TirePressureToDisplay(double bar, bool imperial) => imperial ? Math.Round(bar * 14.504, 1) : bar;
    internal static double TirePressureFromDisplay(double val, bool imperial) => imperial ? Math.Round(val / 14.504, 2) : val;

    internal static double SpeedToDisplay(double kmh, bool imperial) => imperial ? Math.Round(kmh * 0.6214, 1) : kmh;

    internal static double MassToDisplay(double kg, bool imperial) => imperial ? Math.Round(kg * 2.2046, 1) : kg;
    internal static double MassFromDisplay(double val, bool imperial) => imperial ? val / 2.2046 : val;

    internal static double TorqueToDisplay(double nm, bool imperial) => imperial ? Math.Round(nm * 0.73756, 1) : nm;

    internal static double LengthToDisplay(double mm, bool imperial) => imperial ? Math.Round(mm / 25.4, 1) : mm;
    internal static double LengthFromDisplay(double val, bool imperial) => imperial ? val * 25.4 : val;

    internal static double PowerToDisplay(double hp, PowerUnit unit) => unit switch
    {
        PowerUnit.KW => Math.Round(hp * 0.7457, 1),
        PowerUnit.PS => Math.Round(hp * 1.01387, 1),
        _            => hp
    };
}
