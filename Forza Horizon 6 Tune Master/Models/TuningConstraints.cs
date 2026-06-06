using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Forza_Horizon_6_Tune_Master.Models;

public class TuningConstraints : NotifyBase
{
    private void SetMinMax(ref double minField, double minValue, double maxValue, double maxField, [CallerMemberName] string? p = null)
        => Set(ref minField, Math.Min(minValue, maxField), p);

    private void SetMaxMin(ref double maxField, double maxValue, double minValue, double minField, [CallerMemberName] string? p = null)
        => Set(ref maxField, Math.Max(maxValue, minField), p);

    [JsonPropertyOrder(1)]
    public double TirePressureFrontMin { get => _tirePressureFrontMin; set => SetMinMax(ref _tirePressureFrontMin, value, TirePressureFrontMax, _tirePressureFrontMax); }
    private double _tirePressureFrontMin = 1.0;

    [JsonPropertyOrder(0)]
    public double TirePressureFrontMax { get => _tirePressureFrontMax; set => SetMaxMin(ref _tirePressureFrontMax, value, TirePressureFrontMin, _tirePressureFrontMin); }
    private double _tirePressureFrontMax = 3.8;

    [JsonPropertyOrder(1)]
    public double TirePressureRearMin { get => _tirePressureRearMin; set => SetMinMax(ref _tirePressureRearMin, value, TirePressureRearMax, _tirePressureRearMax); }
    private double _tirePressureRearMin = 1.0;

    [JsonPropertyOrder(0)]
    public double TirePressureRearMax { get => _tirePressureRearMax; set => SetMaxMin(ref _tirePressureRearMax, value, TirePressureRearMin, _tirePressureRearMin); }
    private double _tirePressureRearMax = 3.8;

    [JsonPropertyOrder(1)]
    public double CamberFrontMin { get => _camberFrontMin; set => SetMinMax(ref _camberFrontMin, value, CamberFrontMax, _camberFrontMax); }
    private double _camberFrontMin = -5.0;

    [JsonPropertyOrder(0)]
    public double CamberFrontMax { get => _camberFrontMax; set => SetMaxMin(ref _camberFrontMax, value, CamberFrontMin, _camberFrontMin); }
    private double _camberFrontMax = 5.0;

    [JsonPropertyOrder(1)]
    public double CamberRearMin { get => _camberRearMin; set => SetMinMax(ref _camberRearMin, value, CamberRearMax, _camberRearMax); }
    private double _camberRearMin = -5.0;

    [JsonPropertyOrder(0)]
    public double CamberRearMax { get => _camberRearMax; set => SetMaxMin(ref _camberRearMax, value, CamberRearMin, _camberRearMin); }
    private double _camberRearMax = 5.0;

    [JsonPropertyOrder(1)]
    public double ToeFrontMin { get => _toeFrontMin; set => SetMinMax(ref _toeFrontMin, value, ToeFrontMax, _toeFrontMax); }
    private double _toeFrontMin = -5.0;

    [JsonPropertyOrder(0)]
    public double ToeFrontMax { get => _toeFrontMax; set => SetMaxMin(ref _toeFrontMax, value, ToeFrontMin, _toeFrontMin); }
    private double _toeFrontMax = 5.0;

    [JsonPropertyOrder(1)]
    public double ToeRearMin { get => _toeRearMin; set => SetMinMax(ref _toeRearMin, value, ToeRearMax, _toeRearMax); }
    private double _toeRearMin = -5.0;

    [JsonPropertyOrder(0)]
    public double ToeRearMax { get => _toeRearMax; set => SetMaxMin(ref _toeRearMax, value, ToeRearMin, _toeRearMin); }
    private double _toeRearMax = 5.0;

    [JsonPropertyOrder(1)]
    public double CasterMin { get => _casterMin; set => SetMinMax(ref _casterMin, value, CasterMax, _casterMax); }
    private double _casterMin = 1.0;

    [JsonPropertyOrder(0)]
    public double CasterMax { get => _casterMax; set => SetMaxMin(ref _casterMax, value, CasterMin, _casterMin); }
    private double _casterMax = 7.0;

    [JsonPropertyOrder(1)]
    public double ARBFrontMin { get => _arbFrontMin; set => SetMinMax(ref _arbFrontMin, value, ARBFrontMax, _arbFrontMax); }
    private double _arbFrontMin = 1;

