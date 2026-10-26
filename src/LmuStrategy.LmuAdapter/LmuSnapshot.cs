using System.Collections.Immutable;
using LmuStrategy.Domain;

namespace LmuStrategy.LmuAdapter;

public enum ConnectionStatus { Disconnected, Connected }

/// <summary>SessionId identifies a session instance, not a reusable track/event name.</summary>
public sealed record SnapshotContext(IntegrationIdentity Identity, string SessionId, DateTimeOffset CapturedAt);

public sealed record SessionSnapshot
{
    public SnapshotValue<RaceIdentity> Identity { get; init; } = SnapshotValue<RaceIdentity>.Unavailable();
    public SnapshotValue<Seconds> Duration { get; init; } = SnapshotValue<Seconds>.Unavailable();
    public SnapshotValue<Seconds> ElapsedTime { get; init; } = SnapshotValue<Seconds>.Unavailable();
    public SnapshotValue<Seconds> RemainingTime { get; init; } = SnapshotValue<Seconds>.Unavailable();
    public SnapshotValue<Laps> CompletedLaps { get; init; } = SnapshotValue<Laps>.Unavailable();
    public SnapshotValue<RaceControlState> RaceControl { get; init; } = SnapshotValue<RaceControlState>.Unavailable();
    public SnapshotValue<WeatherState> Weather { get; init; } = SnapshotValue<WeatherState>.Unavailable();

    internal SessionSnapshot Validate(SnapshotContext context, string? failure) => new()
    {
        Identity = Identity.Validate(context, true, failure)
            .Require(identity => identity.SessionId == context.SessionId, "Session identity contradicts snapshot context"),
        Duration = Duration.Validate(context, true, failure),
        ElapsedTime = ElapsedTime.Validate(context, false, failure),
        RemainingTime = RemainingTime.Validate(context, false, failure),
        CompletedLaps = CompletedLaps.Validate(context, false, failure),
        RaceControl = RaceControl.Validate(context, false, failure),
        Weather = Weather.Validate(context, false, failure),
    };
}

public sealed record VehicleSnapshot
{
    public SnapshotValue<FuelLitres> FuelCapacity { get; init; } = SnapshotValue<FuelLitres>.Unavailable();
    public SnapshotValue<VePercent> VeCapacity { get; init; } = SnapshotValue<VePercent>.Unavailable();

    internal VehicleSnapshot Validate(SnapshotContext context, string? failure) => new()
    {
        FuelCapacity = FuelCapacity.Validate(context, true, failure),
        VeCapacity = VeCapacity.Validate(context, true, failure),
    };
}

public sealed record TelemetrySnapshot
{
    public SnapshotValue<FuelLitres> Fuel { get; init; } = SnapshotValue<FuelLitres>.Unavailable();
    public SnapshotValue<VePercent> Ve { get; init; } = SnapshotValue<VePercent>.Unavailable();
    public SnapshotValue<FuelRatio> FuelRatio { get; init; } = SnapshotValue<FuelRatio>.Unavailable();
    public SnapshotValue<PerWheel<TyreState>> Tyres { get; init; } = SnapshotValue<PerWheel<TyreState>>.Unavailable();

    internal TelemetrySnapshot Validate(SnapshotContext context, string? failure) => new()
    {
        Fuel = Fuel.Validate(context, false, failure),
        Ve = Ve.Validate(context, false, failure),
        FuelRatio = FuelRatio.Validate(context, false, failure),
        Tyres = Tyres.Validate(context, false, failure),
    };
}

/// <summary>Opponent identity and optional observations; no compound inference from rain or pace.</summary>
public sealed record OpponentObservation(string VehicleId, string? CarId, string? ClassId,
    Laps? CompletedLaps, Seconds? LastLapTime, ImmutableArray<Seconds?> SectorTimes,
    TyreCompound? Compound, bool? InPit, RaceControlState RaceControl);

/// <summary>Verified condition metadata only; no invented crossover thresholds or pace ranking.</summary>
public sealed record CompoundConditionRecommendation(TyreCompound Compound, RoadConditionFamily Condition);

/// <summary>
/// One atomic portable observation. Unavailable fields never mean zero or unlimited.
/// Known empty collections use ImmutableArray.Empty; null fields inside domain payloads remain unknown.
/// Sources must supply initialized immutable arrays and preserve the documented domain units.
/// </summary>
public sealed record LmuSnapshot(SnapshotContext Context, ConnectionStatus Connection)
{
    public SessionSnapshot Session { get; init; } = new();
    public VehicleSnapshot Vehicle { get; init; } = new();
    public TelemetrySnapshot Telemetry { get; init; } = new();
    public SnapshotValue<ImmutableArray<OpponentObservation>> Scoring { get; init; } = SnapshotValue<ImmutableArray<OpponentObservation>>.Unavailable();
    public SnapshotValue<Forecast> Forecast { get; init; } = SnapshotValue<Forecast>.Unavailable();
    public SnapshotValue<ImmutableArray<DryTyreStock>> DryTyreInventory { get; init; } = SnapshotValue<ImmutableArray<DryTyreStock>>.Unavailable();
    public SnapshotValue<ImmutableArray<TyreCompound>> AvailableCompounds { get; init; } = SnapshotValue<ImmutableArray<TyreCompound>>.Unavailable();
    public SnapshotValue<ImmutableArray<CompoundConditionRecommendation>> RecommendedCompoundConditions { get; init; } = SnapshotValue<ImmutableArray<CompoundConditionRecommendation>>.Unavailable();
    public SnapshotValue<ImmutableArray<TyreServicePattern>> TyreServiceOptions { get; init; } = SnapshotValue<ImmutableArray<TyreServicePattern>>.Unavailable();
    public SnapshotValue<PitServiceModel> PitEstimate { get; init; } = SnapshotValue<PitServiceModel>.Unavailable();

