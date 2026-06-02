namespace Forza_Horizon_6_Tune_Master.Models;

public class TuningConstraints : NotifyBase
{
    private double _tirePressureFrontMin = 1.0;
    public double TirePressureFrontMin { get => _tirePressureFrontMin; set => Set(ref _tirePressureFrontMin, value); }

    private double _tirePressureFrontMax = 3.8;
    public double TirePressureFrontMax { get => _tirePressureFrontMax; set => Set(ref _tirePressureFrontMax, value); }

    private double _tirePressureRearMin = 1.0;
    public double TirePressureRearMin { get => _tirePressureRearMin; set => Set(ref _tirePressureRearMin, value); }

    private double _tirePressureRearMax = 3.8;
    public double TirePressureRearMax { get => _tirePressureRearMax; set => Set(ref _tirePressureRearMax, value); }

    private double _camberFrontMin = -5.0;
    public double CamberFrontMin { get => _camberFrontMin; set => Set(ref _camberFrontMin, value); }

    private double _camberFrontMax = 5.0;
    public double CamberFrontMax { get => _camberFrontMax; set => Set(ref _camberFrontMax, value); }

    private double _camberRearMin = -5.0;
    public double CamberRearMin { get => _camberRearMin; set => Set(ref _camberRearMin, value); }

    private double _camberRearMax = 5.0;
    public double CamberRearMax { get => _camberRearMax; set => Set(ref _camberRearMax, value); }

    private double _toeFrontMin = -5.0;
    public double ToeFrontMin { get => _toeFrontMin; set => Set(ref _toeFrontMin, value); }

    private double _toeFrontMax = 5.0;
    public double ToeFrontMax { get => _toeFrontMax; set => Set(ref _toeFrontMax, value); }

    private double _toeRearMin = -5.0;
    public double ToeRearMin { get => _toeRearMin; set => Set(ref _toeRearMin, value); }

    private double _toeRearMax = 5.0;
    public double ToeRearMax { get => _toeRearMax; set => Set(ref _toeRearMax, value); }

    private double _casterMin = 1.0;
    public double CasterMin { get => _casterMin; set => Set(ref _casterMin, value); }

    private double _casterMax = 7.0;
    public double CasterMax { get => _casterMax; set => Set(ref _casterMax, value); }

    private double _arbFrontMin = 1;
    public double ARBFrontMin { get => _arbFrontMin; set => Set(ref _arbFrontMin, value); }

    private double _arbFrontMax = 65;
    public double ARBFrontMax { get => _arbFrontMax; set => Set(ref _arbFrontMax, value); }

    private double _arbRearMin = 1;
    public double ARBRearMin { get => _arbRearMin; set => Set(ref _arbRearMin, value); }

    private double _arbRearMax = 65;
    public double ARBRearMax { get => _arbRearMax; set => Set(ref _arbRearMax, value); }

    private double _springFrontMin = 57.1;
    public double SpringFrontMin { get => _springFrontMin; set => Set(ref _springFrontMin, value); }

    private double _springFrontMax = 285.6;
    public double SpringFrontMax { get => _springFrontMax; set => Set(ref _springFrontMax, value); }

    private double _springRearMin = 57.1;
    public double SpringRearMin { get => _springRearMin; set => Set(ref _springRearMin, value); }

    private double _springRearMax = 285.6;
    public double SpringRearMax { get => _springRearMax; set => Set(ref _springRearMax, value); }

    private double _rideHeightFrontMin = 50;
    public double RideHeightFrontMin { get => _rideHeightFrontMin; set => Set(ref _rideHeightFrontMin, value); }

    private double _rideHeightFrontMax = 250;
    public double RideHeightFrontMax { get => _rideHeightFrontMax; set => Set(ref _rideHeightFrontMax, value); }

    private double _rideHeightRearMin = 50;
    public double RideHeightRearMin { get => _rideHeightRearMin; set => Set(ref _rideHeightRearMin, value); }

    private double _rideHeightRearMax = 250;
    public double RideHeightRearMax { get => _rideHeightRearMax; set => Set(ref _rideHeightRearMax, value); }

    private double _reboundFrontMin = 1;
    public double ReboundFrontMin { get => _reboundFrontMin; set => Set(ref _reboundFrontMin, value); }

    private double _reboundFrontMax = 20;
    public double ReboundFrontMax { get => _reboundFrontMax; set => Set(ref _reboundFrontMax, value); }

    private double _reboundRearMin = 1;
    public double ReboundRearMin { get => _reboundRearMin; set => Set(ref _reboundRearMin, value); }

    private double _reboundRearMax = 20;
    public double ReboundRearMax { get => _reboundRearMax; set => Set(ref _reboundRearMax, value); }

    private double _bumpFrontMin = 1;
    public double BumpFrontMin { get => _bumpFrontMin; set => Set(ref _bumpFrontMin, value); }

    private double _bumpFrontMax = 20;
    public double BumpFrontMax { get => _bumpFrontMax; set => Set(ref _bumpFrontMax, value); }

    private double _bumpRearMin = 1;
    public double BumpRearMin { get => _bumpRearMin; set => Set(ref _bumpRearMin, value); }

    private double _bumpRearMax = 20;
    public double BumpRearMax { get => _bumpRearMax; set => Set(ref _bumpRearMax, value); }

    private double _aeroFrontMin = 78;
    public double AeroFrontMin { get => _aeroFrontMin; set => Set(ref _aeroFrontMin, value); }

    private double _aeroFrontMax = 130;
    public double AeroFrontMax { get => _aeroFrontMax; set => Set(ref _aeroFrontMax, value); }

    private double _aeroRearMin = 78;
    public double AeroRearMin { get => _aeroRearMin; set => Set(ref _aeroRearMin, value); }

    private double _aeroRearMax = 130;
    public double AeroRearMax { get => _aeroRearMax; set => Set(ref _aeroRearMax, value); }

    private double _diffAccelMin;
    public double DiffAccelMin { get => _diffAccelMin; set => Set(ref _diffAccelMin, value); }

    private double _diffAccelMax = 100;
    public double DiffAccelMax { get => _diffAccelMax; set => Set(ref _diffAccelMax, value); }

    private double _diffDecelMin;
    public double DiffDecelMin { get => _diffDecelMin; set => Set(ref _diffDecelMin, value); }

    private double _diffDecelMax = 100;
    public double DiffDecelMax { get => _diffDecelMax; set => Set(ref _diffDecelMax, value); }

    private double _centerDiffBias = 50;
    public double CenterDiffBias { get => _centerDiffBias; set => Set(ref _centerDiffBias, value); }

    private double _brakeBalanceMin = 40;
    public double BrakeBalanceMin { get => _brakeBalanceMin; set => Set(ref _brakeBalanceMin, value); }

    private double _brakeBalanceMax = 70;
    public double BrakeBalanceMax { get => _brakeBalanceMax; set => Set(ref _brakeBalanceMax, value); }

    private double _brakePressureMin = 50;
    public double BrakePressureMin { get => _brakePressureMin; set => Set(ref _brakePressureMin, value); }

    private double _brakePressureMax = 200;
    public double BrakePressureMax { get => _brakePressureMax; set => Set(ref _brakePressureMax, value); }

    private double _finalDriveMin = 2.2;
    public double FinalDriveMin { get => _finalDriveMin; set => Set(ref _finalDriveMin, value); }

    private double _finalDriveMax = 6.1;
    public double FinalDriveMax { get => _finalDriveMax; set => Set(ref _finalDriveMax, value); }
}
