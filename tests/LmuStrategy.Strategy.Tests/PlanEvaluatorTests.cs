using System.Collections.Immutable;
using LmuStrategy.Domain;

namespace LmuStrategy.Strategy.Tests;

public sealed class PlanEvaluatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(12)]
    public void Scores_arbitrary_plans_from_model_terms_without_trusting_candidate_times(int count)
    {
        var (input, plan, model) = Fixture.Create(count);
        var result = new PlanEvaluator(model).EvaluatePlan(input, plan, plan);
        Assert.True(result.Valid, string.Join("; ", result.Violations));
        Assert.Equal(count * 210 + (count - 1) * 33, result.PredictedTotalTime!.Value.Value);
        Assert.Equal(0, result.DeltaVsRecommended!.Value.Value);
        Assert.Equal(count * 2 * 6 + (count - 1) * 3, result.Score.Length);
        Assert.All(result.Score, term => Assert.NotEqual(ModelSource.Unavailable, term.Estimate.Evidence.Source));
        Assert.Equal(result.Score.Length * 0.5, result.Uncertainty!.Value.Value);
        Assert.Equal(ConfidenceLevel.High, result.Confidence);
        Assert.Equal(new FuelLitres(1), result.FinishFuel);
        Assert.Equal(new VePercent(1), result.FinishVe);
    }

    [Fact]
    public void Edited_and_recommended_plans_share_the_same_scoring_path()
    {
        var (input, plan, model) = Fixture.Create(1);
        var edited = plan with { Stints = [plan.Stints[0] with { SavingIntensity = new(0.5) }] };
        var evaluator = new PlanEvaluator(model);
        Assert.Equal(10, evaluator.EvaluatePlan(input, edited, plan).DeltaVsRecommended!.Value.Value);
        Assert.Equal(0, plan.Stints[0].SavingIntensity.Value);
        Assert.Equal(0, plan.Stints[0].PredictedDrivingTime.Value);
    }

    [Theory]
    [InlineData("gap")]
    [InlineData("overlap")]
    [InlineData("index")]
    [InlineData("pitLap")]
    [InlineData("pitIndex")]
    [InlineData("missingPit")]
    [InlineData("extraPit")]
    [InlineData("empty")]
    [InlineData("default")]
    public void Returns_violations_for_malformed_order_without_throwing(string edit)
    {
        var (input, plan, model) = Fixture.Create(3);
        plan = edit switch
        {
            "gap" => plan with { Stints = plan.Stints.SetItem(1, plan.Stints[1] with { StartLap = new(3) }) },
            "overlap" => plan with { Stints = plan.Stints.SetItem(1, plan.Stints[1] with { StartLap = new(1) }) },
            "index" => plan with { Stints = plan.Stints.SetItem(1, plan.Stints[1] with { Index = 0 }) },
            "pitLap" => plan with { Pits = plan.Pits.SetItem(0, plan.Pits[0] with { PitLap = new(1) }) },
            "pitIndex" => plan with { Pits = plan.Pits.Reverse().ToImmutableArray() },
            "missingPit" => plan with { Pits = plan.Pits.RemoveAt(0) },
            "extraPit" => plan with { Pits = plan.Pits.Add(plan.Pits[0]) },
            "empty" => plan with { Stints = [], Pits = [] },
            _ => plan with { Stints = default },
        };
        var result = new PlanEvaluator(model).EvaluatePlan(input, plan);
        Assert.False(result.Valid);
        Assert.Contains(result.Violations, v => v.Code == ViolationCode.Order);
        Assert.Null(result.PredictedTotalTime);
    }

    [Theory]
    [InlineData(4, 21, ViolationCode.Energy)]
    [InlineData(5, 20, ViolationCode.Energy)]
    [InlineData(3.9, 21, ViolationCode.Energy)]
    [InlineData(5, 19.9, ViolationCode.Energy)]
    [InlineData(5.01, 21, ViolationCode.Capacity)]
    [InlineData(5, 21.01, ViolationCode.Capacity)]
    public void Enforces_consumption_reserves_and_exact_capacity_boundaries(double fuel, double ve, ViolationCode code)
    {
        var (input, plan, model) = Fixture.Create(1);
        plan = plan with { Stints = [plan.Stints[0] with { StartFuel = new(fuel), StartVe = new(ve) }] };
        Assert.Contains(new PlanEvaluator(model).EvaluatePlan(input, plan).Violations, v => v.Code == code);
    }

    [Fact]
    public void Finish_buffer_uses_explicit_projection_and_does_not_change_official_duration()
    {
        var (input, plan, model) = Fixture.Create(1);
        input = input with { UserPreferences = input.UserPreferences with { BufferLaps = new(2) } };
        var result = new PlanEvaluator(model).EvaluatePlan(input, plan);
        Assert.Contains(result.Violations, v => v.Code == ViolationCode.FinishBuffer);
        Assert.Equal(100, input.RaceState.RemainingTime.Value);
        model.Projection = null;
        Assert.Contains(new PlanEvaluator(model).EvaluatePlan(input, plan).Violations, v => v.Code == ViolationCode.MissingPrediction);
    }

    [Theory]
    [InlineData(CapabilityId.FuelCapacity)]
    [InlineData(CapabilityId.VeCapacity)]
    [InlineData(CapabilityId.LegalCompounds)]
    [InlineData(CapabilityId.TyreServiceOptions)]
    public void Missing_or_stale_constraint_support_is_not_accepted(CapabilityId id)
    {
        var (input, plan, model) = Fixture.Create(2);
        input = input with { LmuCapabilities = input.LmuCapabilities with
        { Entries = input.LmuCapabilities.Entries.Where(e => e.Id != id).ToImmutableArray() } };
        var result = new PlanEvaluator(model).EvaluatePlan(input, plan);
        Assert.Contains(result.Violations, v => v.Code == ViolationCode.MissingCapability);
        Assert.Equal(ConfidenceLevel.Unavailable, result.Confidence);
        Assert.Null(result.PredictedTotalTime);
    }

    [Fact]
    public void Changed_build_and_unverified_capacity_fail_closed()
    {
        var (input, plan, model) = Fixture.Create(1);
        input = input with { LmuCapabilities = input.LmuCapabilities with { Identity = Fixture.Identity with { GameBuild = "other" } } };
        Assert.False(new PlanEvaluator(model).EvaluatePlan(input, plan).Valid);
        input = input with { SessionConstraints = input.SessionConstraints with { FuelCapacity = new(new(5), Fixture.Personal) } };
        Assert.False(new PlanEvaluator(model).EvaluatePlan(input, plan).Valid);
    }

    [Fact]
    public void Replacements_charge_each_dry_corner_and_wet_is_exempt()
    {
        var replacements = new PerWheel<TyreReplacement?>(new(Fixture.Wet), new(Fixture.Dry), null, null);
        var (input, plan, model) = Fixture.Create(3, replacements);
        var result = new PlanEvaluator(model).EvaluatePlan(input, plan);
        Assert.True(result.Valid, string.Join("; ", result.Violations));
        Assert.Equal(3, Assert.Single(result.DryTyresRemaining!.Value).Available.Value);
        Assert.Equal(1, plan.Pits[0].DryTyresConsumed.Value);
    }

    [Fact]
    public void Wet_replacements_work_without_dry_inventory_but_dry_replacements_do_not()
    {
        var wet = new PerWheel<TyreReplacement?>(new(Fixture.Wet), new(Fixture.Wet), null, null);
        var (input, plan, model) = Fixture.Create(2, wet);
        input = input with { SessionConstraints = input.SessionConstraints with { AvailableDryTyres = null } };
        Assert.True(new PlanEvaluator(model).EvaluatePlan(input, plan).Valid);
        var dry = Fixture.Create(2, new(new(Fixture.Dry), null, null, null));
        Assert.Contains(new PlanEvaluator(dry.Model).EvaluatePlan(input, dry.Plan).Violations,
            v => v.Code == ViolationCode.Inventory);
    }

    [Fact]
    public void Inventory_accounts_for_corner_restrictions_and_whole_race_exhaustion()
    {
        var replacements = new PerWheel<TyreReplacement?>(new(Fixture.Dry), new(Fixture.Dry), null, null);
        var (input, plan, model) = Fixture.Create(3, replacements);
        input = input with { SessionConstraints = input.SessionConstraints with
        { AvailableDryTyres = new([new(Fixture.Dry, new(1), WheelPosition.FR), new(Fixture.Dry, new(0), null)], Fixture.Verified) } };
        Assert.Contains(new PlanEvaluator(model).EvaluatePlan(input, plan).Violations, v => v.Code == ViolationCode.Inventory);
        input = input with { SessionConstraints = input.SessionConstraints with
        { AvailableDryTyres = new([new(Fixture.Dry, new(1), WheelPosition.FR), new(Fixture.Dry, new(1), null)], Fixture.Verified) } };
        Assert.True(new PlanEvaluator(model).EvaluatePlan(input, plan).Valid);
    }

    [Fact]
    public void Rejects_illegal_compounds_services_and_invented_tyre_resets()
    {
        var (input, plan, model) = Fixture.Create(2, new(new(Fixture.Dry), null, null, null));
        Assert.Contains(new PlanEvaluator(model).EvaluatePlan(input, plan).Violations, v => v.Code == ViolationCode.IllegalService);
        input = input with { SessionConstraints = input.SessionConstraints with { LegalCompounds = new([], Fixture.Verified) } };
        Assert.Contains(new PlanEvaluator(model).EvaluatePlan(input, plan).Violations, v => v.Code == ViolationCode.IllegalCompound);
        var f = Fixture.Create(2);
        plan = f.Plan with { Stints = f.Plan.Stints.SetItem(1, f.Plan.Stints[1] with { TyreStart = Fixture.NewTyres(Fixture.Dry) }) };
        Assert.Contains(new PlanEvaluator(f.Model).EvaluatePlan(f.Input, plan).Violations, v => v.Code == ViolationCode.TyreContinuity);
    }

    [Fact]
    public void Candidate_cannot_fake_energy_targets_finish_or_inventory_claims()
    {
        var (input, plan, model) = Fixture.Create(2);
        plan = plan with { FinishFuel = new(2), FinishVe = new(2), DryTyresRemaining = [],
            Stints = plan.Stints.SetItem(0, plan.Stints[0] with { TargetFuelPerLap = new(0), TargetVePerLap = new(0) }),
            Pits = [plan.Pits[0] with { TargetFuel = new(4), DryTyresConsumed = new(1) }] };
        var result = new PlanEvaluator(model).EvaluatePlan(input, plan);
        Assert.Contains(result.Violations, v => v.Code == ViolationCode.Energy);
        Assert.Contains(result.Violations, v => v.Code == ViolationCode.Continuity);
        Assert.Contains(result.Violations, v => v.Code == ViolationCode.Inventory);
    }

    [Fact]
    public void Missing_models_never_become_zero_costs_or_precise_deltas()
    {
        var (input, plan, model) = Fixture.Create(2);
        model.MissingStint = true;
        Assert.Null(new PlanEvaluator(model).EvaluatePlan(input, plan, plan).PredictedTotalTime);
        model.MissingStint = false;
        model.MissingPit = true;
        var result = new PlanEvaluator(model).EvaluatePlan(input, plan, plan);
        Assert.Contains(result.Violations, v => v.Code == ViolationCode.MissingPrediction);
        Assert.Null(result.DeltaVsRecommended);
    }

    [Fact]
    public void Fallback_and_extrapolation_are_visible_and_cannot_claim_high_confidence()
    {
        var (input, plan, model) = Fixture.Create(2);
        model.PaceEvidence = new(ModelSource.SameClassFieldFallback, ConfidenceLevel.High, 2, "fixture wet", new(3), null);
        model.PitEvidence = new(ModelSource.ValidatedBuildFallback, ConfidenceLevel.High, 3, "fixture build", null, Fixture.Identity);
        var result = new PlanEvaluator(model).EvaluatePlan(input, plan);
        Assert.True(result.Valid, string.Join("; ", result.Violations));
        Assert.Equal(ConfidenceLevel.Low, result.Confidence);
        Assert.NotEmpty(result.Warnings);
        Assert.Contains(result.Score, s => s.Estimate.Evidence.ExtrapolationLaps?.Value == 3);
        Assert.Equal(4, model.LastPitRequest!.FuelAdded.Value);
        Assert.Equal(20, model.LastPitRequest.VeAdded.Value);
    }

    [Fact]
    public void Remaining_plan_must_start_at_observed_boundary_and_load()
    {
        var (input, plan, model) = Fixture.Create(1);
        input = input with { RaceState = input.RaceState with { CompletedLaps = new(1) } };
        Assert.Contains(new PlanEvaluator(model).EvaluatePlan(input, plan).Violations, v => v.Code == ViolationCode.Order);
    }

    [Fact]
    public void Allocation_is_shared_across_every_stop_and_does_not_refund_removed_tyres()
    {
        var (input, plan, model) = Fixture.Create(4, new(new(Fixture.Dry), new(Fixture.Dry), null, null), true);
        var result = new PlanEvaluator(model).EvaluatePlan(input, plan);
        Assert.Contains(result.Violations, v => v.Code == ViolationCode.Inventory);
    }

    [Theory]
    [InlineData("fuel")]
    [InlineData("ve")]
    [InlineData("reserves")]
    [InlineData("noCompounds")]
    [InlineData("noServices")]
    public void Zero_and_known_empty_constraints_are_not_treated_as_missing_defaults(string edit)
    {
        var (input, plan, model) = Fixture.Create(2);
        input = edit switch
        {
            "fuel" => input with { SessionConstraints = input.SessionConstraints with { FuelCapacity = new(new(0), Fixture.Verified) } },
            "ve" => input with { SessionConstraints = input.SessionConstraints with { VeCapacity = new(new(0), Fixture.Verified) } },
            "reserves" => input with { UserPreferences = input.UserPreferences with { VeReserve = new(-1) } },
            "noCompounds" => input with { SessionConstraints = input.SessionConstraints with { LegalCompounds = new([], Fixture.Verified) } },
            _ => input with { SessionConstraints = input.SessionConstraints with { LegalTyreServices = new([], Fixture.Verified) } },
        };
        Assert.False(new PlanEvaluator(model).EvaluatePlan(input, plan).Valid);
    }

    [Fact]
    public void Negative_energy_inputs_are_rejected_by_domain_units()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FuelLitres(-0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => new VePercent(-0.01));
    }

    [Fact]
    public void Cached_inventory_with_current_provenance_is_usable()
    {
        var (input, plan, model) = Fixture.Create(2, new(new(Fixture.Dry), new(Fixture.Dry), null, null));
        input = input with { LmuCapabilities = input.LmuCapabilities with { Entries = input.LmuCapabilities.Entries
            .Select(e => e.Id == CapabilityId.DryTyreInventory ? e with { Status = CapabilityStatus.Cached } : e).ToImmutableArray() } };
        Assert.True(new PlanEvaluator(model).EvaluatePlan(input, plan).Valid);
    }

    [Fact]
    public void Absent_time_term_or_per_lap_coverage_does_not_become_zero()
    {
        var (input, plan, model) = Fixture.Create(1);
        model.MissingTerm = true;
        Assert.Contains(new PlanEvaluator(model).EvaluatePlan(input, plan).Violations, v => v.Code == ViolationCode.MissingPrediction);
        model.MissingTerm = false;
        model.MissingLap = true;
        Assert.Contains(new PlanEvaluator(model).EvaluatePlan(input, plan).Violations, v => v.Code == ViolationCode.MissingPrediction);
    }

    [Fact]
    public void Unsupported_personal_evidence_cannot_claim_high_confidence()
    {
        var (input, plan, model) = Fixture.Create(1);
        model.PaceEvidence = new(ModelSource.PersonalMeasured, ConfidenceLevel.High, 0, "no observations", null, null);
        Assert.False(new PlanEvaluator(model).EvaluatePlan(input, plan).Valid);
    }

    [Fact]
    public void Nonpositive_baseline_is_not_a_supported_race_prediction()
    {
        var (input, plan, model) = Fixture.Create(1);
        model.Baseline = -1;
        Assert.False(new PlanEvaluator(model).EvaluatePlan(input, plan).Valid);
    }

    [Fact]
    public void Invalid_recommendation_suppresses_delta_but_does_not_change_valid_candidate()
    {
        var (input, plan, model) = Fixture.Create(1);
        var result = new PlanEvaluator(model).EvaluatePlan(input, plan, plan with { FinishFuel = new(2) });
        Assert.True(result.Valid);
        Assert.Null(result.DeltaVsRecommended);
        Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void Remaining_race_plan_accepts_current_load_and_rejects_fabricated_load()
    {
        var (input, plan, model) = Fixture.Create(1);
        input = input with { RaceState = input.RaceState with { CompletedLaps = new(10) } };
        plan = plan with { Stints = [plan.Stints[0] with { StartLap = new(10), EndLap = new(12) }] };
        model.Projection = new(new(11), Fixture.Personal);
        Assert.True(new PlanEvaluator(model).EvaluatePlan(input, plan).Valid);
        input = input with { VehicleState = input.VehicleState with { Fuel = new FuelLitres(4) } };
        Assert.Contains(new PlanEvaluator(model).EvaluatePlan(input, plan).Violations, v => v.Code == ViolationCode.Continuity);
    }

    [Fact]
    public void Missing_per_corner_observations_are_violations_before_model_invocation()
    {
        var (input, plan, model) = Fixture.Create(1);
        var tyres = plan.Stints[0].TyreStart with { FL = new(Fixture.Dry, null, null) };
        plan = plan with { Stints = [plan.Stints[0] with { TyreStart = tyres }] };
        Assert.Contains(new PlanEvaluator(model).EvaluatePlan(input, plan).Violations, v => v.Code == ViolationCode.MissingPrediction);
    }

    [Fact]
    public void Score_overflow_is_a_violation_instead_of_an_exception()
    {
        var (input, plan, model) = Fixture.Create(1);
        model.Baseline = double.MaxValue;
        Assert.Contains(new PlanEvaluator(model).EvaluatePlan(input, plan).Violations, v => v.Code == ViolationCode.UnsupportedInput);
    }
}