    /// <summary>Group flags are conservative: partial or mixed-status groups are unavailable; inspect individual values.</summary>
    public LmuCapabilities Capabilities => new(Context.Identity,
    [
        new(CapabilityId.Session, GroupStatus(Session.Identity.Status, Session.Duration.Status,
            Session.ElapsedTime.Status, Session.RemainingTime.Status, Session.CompletedLaps.Status,
            Session.RaceControl.Status, Session.Weather.Status), "Inspect Session for per-field provenance"),
        new(CapabilityId.Telemetry, GroupStatus(Telemetry.Fuel.Status, Telemetry.Ve.Status,
            Telemetry.FuelRatio.Status, Telemetry.Tyres.Status), "Inspect Telemetry for per-field provenance"),
        new(CapabilityId.FuelCapacity, Vehicle.FuelCapacity.Status, Vehicle.FuelCapacity.UnavailableReason),
        new(CapabilityId.VeCapacity, Vehicle.VeCapacity.Status, Vehicle.VeCapacity.UnavailableReason),
        new(CapabilityId.OpponentTiming, Scoring.Status, Scoring.UnavailableReason),
        // Timing alone cannot establish opponent compound support, including for an empty field.
        new(CapabilityId.OpponentCompound, Scoring.IsAvailable && !Scoring.Value.IsDefaultOrEmpty &&
            Scoring.Value.All(opponent => opponent.Compound is not null) ? Scoring.Status : CapabilityStatus.Unavailable,
            "Only explicitly observed opponent compounds are supported"),
        new(CapabilityId.Forecast, Forecast.Status, Forecast.UnavailableReason),
        new(CapabilityId.DryTyreInventory, DryTyreInventory.Status, DryTyreInventory.UnavailableReason),
        new(CapabilityId.LegalCompounds, AvailableCompounds.Status, AvailableCompounds.UnavailableReason),
        new(CapabilityId.RecommendedCompoundConditions, RecommendedCompoundConditions.Status, RecommendedCompoundConditions.UnavailableReason),
        new(CapabilityId.TyreServiceOptions, TyreServiceOptions.Status, TyreServiceOptions.UnavailableReason),
        new(CapabilityId.PitEstimate, PitEstimate.Status, PitEstimate.UnavailableReason),
    ]);

    private static CapabilityStatus GroupStatus(params CapabilityStatus[] statuses) =>
        statuses.Distinct().Count() == 1 ? statuses[0] : CapabilityStatus.Unavailable;

    internal LmuSnapshot Validate(string? failure) => this with
    {
        Session = Session.Validate(Context, failure),
        Vehicle = Vehicle.Validate(Context, failure),
        Telemetry = Telemetry.Validate(Context, failure),
        Scoring = Scoring.Validate(Context, false, failure)
            .Require(items => !items.IsDefault && items.All(item => item is not null && !item.SectorTimes.IsDefault),
                "Scoring requires initialized observations and sector arrays"),
        Forecast = Forecast.Validate(Context, true, failure)
            .Require(forecast => !forecast.Points.IsDefault && forecast.Points.All(point => point is not null),
                "Forecast requires initialized points"),
        DryTyreInventory = DryTyreInventory.Validate(Context, true, failure)
            .Require(items => !items.IsDefault && items.All(item => item is not null), "Inventory requires initialized stock"),
        AvailableCompounds = AvailableCompounds.Validate(Context, true, failure)
            .Require(items => !items.IsDefault && items.All(item => item is not null), "Compounds require an initialized array"),
        RecommendedCompoundConditions = RecommendedCompoundConditions.Validate(Context, true, failure)
            .Require(items => !items.IsDefault && items.All(item => item is not null), "Recommendations require an initialized array"),
        TyreServiceOptions = TyreServiceOptions.Validate(Context, true, failure)
            .Require(items => !items.IsDefault, "Tyre services require an initialized array"),
        PitEstimate = PitEstimate.Validate(Context, true, failure)
            .Require(model => !model.Estimates.IsDefaultOrEmpty && model.Estimates.All(estimate => estimate is not null),
                "No pit-service estimates supplied"),
    };
}
