using System.Collections.Immutable;
using LmuStrategy.Domain;

namespace LmuStrategy.Strategy.Tests;

// All timings and verification metadata here are invented test inputs, never LMU calibration.
public sealed class PitServiceTests
{
    private static readonly IntegrationIdentity Identity = new("fixture-build", "fixture-adapter", "fixture-schema", "fixture-physics", "fixture-bop");
    private static readonly PitContext Context = new("fixture-car", "fixture-track", "fixture-layout", "fixture-rules", Identity);
    private static readonly PerWheel<TyreReplacement?> Keep = new(null, null, null, null);
    private static readonly PerWheel<TyreReplacement?> Fronts = new(new(new("fixture-dry", TyreSurface.Dry)), new(new("fixture-dry", TyreSurface.Dry)), null, null);
    private static PitRequest Request(double fuel = 5, double ve = 10, PerWheel<TyreReplacement?>? tyres = null) =>
        new(Context, "fixture-current-snapshot", new(40 - fuel), new(50 - ve), new(40), new(50), new(1), tyres ?? Keep);

    private static PitServiceEstimate Sample(double fuel = 5, double ve = 10, double service = 4,
        PerWheel<TyreReplacement?>? tyres = null, double cold = 0, ModelSource source = ModelSource.VerifiedLmu) =>
        new(new(fuel), new(ve), new(1), tyres ?? Keep, new(20), new(service), new(cold),
            new(source, ConfidenceLevel.High, 1, "Synthetic test inputs only", null, Identity));

    private static ScopedPitServiceModel Scope(params PitServiceEstimate[] samples) =>
        new(Context, "fixture-current-snapshot", new(samples.ToImmutableArray()));
    private static EvidencePitServiceModel Model(params PitServiceEstimate[] samples) => new([Scope(samples)]);

    [Fact]
    public void Splash_uses_amount_added_not_identical_target_and_preserves_breakdown()
    {
        var model = Model(Sample(), Sample(30, 40, 18));
        var splash = model.EstimatePit(Request());
        var refill = model.EstimatePit(Request(30, 40));
        Assert.Equal(24, splash.TotalCost!.Value.Value);
        Assert.Equal(38, refill.TotalCost!.Value.Value);
        Assert.Equal(20, splash.Estimate!.LaneLoss.Value);
        Assert.Equal(4, splash.Estimate.ServiceTime.Value);
        Assert.Equal(5, splash.Estimate.FuelAdded.Value);
        Assert.Equal(10, splash.Estimate.VeAdded.Value);
    }

    [Fact]
    public void Tyre_only_stop_pays_lane_service_and_cold_outlap_once()
    {
        var result = Model(Sample(0, 0, 12, Fronts, 3)).EstimatePit(Request(0, 0, Fronts));
        Assert.Equal(35, result.TotalCost!.Value.Value);
        Assert.Equal(3, result.Estimate!.ColdOutlapTime.Value);
    }

    [Theory]
    [InlineData(12, 11)] // synthetic sequential observation
    [InlineData(5, 4)]   // synthetic concurrent observation
    public void Combined_service_requires_its_own_evidence_and_marginal_tyres_exclude_lane(double combined, double marginal)
    {
        var model = Model(Sample(), Sample(service: combined, tyres: Fronts, cold: 3));
        var result = model.EstimateMarginalTyres(Request(tyres: Fronts));
        Assert.Equal(marginal, result.Cost!.TotalCost.Value);
        Assert.Equal(combined - 4, result.Cost.ServiceTime.Value);
        Assert.Equal(3, result.Cost.ColdOutlapTime.Value);
        Assert.Equal(20, result.Cost.WithTyres.LaneLoss.Value);
        Assert.Equal(ModelSource.VerifiedLmu, result.Cost.WithoutTyres.Evidence.Source);
    }

    [Fact]
    public void Separate_energy_and_tyre_samples_do_not_establish_concurrency()
    {
        var model = Model(Sample(), Sample(0, 0, 12, Fronts, 3));
        Assert.Null(model.EstimatePit(Request(tyres: Fronts)).TotalCost);
        Assert.Null(model.EstimateMarginalTyres(Request(tyres: Fronts)).Cost);
    }

    [Fact]
    public void Verified_current_estimate_precedes_fallback_and_fallback_is_low_confidence()
    {
        var fallback = Sample(service: 9, source: ModelSource.ValidatedBuildFallback);
        Assert.Equal(4, Model(fallback, Sample()).EstimatePit(Request()).Estimate!.ServiceTime.Value);
        var result = Model(fallback).EstimatePit(Request());
        Assert.Equal(9, result.Estimate!.ServiceTime.Value);
        Assert.Equal(ConfidenceLevel.Low, result.Estimate.Evidence.Confidence);
        Assert.Equal(ModelSource.ValidatedBuildFallback, result.Estimate.Evidence.Source);
        Assert.NotEmpty(result.Warnings);
    }

    [Theory]
    [InlineData("build")]
    [InlineData("schema")]
    [InlineData("physics")]
    [InlineData("bop")]
    [InlineData("adapter")]
    [InlineData("track")]
    [InlineData("layout")]
    [InlineData("car")]
    [InlineData("rules")]
    public void Context_or_build_mismatch_cannot_reuse_even_a_fallback(string field)
    {
        var context = field switch
        {
            "build" => Context with { Identity = Identity with { GameBuild = "other" } },
            "schema" => Context with { Identity = Identity with { SchemaVersion = "other" } },
            "physics" => Context with { Identity = Identity with { PhysicsVersion = "other" } },
            "bop" => Context with { Identity = Identity with { BopVersion = "other" } },
            "adapter" => Context with { Identity = Identity with { AdapterVersion = "other" } },
            "track" => Context with { TrackId = "other" },
            "layout" => Context with { LayoutId = "other" },
            "car" => Context with { CarId = "other" },
            _ => Context with { ServiceRulesId = "other" },
        };
        var model = new EvidencePitServiceModel([Scope(Sample(source: ModelSource.ValidatedBuildFallback)) with { Context = context }]);
        Assert.Null(model.EstimatePit(Request()).Estimate);
    }