    [JsonPropertyOrder(0)]
    public double ARBFrontMax { get => _arbFrontMax; set => SetMaxMin(ref _arbFrontMax, value, ARBFrontMin, _arbFrontMin); }
    private double _arbFrontMax = 65;

    [JsonPropertyOrder(1)]
    public double ARBRearMin { get => _arbRearMin; set => SetMinMax(ref _arbRearMin, value, ARBRearMax, _arbRearMax); }
    private double _arbRearMin = 1;

    [JsonPropertyOrder(0)]
    public double ARBRearMax { get => _arbRearMax; set => SetMaxMin(ref _arbRearMax, value, ARBRearMin, _arbRearMin); }
    private double _arbRearMax = 65;

    [JsonPropertyOrder(1)]
    public double SpringFrontMin { get => _springFrontMin; set => SetMinMax(ref _springFrontMin, value, SpringFrontMax, _springFrontMax); }
    private double _springFrontMin = 57.1;

    [JsonPropertyOrder(0)]
    public double SpringFrontMax { get => _springFrontMax; set => SetMaxMin(ref _springFrontMax, value, SpringFrontMin, _springFrontMin); }
    private double _springFrontMax = 285.6;

    [JsonPropertyOrder(1)]
    public double SpringRearMin { get => _springRearMin; set => SetMinMax(ref _springRearMin, value, SpringRearMax, _springRearMax); }
    private double _springRearMin = 57.1;

    [JsonPropertyOrder(0)]
    public double SpringRearMax { get => _springRearMax; set => SetMaxMin(ref _springRearMax, value, SpringRearMin, _springRearMin); }
    private double _springRearMax = 285.6;

    [JsonPropertyOrder(1)]
    public double RideHeightFrontMin { get => _rideHeightFrontMin; set => SetMinMax(ref _rideHeightFrontMin, value, RideHeightFrontMax, _rideHeightFrontMax); }
    private double _rideHeightFrontMin = 50;

    [JsonPropertyOrder(0)]
    public double RideHeightFrontMax { get => _rideHeightFrontMax; set => SetMaxMin(ref _rideHeightFrontMax, value, RideHeightFrontMin, _rideHeightFrontMin); }
    private double _rideHeightFrontMax = 250;

    [JsonPropertyOrder(1)]
    public double RideHeightRearMin { get => _rideHeightRearMin; set => SetMinMax(ref _rideHeightRearMin, value, RideHeightRearMax, _rideHeightRearMax); }
    private double _rideHeightRearMin = 50;

    [JsonPropertyOrder(0)]
    public double RideHeightRearMax { get => _rideHeightRearMax; set => SetMaxMin(ref _rideHeightRearMax, value, RideHeightRearMin, _rideHeightRearMin); }
    private double _rideHeightRearMax = 250;

    [JsonPropertyOrder(1)]
    public double ReboundFrontMin { get => _reboundFrontMin; set => SetMinMax(ref _reboundFrontMin, value, ReboundFrontMax, _reboundFrontMax); }
    private double _reboundFrontMin = 1;

    [JsonPropertyOrder(0)]
    public double ReboundFrontMax { get => _reboundFrontMax; set => SetMaxMin(ref _reboundFrontMax, value, ReboundFrontMin, _reboundFrontMin); }
    private double _reboundFrontMax = 20;

    [JsonPropertyOrder(1)]
    public double ReboundRearMin { get => _reboundRearMin; set => SetMinMax(ref _reboundRearMin, value, ReboundRearMax, _reboundRearMax); }
    private double _reboundRearMin = 1;

    [JsonPropertyOrder(0)]
    public double ReboundRearMax { get => _reboundRearMax; set => SetMaxMin(ref _reboundRearMax, value, ReboundRearMin, _reboundRearMin); }
    private double _reboundRearMax = 20;

    [JsonPropertyOrder(1)]
    public double BumpFrontMin { get => _bumpFrontMin; set => SetMinMax(ref _bumpFrontMin, value, BumpFrontMax, _bumpFrontMax); }
    private double _bumpFrontMin = 1;

    [JsonPropertyOrder(0)]
    public double BumpFrontMax { get => _bumpFrontMax; set => SetMaxMin(ref _bumpFrontMax, value, BumpFrontMin, _bumpFrontMin); }
    private double _bumpFrontMax = 20;

