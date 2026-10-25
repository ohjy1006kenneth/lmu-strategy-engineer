using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using LmuStrategy.Domain;

namespace LmuStrategy.Domain.Tests;

public class SnapshotTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(12)]
    public void Plans_of_arbitrary_length_roundtrip_and_keep_each_corner(int stintCount)
    {
        var plan = Fixtures.Plan(stintCount);
        var copy = Roundtrip(plan);
        Assert.Equal(stintCount, copy.Stints.Length);
        Assert.Equal(stintCount - 1, copy.Pits.Length);
        Assert.Equal(new Laps(10), copy.Stints[^1].LapCount);
        Assert.Equal(new Laps(stintCount * 10), copy.Stints[^1].EndLap);
        Assert.Equal(81, copy.Stints[0].TyreStart.FL.RemainingLife!.Value.Value);
        Assert.Equal(82, copy.Stints[0].TyreStart.FR.RemainingLife!.Value.Value);
        Assert.Equal(83, copy.Stints[0].TyreStart.RL.RemainingLife!.Value.Value);
        Assert.Equal(84, copy.Stints[0].TyreStart.RR.RemainingLife!.Value.Value);
        Assert.Equal(new Laps(1), copy.Stints[0].TyreStart.FL.Age);
        Assert.Equal(new Laps(4), copy.Stints[0].TyreStart.RR.Age);
        Assert.Equal(new Seconds(stintCount * 1000 + (stintCount - 1) * 50), copy.PredictedTotalTime);
        if (stintCount > 1)
        {
            Assert.Equal(Fixtures.Wet, copy.Pits[0].Replacements.FL!.Compound);
            Assert.Equal(Fixtures.Dry, copy.Pits[0].Replacements.FR!.Compound);
            Assert.Null(copy.Pits[0].Replacements.RL);
            Assert.Null(copy.Pits[0].Replacements.RR);
            Assert.Equal(1, copy.Pits[0].DryTyresConsumed.Value);
        }
    }

    [Fact]
    public void Input_roundtrip_preserves_unknown_constraints_separately_from_known_empty()
    {
        var unknown = Roundtrip(Fixtures.Input() with
        {
            SessionConstraints = new(null, null, null, null, null),
            VehicleState = new(null, null, null, new(new(null, null, null), new(null, null, null),
                new(null, null, null), new(null, null, null)))
        });
        Assert.Null(unknown.SessionConstraints.FuelCapacity);
        Assert.Null(unknown.SessionConstraints.AvailableDryTyres);
        Assert.Null(unknown.SessionConstraints.LegalCompounds);
        Assert.Null(unknown.VehicleState.Fuel);
        Assert.Null(unknown.VehicleState.Tyres.FL.RemainingLife);

        var empty = Roundtrip(Fixtures.Input() with
        {
            SessionConstraints = new(new(new(80), Fixtures.Evidence), new(new(100), Fixtures.Evidence),
                new([], Fixtures.Evidence), new([], Fixtures.Evidence), new([], Fixtures.Evidence))
        });
        Assert.Empty(empty.SessionConstraints.AvailableDryTyres!.Value);
        Assert.Empty(empty.SessionConstraints.LegalCompounds!.Value);
        Assert.Equal(80, empty.SessionConstraints.FuelCapacity!.Value.Value);
        Assert.Equal(CapabilityStatus.Unavailable, empty.LmuCapabilities.StatusOf(CapabilityId.PitEstimate));
    }

    [Fact]
    public void Input_models_weather_and_versions_survive_roundtrip()
    {
        var copy = Roundtrip(Fixtures.Input());
        Assert.Equal(new Fraction(0.6), copy.Forecast.Points[0].Weather.RoadWetness);
        Assert.Equal(new Celsius(-2), copy.Forecast.Points[0].Weather.AirTemperature);
        Assert.Equal(Fixtures.Evidence, copy.DriverModels[0].FuelPerLap!.Evidence);
        Assert.Equal(new VePerLapPercentagePoints(4.5), copy.DriverModels[0].VePerLap!.Value);
        Assert.Equal(new TyreWearPerLapPercentagePoints(0.4), copy.DriverModels[0].TyreWearPerLap!.Value.RR);
        Assert.Equal("synthetic-build", copy.LmuCapabilities.Identity.GameBuild);
        Assert.Equal(CapabilityStatus.Cached, copy.LmuCapabilities.StatusOf(CapabilityId.DryTyreInventory));
        Assert.Equal(3, copy.LoadedPlan!.Stints.Length);
        Assert.Equal(4, copy.UserEditedPlan!.Stints.Length);
    }

    [Fact]
    public void Results_preserve_optional_fuel_save_and_unavailable_outcome()
    {
        var plan = Fixtures.Plan(3);
        var result = new StrategyResult(StrategyStatus.Success, plan, null, plan,
            new(29), new(30), Fixtures.Evidence, ["Synthetic test only"],
            new([MaterialChangeKind.PitLap], "Pit lap changed", new SecondsDelta(-2)));
        var copy = Roundtrip(result);
        Assert.Null(copy.FuelSave);
        Assert.Equal(new Laps(29), copy.PredictedRaceLaps);
        Assert.Equal(new Laps(30), copy.StrategyDistanceLaps);
        Assert.Equal(new SecondsDelta(-2), copy.MaterialChange!.PredictedTimeDelta);
        Assert.Single(copy.Warnings);
        var save = Roundtrip(result with { FuelSave = plan with { StrategyMode = StrategyMode.FuelSave } });
        Assert.Equal(StrategyMode.FuelSave, save.FuelSave!.StrategyMode);
        var unavailable = Roundtrip(new StrategyResult(StrategyStatus.Unavailable, null, null, null,
            null, null, ModelEvidence.Unavailable, ["Missing capacity"], null));
        Assert.Null(unavailable.RecommendedPlan);
        Assert.Null(unavailable.PredictedRaceLaps);
        Assert.Equal(ConfidenceLevel.Unavailable, unavailable.ModelConfidence.Confidence);
    }

    [Fact]
    public void Snapshots_do_not_alias_caller_arrays_or_allow_in_place_corner_changes()
    {
        Stint[] caller = [Fixtures.Plan(1).Stints[0]];
        var plan = Fixtures.Plan(1) with { Stints = caller.ToImmutableArray() };
        caller[0] = caller[0] with { StartFuel = new(1) };
        Assert.Equal(new FuelLitres(60), plan.Stints[0].StartFuel);
        var changed = plan.Stints[0].TyreStart with { FL = new(Fixtures.Wet, new(100), new(0)) };
        Assert.Equal(Fixtures.Dry, plan.Stints[0].TyreStart.FL.Compound);
        Assert.Equal(Fixtures.Wet, changed.FL.Compound);
    }

    [Fact]
    public void Json_rejects_missing_null_unknown_enum_and_unknown_members()
    {
        Assert.Throws<JsonException>(() => DomainJson.Deserialize<StrategyInput>("{}"));
        Assert.Throws<JsonException>(() => DomainJson.Deserialize<StrategyInput>("null"));
        var node = JsonNode.Parse(DomainJson.Serialize(Fixtures.Plan(1)))!.AsObject();
        node["strategyMode"] = "Turbo";
        Assert.Throws<JsonException>(() => DomainJson.Deserialize<RacePlan>(node.ToJsonString()));
        node["strategyMode"] = 1;
        Assert.Throws<JsonException>(() => DomainJson.Deserialize<RacePlan>(node.ToJsonString()));
        node["strategyMode"] = "FlatOut";
        node["inventedField"] = true;
        Assert.Throws<JsonException>(() => DomainJson.Deserialize<RacePlan>(node.ToJsonString()));
        node.Remove("inventedField");
        node["stints"] = null;
        Assert.Throws<JsonException>(() => DomainJson.Deserialize<RacePlan>(node.ToJsonString()));
    }

    [Fact]
    public void Dry_inventory_cannot_include_wet_tyres_and_corner_restrictions_survive()
    {
        Assert.Throws<ArgumentException>(() => new DryTyreStock(Fixtures.Wet, new(4), null));
        var stock = Roundtrip(new DryTyreStock(Fixtures.Dry, new(2), WheelPosition.FL));
        Assert.Equal(WheelPosition.FL, stock.RestrictedTo);
        Assert.Equal(new TyreCount(2), stock.Available);
        var capabilities = new LmuCapabilities(Fixtures.Identity,
            [new(CapabilityId.FuelCapacity, CapabilityStatus.VerifiedRest, "fixture"),
             new(CapabilityId.FuelCapacity, CapabilityStatus.Unavailable, "conflicting fixture")]);
        Assert.Equal(CapabilityStatus.Unavailable, capabilities.StatusOf(CapabilityId.FuelCapacity));
    }

    [Fact]
    public void Unavailable_evidence_cannot_claim_high_confidence()
    {
        Assert.Throws<ArgumentException>(() => new ModelEvidence(ModelSource.Unavailable,
            ConfidenceLevel.High, 0, "unknown", null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModelEvidence(ModelSource.PersonalMeasured,
            ConfidenceLevel.Low, -1, "dry", new(0), null));
    }

    private static T Roundtrip<T>(T value)
    {
        var json = DomainJson.Serialize(value);
        var copy = DomainJson.Deserialize<T>(json);
        Assert.Equal(json, DomainJson.Serialize(copy));
        return copy;
    }
}