    [Fact]
    public void Old_current_snapshot_is_rejected_but_build_scoped_fallback_can_be_reused()
    {
        var model = new EvidencePitServiceModel([Scope(Sample()) with { SnapshotId = "old" },
            Scope(Sample(service: 9, source: ModelSource.ValidatedBuildFallback)) with { SnapshotId = null }]);
        Assert.Equal(9, model.EstimatePit(Request()).Estimate!.ServiceTime.Value);
        Assert.Null(new EvidencePitServiceModel([Scope(Sample()) with { SnapshotId = null }]).EstimatePit(Request()).Estimate);
    }

    [Fact]
    public void Missing_or_unsupported_estimate_is_explicitly_unavailable_never_zero()
    {
        foreach (var model in new[] { Model(), new EvidencePitServiceModel([]),
            Model(Sample(source: ModelSource.SyntheticSimulationFixture)), Model(Sample(fuel: 6)),
            Model(Sample(ve: -1)), Model(Sample() with { Evidence = ModelEvidence.Unavailable }) })
        {
            var result = model.EstimatePit(Request());
            Assert.Null(result.Estimate);
            Assert.Null(result.TotalCost);
            Assert.NotEmpty(result.Warnings);
        }
        Assert.Null(Model(Sample()).EstimatePit(Request() with { ArrivalFuel = null }).Estimate);
        Assert.Null(Model(Sample()).EstimatePit(Request() with { ArrivalVe = null }).Estimate);
    }

    [Fact]
    public void No_extrapolation_from_ratio_compound_or_corner_and_conflicting_quotes_fail_closed()
    {
        var model = Model(Sample(tyres: Fronts));
        Assert.Null(model.EstimatePit(Request(tyres: Fronts) with { FuelRatio = new(2) }).Estimate);
        Assert.Null(model.EstimatePit(Request(tyres: Fronts with { RR = Fronts.FL })).Estimate);
        Assert.Null(model.EstimatePit(Request(tyres: Fronts with { FL = new(new("other", TyreSurface.Dry)) })).Estimate);
        Assert.Null(Model(Sample(), Sample(service: 99)).EstimatePit(Request()).Estimate);
    }

    [Fact]
    public void Marginal_cost_requires_an_energy_stop_and_both_comparable_quotes()
    {
        Assert.Null(Model(Sample(0, 0, tyres: Fronts)).EstimateMarginalTyres(Request(0, 0, Fronts)).Cost);
        Assert.Null(Model(Sample()).EstimateMarginalTyres(Request()).Cost);
        Assert.Null(Model(Sample(tyres: Fronts)).EstimateMarginalTyres(Request(tyres: Fronts)).Cost);
    }

    [Fact]
    public void Unknown_identity_or_evidence_from_another_build_cannot_establish_validation()
    {
        var unknown = Context with { Identity = Identity with { GameBuild = null } };
        var sample = Sample() with { Evidence = new(ModelSource.VerifiedLmu, ConfidenceLevel.High, 1,
            "Synthetic test", null, Identity with { GameBuild = "other" }) };
        Assert.Null(Model(sample).EstimatePit(Request()).Estimate);
        Assert.Null(new EvidencePitServiceModel([Scope(Sample()) with { Context = unknown }])
            .EstimatePit(Request() with { Context = unknown }).Estimate);
        Assert.Null(new EvidencePitServiceModel(default).EstimatePit(Request()).Estimate);
        Assert.Null(new EvidencePitServiceModel([Scope() with { Model = new(default) }]).EstimatePit(Request()).Estimate);
    }

    [Fact]
    public void Target_below_arrival_does_not_model_draining_and_ve_addition_is_matched_independently()
    {
        var model = Model(Sample(0, 10, 6, Fronts));
        var request = Request(0, 10, Fronts) with { ArrivalFuel = new(45) };
        Assert.Equal(6, model.EstimatePit(request).Estimate!.ServiceTime.Value);
        Assert.Null(model.EstimatePit(request with { ArrivalVe = new(41) }).Estimate);
        Assert.Null(Model(Sample(0, 0)).EstimatePit(Request(0, 0)).Estimate);
    }

    [Fact]
    public void Marginal_estimate_preserves_both_provenances_and_rejects_inconsistent_lane_loss()
    {
        var result = Model(Sample(), Sample(service: 12, tyres: Fronts, cold: 3,
            source: ModelSource.ValidatedBuildFallback)).EstimateMarginalTyres(Request(tyres: Fronts));
        Assert.Equal(ConfidenceLevel.High, result.Cost!.WithoutTyres.Evidence.Confidence);
        Assert.Equal(ConfidenceLevel.Low, result.Cost.WithTyres.Evidence.Confidence);
        Assert.NotEmpty(result.Warnings);
        Assert.Null(Model(Sample(), Sample(tyres: Fronts) with { LaneLoss = new(99) })
            .EstimateMarginalTyres(Request(tyres: Fronts)).Cost);
    }

    [Fact]
    public void Decimal_splash_matches_despite_subtraction_roundoff_without_interpolating_amounts()
    {
        var request = Request(0.1, 0);
        Assert.Equal(4, Model(Sample(0.1, 0)).EstimatePit(request).Estimate!.ServiceTime.Value);
        Assert.Null(Model(Sample(0.11, 0)).EstimatePit(request).Estimate);
    }
}