internal static class Fixture
{
    internal static readonly IntegrationIdentity Identity = new("fixture-build", "fixture-adapter", "fixture-schema", null, null);
    internal static readonly ModelEvidence Verified = new(ModelSource.VerifiedLmu, ConfidenceLevel.High, 1, "Synthetic test constraints", null, Identity);
    internal static readonly ModelEvidence Personal = new(ModelSource.PersonalMeasured, ConfidenceLevel.High, 20, "Synthetic test pace", null, null);
    internal static readonly TyreCompound Dry = new("event-dry", TyreSurface.Dry);
    internal static readonly TyreCompound Wet = new("event-wet", TyreSurface.Wet);
    internal static PerWheel<TyreState> NewTyres(TyreCompound c) => new(new(c, new(100), new(0)), new(c, new(100), new(0)), new(c, new(100), new(0)), new(c, new(100), new(0)));
    internal static PerWheel<TyreState> End(PerWheel<TyreState> tyres) => new(Age(tyres.FL), Age(tyres.FR), Age(tyres.RL), Age(tyres.RR));
    private static TyreState Age(TyreState t) => t with { RemainingLife = new(t.RemainingLife!.Value.Value - 2), Age = new(t.Age!.Value.Value + 2) };
    private static TyreState Replace(TyreState t, TyreReplacement? r) => r is null ? t : new(r.Compound, new(100), new(0));
    internal static (StrategyInput Input, RacePlan Plan, TestModel Model) Create(int count, PerWheel<TyreReplacement?>? replacements = null, bool everyStop = false)
    {
        replacements ??= new(null, null, null, null);
        var stints = ImmutableArray.CreateBuilder<Stint>();
        var pits = ImmutableArray.CreateBuilder<Pit>();
        var tyres = NewTyres(Dry);
        for (int i = 0; i < count; i++)
        {
            var end = End(tyres);
            stints.Add(new(i, new(i * 2), new(i * 2 + 2), new(5), new(21), new(1), new(2), new(10), new(0), tyres, end, new(0)));
            if (i < count - 1)
            {
                var r = i == 0 || everyStop ? replacements : new(null, null, null, null);
                int dry = new[] { r.FL, r.FR, r.RL, r.RR }.Count(x => x?.Compound.Surface == TyreSurface.Dry);
                pits.Add(new(i, new(i * 2 + 2), new(5), new(21), new(1), r, new(0), new(0), new(0), new(dry), ModelEvidence.Unavailable));
                tyres = new(Replace(end.FL, r.FL), Replace(end.FR, r.FR), Replace(end.RL, r.RL), Replace(end.RR, r.RR));
            }
        }
        var constraints = new SessionConstraints(new(new(5), Verified), new(new(21), Verified), new([Dry, Wet], Verified),
            new([new(Dry, new(4), null)], Verified), new([TyreServicePattern.None, TyreServicePattern.Fronts, TyreServicePattern.All], Verified));
        var race = new RaceState(new("test", "test", "test", "test", "test", "test"), new(0), new(0), new(100), RaceControlState.Green,
            new(RoadConditionFamily.Dry, null, null, null, null));
        var capabilities = new LmuCapabilities(Identity, Enum.GetValues<CapabilityId>().Select(id => new CapabilityState(id, CapabilityStatus.VerifiedRest, "Synthetic test support")).ToImmutableArray());
        var input = new StrategyInput(constraints, race, new(new(5), new(21), new(1), NewTyres(Dry)), new([]), [], new([]), capabilities,
            new(new(1), new(1), new(1), ModelDataSelection.RecentCleanLaps, new(20), Dry), null, null);
        return (input, new(StrategyMode.FlatOut, stints.ToImmutable(), pits.ToImmutable(), null, new(1), new(1), ModelEvidence.Unavailable), new(count));
    }
}

