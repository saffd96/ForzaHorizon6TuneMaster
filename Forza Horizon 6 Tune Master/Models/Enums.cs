namespace Forza_Horizon_6_Tune_Master.Models;

public enum Discipline
{
    Road,          // Шоссейные гонки
    Touge,         // Тогэ
    Rally,         // Ралли
    CrossCountry,  // Кросс-кантри
    Drift,         // Дрифт
    Drag,          // Драг-рейсинг
    Eliminator,    // Вышибала
    Street         // Уличные гонки
}

public enum DragDistance { Eighth, Quarter, Half, Mile }

public enum DriveType { FWD, RWD, AWD }

public enum EngineType { I1, I2, I3, I4, I5, I6, I8, Boxer, V6, V8, V10, V12, W12, Rotary }

public enum EnginePosition { Front, Mid, RearMid, Rear }

public enum TireType { Stock, Street, Sport, SemiSlick, Slick, Rally, Offroad, Drag, Winter }

public enum SuspensionUpgrade { Stock, Street, Sport, Race, Rally, Drift, Offroad }

public enum DifferentialUpgrade { Stock, Street, Sport, Rally, Race, DriftSpec, Offroad }

public enum BrakesUpgrade { Stock, Street, Sport, Race }

public enum AspirationType
{
    Natural,               // Атмосферный — линейная тяга
    SingleTurbo,           // Одиночная турбина — яма на низах, пик в середине
    TwinTurbo,             // Двойной турбо — мощно и ровнее одиночной
    PositiveDisplacement,  // Объёмный компрессор — мгновенный отклик, линейный
    Centrifugal,           // Центробежный компрессор — нарастает с оборотами
    Electric               // Электро — мгновенный максимальный момент с 0 об/мин
}

public enum PowertrainType
{
    ICE,      // Традиционный ДВС
    Hybrid,   // Параллельный гибрид (ДВС + электромотор)
    Electric  // Чистый электромобиль (BEV)
}

public enum UnitSystem { Metric, Imperial }

public enum PowerUnit { HP, PS, KW }

public enum SpringUnit { KgfMm, NMm, LbsIn }

public enum FuelType { Gasoline, Diesel }
