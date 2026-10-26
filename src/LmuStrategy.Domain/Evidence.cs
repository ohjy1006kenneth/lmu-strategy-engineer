using System.Collections.Immutable;

namespace LmuStrategy.Domain;

public enum ConfidenceLevel { Unavailable, Low, Medium, High }
public enum ModelSource
{
    Unavailable, PersonalMeasured, PersonalFitted, SameCarFieldFallback, SameClassFieldFallback,
    VerifiedLmu, ValidatedBuildFallback, SyntheticSimulationFixture,
}

/// <summary>Identity supplied by the adapter or fixture; null fields are explicitly unknown.</summary>
public sealed record IntegrationIdentity(
    string? GameBuild, string? AdapterVersion, string? SchemaVersion, string? PhysicsVersion, string? BopVersion);

/// <summary>Coverage describes the selected conditions; extrapolation is distance beyond lap coverage.</summary>
public sealed record ModelEvidence
{
    public ModelSource Source { get; }
    public ConfidenceLevel Confidence { get; }
    public int SampleCount { get; }
    public string ConditionCoverage { get; }
    public Laps? ExtrapolationLaps { get; }
    public IntegrationIdentity? Identity { get; }

    public ModelEvidence(ModelSource source, ConfidenceLevel confidence, int sampleCount,
        string conditionCoverage, Laps? extrapolationLaps, IntegrationIdentity? identity)
    {
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
        if (!Enum.IsDefined(confidence)) throw new ArgumentOutOfRangeException(nameof(confidence));
        if (source == ModelSource.Unavailable && confidence != ConfidenceLevel.Unavailable)
            throw new ArgumentException("Unavailable evidence cannot support a confidence claim.", nameof(confidence));
        ArgumentException.ThrowIfNullOrWhiteSpace(conditionCoverage);
        Source = source;
        Confidence = confidence;
        SampleCount = UnitGuard.Count(sampleCount, nameof(sampleCount));
        ConditionCoverage = conditionCoverage;
        ExtrapolationLaps = extrapolationLaps;
        Identity = identity;
    }

    public static ModelEvidence Unavailable { get; } = new(
        ModelSource.Unavailable, ConfidenceLevel.Unavailable, 0, "Unknown", null, null);
}

/// <summary>A supplied value plus its evidence. A null Sourced wrapper means no value is available.</summary>
public sealed record Sourced<T>(T Value, ModelEvidence Evidence);

public enum CapabilityStatus { Unavailable, VerifiedSharedMemory, VerifiedRest, Derived, Cached }
public enum CapabilityId
{
    Telemetry, FuelCapacity, VeCapacity, Forecast, DryTyreInventory, LegalCompounds,
    TyreServiceOptions, PitEstimate, FuelRatioMapping, OpponentTiming, OpponentCompound,
    Session, RecommendedCompoundConditions,
}
public sealed record CapabilityState(CapabilityId Id, CapabilityStatus Status, string? Details);

/// <summary>Describes evidence only; it does not probe LMU. Missing entries are unavailable.</summary>
public sealed record LmuCapabilities(IntegrationIdentity Identity, ImmutableArray<CapabilityState> Entries)
{
    public CapabilityStatus StatusOf(CapabilityId id)
    {
        if (!Enum.IsDefined(id) || Entries.IsDefaultOrEmpty) return CapabilityStatus.Unavailable;
        var matches = Entries.Where(entry => entry.Id == id).ToArray();
        // Conflicting or duplicate reports must not turn into an arbitrary positive capability.
        return matches.Length == 1 && Enum.IsDefined(matches[0].Status)
            ? matches[0].Status : CapabilityStatus.Unavailable;
    }
}