internal sealed class TestModel(int count) : IPlanPredictionModel
{
    internal Sourced<Laps>? Projection { get; set; } = new(new(count * 2 - 1), Fixture.Personal);
    internal bool MissingStint { get; set; }
    internal bool MissingPit { get; set; }
    internal bool MissingTerm { get; set; }
    internal bool MissingLap { get; set; }
    internal double Baseline { get; set; } = 100;
    internal ModelEvidence PaceEvidence { get; set; } = Fixture.Personal;
    internal ModelEvidence PitEvidence { get; set; } = Fixture.Verified;
    internal PitRequest? LastPitRequest { get; private set; }
    public Sourced<Laps>? ProjectRaceFinish(StrategyInput input) => Projection;
    private TimeEstimate Term(double seconds, ModelEvidence evidence) => new(new(seconds), new(0.5), evidence);
    public StintPrediction? PredictStint(StrategyInput input, Stint stint) => MissingStint ? null : new(
        new(new(2), Fixture.Personal), new(new(10), Fixture.Personal), Fixture.End(stint.TyreStart), Fixture.Personal,
        Enumerable.Range(0, stint.EndLap.Value - stint.StartLap.Value - (MissingLap ? 1 : 0)).Select(_ => new LapTimePrediction(
            Term(Baseline, PaceEvidence), MissingTerm ? null! : Term(1, PaceEvidence), Term(2 + stint.SavingIntensity.Value * 10, PaceEvidence),
            Term(3, PaceEvidence), Term(-1, PaceEvidence), Term(0, PaceEvidence))).ToImmutableArray());
    public PitPrediction? PredictPit(StrategyInput input, PitRequest request)
    {
        LastPitRequest = request;
        return MissingPit ? null : new(Term(10, PitEvidence), Term(20, PitEvidence), Term(3, PitEvidence));
    }
}
