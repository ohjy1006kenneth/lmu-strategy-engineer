using System.Text.Json.Serialization;

namespace LmuStrategy.Domain;

/// <summary>Physical fuel in litres.</summary>
public readonly record struct FuelLitres
{
    public double Value { get; }

    [JsonConstructor]
    public FuelLitres(double value) => Value = UnitGuard.NonNegative(value, nameof(value));
}

/// <summary>A nonnegative duration in seconds.</summary>
public readonly record struct Seconds
{
    public double Value { get; }

    [JsonConstructor]
    public Seconds(double value) => Value = UnitGuard.NonNegative(value, nameof(value));
}

/// <summary>A signed time difference in seconds; negative means less time.</summary>
public readonly record struct SecondsDelta
{
    public double Value { get; }

    [JsonConstructor]
    public SecondsDelta(double value) => Value = UnitGuard.Finite(value, nameof(value));
}

/// <summary>Virtual Energy level, 0 to 100 percent.</summary>
public readonly record struct VePercent
{
    public double Value { get; }

    [JsonConstructor]
    public VePercent(double value) => Value = UnitGuard.Percent(value, nameof(value));
}

/// <summary>Remaining tyre life, 0 to 100 percent.</summary>
public readonly record struct TyreRemainingPercent
{
    public double Value { get; }

    [JsonConstructor]
    public TyreRemainingPercent(double value) => Value = UnitGuard.Percent(value, nameof(value));
}

/// <summary>A signed absolute percentage-point change, never a fraction or level.</summary>
public readonly record struct PercentagePoints
{
    public double Value { get; }

    [JsonConstructor]
    public PercentagePoints(double value) => Value = UnitGuard.Finite(value, nameof(value));
}

/// <summary>Fuel consumption in litres per lap.</summary>
public readonly record struct FuelPerLapLitres
{
    public double Value { get; }

    [JsonConstructor]
    public FuelPerLapLitres(double value) => Value = UnitGuard.NonNegative(value, nameof(value));
}

/// <summary>VE consumption in percentage points per lap.</summary>
public readonly record struct VePerLapPercentagePoints
{
    public double Value { get; }

    [JsonConstructor]
    public VePerLapPercentagePoints(double value) => Value = UnitGuard.NonNegative(value, nameof(value));
}

/// <summary>Tyre life consumed in percentage points per lap.</summary>
public readonly record struct TyreWearPerLapPercentagePoints
{
    public double Value { get; }

    [JsonConstructor]
    public TyreWearPerLapPercentagePoints(double value) => Value = UnitGuard.NonNegative(value, nameof(value));
}

/// <summary>A normalized fraction from 0 to 1, e.g. saving intensity or road wetness.</summary>
public readonly record struct Fraction
{
    public double Value { get; }

    [JsonConstructor]
    public Fraction(double value) => Value = UnitGuard.Fraction(value, nameof(value));
}

/// <summary>An explicit dimensionless Fuel Ratio. Its LMU mapping is not defined here.</summary>
public readonly record struct FuelRatio
{
    public double Value { get; }

    [JsonConstructor]
    public FuelRatio(double value) => Value = UnitGuard.NonNegative(value, nameof(value));
}

/// <summary>Temperature in degrees Celsius.</summary>
public readonly record struct Celsius
{
    public double Value { get; }

    [JsonConstructor]
    public Celsius(double value) => Value = UnitGuard.Finite(value, nameof(value));
}

/// <summary>A nonnegative number of laps, also used for completed-lap boundaries.</summary>
public readonly record struct Laps
{
    public int Value { get; }

    [JsonConstructor]
    public Laps(int value) => Value = UnitGuard.Count(value, nameof(value));
}

/// <summary>A nonnegative count of individual tyres, never sets.</summary>
public readonly record struct TyreCount
{
    public int Value { get; }

    [JsonConstructor]
    public TyreCount(int value) => Value = UnitGuard.Count(value, nameof(value));
}

internal static class UnitGuard
{
    internal static double Finite(double value, string name) => double.IsFinite(value)
        ? value : throw new ArgumentOutOfRangeException(name, "Must be finite.");

    internal static double NonNegative(double value, string name) => Finite(value, name) >= 0
        ? value : throw new ArgumentOutOfRangeException(name, "Must be nonnegative.");

    internal static double Percent(double value, string name) => NonNegative(value, name) <= 100
        ? value : throw new ArgumentOutOfRangeException(name, "Must be at most 100 percent.");

    internal static double Fraction(double value, string name) => NonNegative(value, name) <= 1
        ? value : throw new ArgumentOutOfRangeException(name, "Must be at most one.");

    internal static int Count(int value, string name) => value >= 0
        ? value : throw new ArgumentOutOfRangeException(name, "Must be nonnegative.");
}
