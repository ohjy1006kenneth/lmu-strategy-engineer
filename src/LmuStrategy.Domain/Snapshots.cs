using System.Collections.Immutable;

namespace LmuStrategy.Domain;

/// <summary>Null constraint wrappers mean unavailable; supplied empty lists mean known empty.</summary>
public sealed record SessionConstraints(
    Sourced<FuelLitres>? FuelCapacity,
    Sourced<VePercent>? VeCapacity,
    Sourced<ImmutableArray<TyreCompound>>? LegalCompounds,
    Sourced<ImmutableArray<DryTyreStock>>? AvailableDryTyres,
    Sourced<ImmutableArray<TyreServicePattern>>? LegalTyreServices);

public sealed record RaceIdentity(string SessionId, string DriverId, string CarId, string ClassId, string TrackId, string LayoutId);
public enum RaceControlState { Unknown, Green, Yellow, SlowZone, SafetyCar, Formation, Finished }
public enum RoadConditionFamily { Unknown, Dry, Damp, Wet }
public sealed record WeatherState(RoadConditionFamily RoadCondition, Fraction? RainIntensity,
    Fraction? RoadWetness, Celsius? AirTemperature, Celsius? TrackTemperature);
public sealed record ForecastPoint(Seconds FromNow, WeatherState Weather, ModelEvidence Evidence);
public sealed record Forecast(ImmutableArray<ForecastPoint> Points);

public sealed record RaceState(RaceIdentity Identity, Laps CompletedLaps, Seconds ElapsedTime,
    Seconds RemainingTime, RaceControlState RaceControl, WeatherState Weather);
public sealed record VehicleState(FuelLitres? Fuel, VePercent? Ve, FuelRatio? FuelRatio, PerWheel<TyreState> Tyres);

/// <summary>Fuel/VE models can omit compound; tyre models supply one. Dry/Wet stay separate.</summary>
public sealed record DriverModelKey(string DriverId, string CarId, string TrackId, string LayoutId,
    RoadConditionFamily RoadCondition, TyreCompound? Compound);
public sealed record DriverModelSnapshot(DriverModelKey Key,
    Sourced<FuelPerLapLitres>? FuelPerLap,
    Sourced<VePerLapPercentagePoints>? VePerLap,
    Sourced<Seconds>? RepresentativeLapTime,
    Sourced<PerWheel<TyreWearPerLapPercentagePoints>>? TyreWearPerLap);

/// <summary>A contextual estimate, not a service-time formula. Null/no samples means no estimate.</summary>
public sealed record PitServiceEstimate(FuelLitres FuelAdded, PercentagePoints VeAdded, FuelRatio FuelRatio,
    PerWheel<TyreReplacement?> Replacements, Seconds LaneLoss, Seconds ServiceTime,
    Seconds ColdOutlapTime, ModelEvidence Evidence);
public sealed record PitServiceModel(ImmutableArray<PitServiceEstimate> Estimates);

public enum ModelDataSelection { Qualifying, RecentCleanLaps }
/// <summary>Null StartCompound means Auto; null RecentCleanLapCount means not selected/unknown.</summary>
public sealed record UserPreferences(Laps BufferLaps, FuelLitres FuelReserve, PercentagePoints VeReserve,
    ModelDataSelection DataSelection, Laps? RecentCleanLapCount, TyreCompound? StartCompound);

public sealed record StrategyInput(SessionConstraints SessionConstraints, RaceState RaceState,
    VehicleState VehicleState, Forecast Forecast, ImmutableArray<DriverModelSnapshot> DriverModels,
    PitServiceModel PitServiceModel, LmuCapabilities LmuCapabilities, UserPreferences UserPreferences,
    RacePlan? LoadedPlan, RacePlan? UserEditedPlan);