internal static class Fixtures
{
    internal static readonly TyreCompound Dry = new("synthetic-dry", TyreSurface.Dry);
    internal static readonly TyreCompound Wet = new("synthetic-wet", TyreSurface.Wet);
    internal static readonly IntegrationIdentity Identity = new("synthetic-build", "test-adapter", "test-schema", null, null);
    internal static readonly ModelEvidence Evidence = new(ModelSource.SyntheticSimulationFixture,
        ConfidenceLevel.Low, 12, "synthetic dry and wet coverage", new(2), Identity);
    internal static PerWheel<TyreState> Tyres => new(
        new(Dry, new(81), new(1)), new(Dry, new(82), new(2)),
        new(Dry, new(83), new(3)), new(Dry, new(84), new(4)));
    internal static RacePlan Plan(int count)
    {
        var stints = Enumerable.Range(0, count).Select(i => new Stint(i, new(i * 10), new((i + 1) * 10),
            new(60), new(90), new(0.9), new(3.5), new(4.5), new(0), Tyres, Tyres, new(1000))).ToImmutableArray();
        var pits = Enumerable.Range(0, count - 1).Select(i => new Pit(i, new((i + 1) * 10),
            new(60), new(90), new(0.9), new(new(Wet), new(Dry), null, null),
            new(20), new(25), new(5), new(1), Evidence)).ToImmutableArray();
        return new(StrategyMode.FlatOut, stints, pits, [new(Dry, new(8), null)], new(5), new(8), Evidence);
    }
    internal static StrategyInput Input() => new(
        new(new(new(80), Evidence), new(new(100), Evidence), new([Dry, Wet], Evidence),
            new([new(Dry, new(8), null)], Evidence), new([TyreServicePattern.None, TyreServicePattern.Fronts, TyreServicePattern.All], Evidence)),
        new(new("synthetic-session", "synthetic-driver", "synthetic-car", "synthetic-class", "synthetic-track", "layout"),
            new(3), new(300), new(3300), RaceControlState.Green,
            new(RoadConditionFamily.Dry, new(0), new(0), new(20), new(30))),
        new(new(50), new(80), new(0.9), Tyres),
        new([new(new(600), new(RoadConditionFamily.Wet, new(0.7), new(0.6), new(-2), new(8)), Evidence)]),
        [new(new("synthetic-driver", "synthetic-car", "synthetic-track", "layout", RoadConditionFamily.Dry, Dry),
            new(new(3.5), Evidence), new(new(4.5), Evidence), new(new(100), Evidence),
            new(new(new(0.1), new(0.2), new(0.3), new(0.4)), Evidence))],
        new([new(new(20), new(30), new(0.9), new(null, null, null, null), new(20), new(25), new(5), Evidence)]),
        new(Identity, [new(CapabilityId.Telemetry, CapabilityStatus.Derived, "synthetic"),
            new(CapabilityId.DryTyreInventory, CapabilityStatus.Cached, "synthetic cached fixture")]),
        new(new(1), new(2), new(3), ModelDataSelection.RecentCleanLaps, new(12), null),
        Plan(3), Plan(4));
}
