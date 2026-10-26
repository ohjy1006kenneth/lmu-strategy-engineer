using System.Collections.Immutable;
using LmuStrategy.Domain;

namespace LmuStrategy.Strategy;

/// <summary>Opaque identities supplied by the caller, including the validated service-rule configuration.</summary>
public sealed record PitContext(string CarId, string TrackId, string LayoutId, string ServiceRulesId,
    IntegrationIdentity Identity);

/// <summary>
/// SnapshotId identifies an immutable current pit-estimate observation, scoped to the session by the caller.
/// Arrival levels are predictions at this stop, not race-start levels. Missing levels remain unavailable.
/// </summary>
public sealed record PitRequest(PitContext Context, string? SnapshotId, FuelLitres? ArrivalFuel,
    VePercent? ArrivalVe, FuelLitres TargetFuel, VePercent TargetVe, FuelRatio FuelRatio,
    PerWheel<TyreReplacement?> Replacements);

/// <summary>Context not present in the Domain sample is required here before any sample may be used.</summary>
public sealed record ScopedPitServiceModel(PitContext Context, string? SnapshotId, PitServiceModel Model);

public sealed record PitEstimate(PitServiceEstimate? Estimate, ImmutableArray<string> Warnings)
{
    public Seconds? TotalCost => Estimate is null ? null :
        new(Estimate.LaneLoss.Value + Estimate.ServiceTime.Value + Estimate.ColdOutlapTime.Value);
}

/// <summary>Both source estimates are retained. Lane traversal is already paid by the energy stop.</summary>
public sealed record MarginalTyreCost(PitServiceEstimate WithoutTyres, PitServiceEstimate WithTyres)
{
    public SecondsDelta ServiceTime => new(WithTyres.ServiceTime.Value - WithoutTyres.ServiceTime.Value);
    public SecondsDelta ColdOutlapTime => new(WithTyres.ColdOutlapTime.Value - WithoutTyres.ColdOutlapTime.Value);
    public SecondsDelta TotalCost => new(ServiceTime.Value + ColdOutlapTime.Value);
}

public sealed record MarginalTyreEstimate(MarginalTyreCost? Cost, ImmutableArray<string> Warnings);

public interface IPitServiceModel
{
    PitEstimate EstimatePit(PitRequest request);
    MarginalTyreEstimate EstimateMarginalTyres(PitRequest request);
}

/// <summary>
/// Selects exact contextual observations. No timing coefficients, interpolation or concurrency assumptions.
/// Evidence is supplied by a verified adapter/calibration process; this class cannot verify LMU itself.
/// </summary>
public sealed class EvidencePitServiceModel(ImmutableArray<ScopedPitServiceModel> models) : IPitServiceModel
{
    private static readonly PerWheel<TyreReplacement?> Keep = new(null, null, null, null);

    public PitEstimate EstimatePit(PitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ValidContext(request.Context) || request.Replacements is null ||
            request.ArrivalFuel is null || request.ArrivalVe is null)
            return Unavailable("Pit estimate unavailable: arrival energy or complete context is missing.");

        // Targets below arrival levels request no addition; they never imply draining energy.
        double fuelAdded = Math.Max(0, request.TargetFuel.Value - request.ArrivalFuel.Value.Value);
        double veAdded = Math.Max(0, request.TargetVe.Value - request.ArrivalVe.Value.Value);
        if (fuelAdded == 0 && veAdded == 0 && request.Replacements == Keep)
            return Unavailable("Pit estimate unavailable: no supported service requested.");

        var matches = (models.IsDefault ? [] : models)
            .Where(scope => scope is not null && scope.Context == request.Context && scope.Model is not null)
            .SelectMany(scope => (scope.Model.Estimates.IsDefault ? [] : scope.Model.Estimates)
                .Where(sample => Matches(sample, scope, request, fuelAdded, veAdded)))
            .ToArray();

