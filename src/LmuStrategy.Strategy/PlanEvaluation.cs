using System.Collections.Immutable;
using LmuStrategy.Domain;

namespace LmuStrategy.Strategy;

/// <summary>Signed modeled contribution and a nonnegative absolute uncertainty bound, in seconds.</summary>
public sealed record TimeEstimate(SecondsDelta Expected, Seconds Uncertainty, ModelEvidence Evidence);

/// <summary>
/// All terms are explicit, including modeled zeros. Cold outlap increments belong to the pit only.
/// These predictions exclude stochastic traffic/incidents from the clean baseline.
/// </summary>
public sealed record LapTimePrediction(TimeEstimate Baseline, TimeEstimate FuelMass,
    TimeEstimate Saving, TimeEstimate Tyres, TimeEstimate RoadWeather, TimeEstimate Other);

public sealed record StintPrediction(Sourced<FuelPerLapLitres> FuelPerLap,
    Sourced<VePerLapPercentagePoints> VePerLap, PerWheel<TyreState> TyreEnd,
    ModelEvidence TyreEvidence, ImmutableArray<LapTimePrediction> Laps);

/// <summary>Service additions are computed from arrival energy, never from an assumed empty tank.</summary>
public sealed record PitRequest(int AfterStint, Laps PitLap, FuelLitres FuelAdded,
    PercentagePoints VeAdded, FuelRatio FuelRatio, PerWheel<TyreReplacement?> Replacements);
public sealed record PitPrediction(TimeEstimate LaneLoss, TimeEstimate Service, TimeEstimate ColdOutlap);

/// <summary>
/// Pure deterministic prediction boundary shared by every candidate. Null means unsupported.
/// The race finish is an absolute completed-lap boundary excluding the requested buffer.
/// PredictStint must support the supplied load, ratio, saving intensity and per-corner tyres;
/// it must return null when those conditions are unsupported. No Fuel Ratio formula is assumed here.
/// </summary>
public interface IPlanPredictionModel
{
    Sourced<Laps>? ProjectRaceFinish(StrategyInput input);
    StintPrediction? PredictStint(StrategyInput input, Stint stint);
    PitPrediction? PredictPit(StrategyInput input, PitRequest request);
}

public enum ViolationCode
{
    Order, FinishBuffer, MissingCapability, MissingPrediction, Capacity, Energy,
    Continuity, TyreContinuity, IllegalCompound, IllegalService, Inventory, UnsupportedInput,
}

public sealed record PlanViolation(ViolationCode Code, string Path, string Message);
public enum ScoreComponent { Baseline, FuelMass, Saving, Tyres, RoadWeather, Other, PitLane, PitService, ColdOutlap }
public sealed record PlanScoreTerm(int StintIndex, Laps? Lap, ScoreComponent Component, TimeEstimate Estimate);

/// <summary>
/// Totals/delta are absent on invalid plans. Uncertainty is the conservative sum of component bounds,
/// not a statistical confidence interval. Score terms retain the original evidence for inspection.
/// </summary>
public sealed record PlanEvaluation(bool Valid, ImmutableArray<PlanViolation> Violations,
    Seconds? PredictedTotalTime, SecondsDelta? DeltaVsRecommended, ImmutableArray<PlanScoreTerm> Score,
    Seconds? Uncertainty, ConfidenceLevel Confidence, ImmutableArray<string> Warnings,
    ImmutableArray<DryTyreStock>? DryTyresRemaining, FuelLitres? FinishFuel, VePercent? FinishVe);