    [JsonPropertyOrder(1)]
    public double BumpRearMin { get => _bumpRearMin; set => SetMinMax(ref _bumpRearMin, value, BumpRearMax, _bumpRearMax); }
    private double _bumpRearMin = 1;

    [JsonPropertyOrder(0)]
    public double BumpRearMax { get => _bumpRearMax; set => SetMaxMin(ref _bumpRearMax, value, BumpRearMin, _bumpRearMin); }
    private double _bumpRearMax = 20;

    [JsonPropertyOrder(1)]
    public double AeroFrontMin { get => _aeroFrontMin; set => SetMinMax(ref _aeroFrontMin, value, AeroFrontMax, _aeroFrontMax); }
    private double _aeroFrontMin = 78;

    [JsonPropertyOrder(0)]
    public double AeroFrontMax { get => _aeroFrontMax; set => SetMaxMin(ref _aeroFrontMax, value, AeroFrontMin, _aeroFrontMin); }
    private double _aeroFrontMax = 130;

    [JsonPropertyOrder(1)]
    public double AeroRearMin { get => _aeroRearMin; set => SetMinMax(ref _aeroRearMin, value, AeroRearMax, _aeroRearMax); }
    private double _aeroRearMin = 78;

    [JsonPropertyOrder(0)]
    public double AeroRearMax { get => _aeroRearMax; set => SetMaxMin(ref _aeroRearMax, value, AeroRearMin, _aeroRearMin); }
    private double _aeroRearMax = 130;

    [JsonPropertyOrder(1)]
    public double DiffAccelMin { get => _diffAccelMin; set => SetMinMax(ref _diffAccelMin, value, DiffAccelMax, _diffAccelMax); }
    private double _diffAccelMin;

    [JsonPropertyOrder(0)]
    public double DiffAccelMax { get => _diffAccelMax; set => SetMaxMin(ref _diffAccelMax, value, DiffAccelMin, _diffAccelMin); }
    private double _diffAccelMax = 100;

    [JsonPropertyOrder(1)]
    public double DiffDecelMin { get => _diffDecelMin; set => SetMinMax(ref _diffDecelMin, value, DiffDecelMax, _diffDecelMax); }
    private double _diffDecelMin;

    [JsonPropertyOrder(0)]
    public double DiffDecelMax { get => _diffDecelMax; set => SetMaxMin(ref _diffDecelMax, value, DiffDecelMin, _diffDecelMin); }
    private double _diffDecelMax = 100;

    public double CenterDiffBias { get => _centerDiffBias; set => Set(ref _centerDiffBias, value); }
    private double _centerDiffBias = 50;

    [JsonPropertyOrder(1)]
    public double BrakeBalanceMin { get => _brakeBalanceMin; set => SetMinMax(ref _brakeBalanceMin, value, BrakeBalanceMax, _brakeBalanceMax); }
    private double _brakeBalanceMin = 40;

    [JsonPropertyOrder(0)]
    public double BrakeBalanceMax { get => _brakeBalanceMax; set => SetMaxMin(ref _brakeBalanceMax, value, BrakeBalanceMin, _brakeBalanceMin); }
    private double _brakeBalanceMax = 70;

    [JsonPropertyOrder(1)]
    public double BrakePressureMin { get => _brakePressureMin; set => SetMinMax(ref _brakePressureMin, value, BrakePressureMax, _brakePressureMax); }
    private double _brakePressureMin = 50;

    [JsonPropertyOrder(0)]
    public double BrakePressureMax { get => _brakePressureMax; set => SetMaxMin(ref _brakePressureMax, value, BrakePressureMin, _brakePressureMin); }
    private double _brakePressureMax = 200;

    [JsonPropertyOrder(1)]
    public double FinalDriveMin { get => _finalDriveMin; set => SetMinMax(ref _finalDriveMin, Math.Max(value, 0.1), FinalDriveMax, _finalDriveMax); }
    private double _finalDriveMin = 2.2;

    [JsonPropertyOrder(0)]
    public double FinalDriveMax { get => _finalDriveMax; set => SetMaxMin(ref _finalDriveMax, value, FinalDriveMin, _finalDriveMin); }
    private double _finalDriveMax = 6.1;
}
