using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace LmuStrategy.Domain;

public enum StrategyMode { FlatOut, FuelSave }

/// <summary>
/// Index is zero based. Lap boundaries count completed laps: StartLap=0, EndLap=10 covers laps 1..10.
/// Ordered boundaries and energy/inventory feasibility are checked by the future plan evaluator.
/// </summary>
public sealed record Stint(int Index, Laps StartLap, Laps EndLap, FuelLitres StartFuel, VePercent StartVe,
    FuelRatio FuelRatio, FuelPerLapLitres TargetFuelPerLap, VePerLapPercentagePoints TargetVePerLap,
    Fraction SavingIntensity, PerWheel<TyreState> TyreStart, PerWheel<TyreState> TyreEnd, Seconds PredictedDrivingTime)
{
    [JsonIgnore]
    public Laps LapCount => new(EndLap.Value - StartLap.Value);
}

/// <summary>AfterStint is a zero-based index; PitLap is the completed-lap boundary before service.</summary>
public sealed record Pit(int AfterStint, Laps PitLap, FuelLitres TargetFuel, VePercent TargetVe,
    FuelRatio FuelRatio, PerWheel<TyreReplacement?> Replacements, Seconds LaneLoss, Seconds ServiceTime,
    Seconds ColdOutlapTime, TyreCount DryTyresConsumed, ModelEvidence Evidence)
{
    [JsonIgnore]
    public Seconds TotalPitCost => new(LaneLoss.Value + ServiceTime.Value + ColdOutlapTime.Value);
}

/// <summary>Immutable collections support zero/one/many stints or stops, including edited candidates.</summary>
public sealed record RacePlan(StrategyMode StrategyMode, ImmutableArray<Stint> Stints, ImmutableArray<Pit> Pits,
    ImmutableArray<DryTyreStock>? DryTyresRemaining, FuelLitres FinishFuel, VePercent FinishVe, ModelEvidence Evidence)
{
    [JsonIgnore]
    public Seconds PredictedDrivingTime => new(Stints.Sum(stint => stint.PredictedDrivingTime.Value));
    [JsonIgnore]
    public Seconds PredictedPitTime => new(Pits.Sum(pit => pit.TotalPitCost.Value));
    [JsonIgnore]
    public Seconds PredictedTotalTime => new(PredictedDrivingTime.Value + PredictedPitTime.Value);
}

public enum StrategyStatus { Unavailable, Success, Infeasible }
public enum MaterialChangeKind { PitLap, StopCount, TyreAction, Compound, WeatherCrossover, EnergyTargets, RaceDistance, IncidentRecovery }
public sealed record MaterialChange(ImmutableArray<MaterialChangeKind> Reasons, string Explanation, SecondsDelta? PredictedTimeDelta);

/// <summary>No candidate/projection is fabricated when a solve is unavailable or infeasible.</summary>
public sealed record StrategyResult(StrategyStatus Status, RacePlan? FlatOut, RacePlan? FuelSave,
    RacePlan? RecommendedPlan, Laps? PredictedRaceLaps, Laps? StrategyDistanceLaps,
    ModelEvidence ModelConfidence, ImmutableArray<string> Warnings, MaterialChange? MaterialChange);