        // Never derive a combined-service time from unrelated energy/tyre observations.
        foreach (var source in new[] { ModelSource.VerifiedLmu, ModelSource.ValidatedBuildFallback })
        {
            var candidates = matches.Where(sample => sample.Evidence.Source == source).Distinct().ToArray();
            if (candidates.Length == 0) continue;
            if (candidates.Length != 1)
                return Unavailable("Pit estimate unavailable: conflicting matching evidence.");

            var selected = candidates[0];
            if (source == ModelSource.VerifiedLmu)
                return new(selected, []);

            var evidence = selected.Evidence;
            // Build-scoped fallback is always explicit and low confidence, even if mislabelled high by a caller.
            return new(selected with { Evidence = new(evidence.Source, ConfidenceLevel.Low, evidence.SampleCount,
                evidence.ConditionCoverage, evidence.ExtrapolationLaps, evidence.Identity) },
                ["Using a validated-build pit fallback with low confidence; no current LMU estimate matched."]);
        }

        return Unavailable("Pit estimate unavailable: no matching current LMU or validated-build evidence; timing is unknown.");
    }

    public MarginalTyreEstimate EstimateMarginalTyres(PitRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Replacements is null || request.Replacements == Keep ||
            request.ArrivalFuel is null || request.ArrivalVe is null ||
            (request.TargetFuel.Value <= request.ArrivalFuel.Value.Value &&
             request.TargetVe.Value <= request.ArrivalVe.Value.Value))
            return new(null, ["Marginal tyre cost requires tyres added to an already-required energy stop."]);

        var withoutTyres = EstimatePit(request with { Replacements = Keep });
        var withTyres = EstimatePit(request);
        var warnings = withoutTyres.Warnings.AddRange(withTyres.Warnings);
        if (withoutTyres.Estimate is null || withTyres.Estimate is null)
            return new(null, warnings);
        if (withoutTyres.Estimate.LaneLoss != withTyres.Estimate.LaneLoss)
            return new(null, warnings.Add("Marginal tyre cost unavailable: lane-loss evidence is inconsistent."));

        return new(new(withoutTyres.Estimate, withTyres.Estimate), warnings);
    }

    private static bool Matches(PitServiceEstimate sample, ScopedPitServiceModel scope, PitRequest request,
        double fuelAdded, double veAdded) => sample is not null && sample.Evidence is not null &&
        sample.Evidence.Identity == request.Context.Identity && sample.Evidence.SampleCount > 0 &&
        sample.Evidence.Confidence != ConfidenceLevel.Unavailable &&
        MatchesAddition(sample.FuelAdded.Value, fuelAdded, request.ArrivalFuel!.Value.Value, request.TargetFuel.Value) &&
        MatchesAddition(sample.VeAdded.Value, veAdded, request.ArrivalVe!.Value.Value, request.TargetVe.Value) &&
        sample.FuelRatio == request.FuelRatio && sample.Replacements == request.Replacements &&
        (sample.Evidence.Source == ModelSource.ValidatedBuildFallback ||
         (sample.Evidence.Source == ModelSource.VerifiedLmu &&
          !string.IsNullOrWhiteSpace(request.SnapshotId) && scope.SnapshotId == request.SnapshotId));

    // Subtraction can round e.g. 40 - 39.9 away from the stored 0.1-litre quote.
    // Accept exact reconstruction at machine precision, never a domain-sized tolerance or interpolation.
    private static bool MatchesAddition(double sample, double added, double arrival, double target) =>
        sample == added || (sample > 0 && target > arrival && arrival + sample == target);

    private static bool ValidContext(PitContext context) => context is not null &&
        !string.IsNullOrWhiteSpace(context.CarId) && !string.IsNullOrWhiteSpace(context.TrackId) &&
        !string.IsNullOrWhiteSpace(context.LayoutId) && !string.IsNullOrWhiteSpace(context.ServiceRulesId) &&
        context.Identity is { } identity && !string.IsNullOrWhiteSpace(identity.GameBuild) &&
        !string.IsNullOrWhiteSpace(identity.AdapterVersion) && !string.IsNullOrWhiteSpace(identity.SchemaVersion) &&
        !string.IsNullOrWhiteSpace(identity.PhysicsVersion) && !string.IsNullOrWhiteSpace(identity.BopVersion);

    private static PitEstimate Unavailable(string warning) => new(null, [warning]);
}
