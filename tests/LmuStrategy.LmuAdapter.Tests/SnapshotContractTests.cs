using System.Collections.Immutable;
using LmuStrategy.Domain;
using LmuStrategy.LmuAdapter;

namespace LmuStrategy.LmuAdapter.Tests;

public sealed class SnapshotContractTests
{
    private static readonly IntegrationIdentity Identity = new("fixture-build", "adapter-1", "schema-1", null, null);
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly SnapshotContext Context = new(Identity, "session-1", Now);

    [Fact]
    public void Missing_data_is_unavailable_and_known_empty_inventory_is_distinct()
    {
        var input = new LmuSnapshot(Context, ConnectionStatus.Connected)
        {
            DryTyreInventory = Value(ImmutableArray<DryTyreStock>.Empty),
        };
        var result = Read(input);
        Assert.False(result.Vehicle.FuelCapacity.TryGetValue(out _));
        Assert.Throws<InvalidOperationException>(() => result.Telemetry.Fuel.Value);
        Assert.True(result.DryTyreInventory.TryGetValue(out var inventory));
        Assert.Empty(inventory);
        Assert.Equal(CapabilityStatus.Unavailable, result.Capabilities.StatusOf(CapabilityId.PitEstimate));
        Assert.Equal(CapabilityStatus.Derived, result.Capabilities.StatusOf(CapabilityId.DryTyreInventory));
    }

    [Fact]
    public void Disconnect_clears_live_values_and_reconnect_requires_fresh_observations()
    {
        var live = FuelSnapshot();
        var disconnect = live with { Connection = ConnectionStatus.Disconnected };
        var reconnectContext = Context with { CapturedAt = Now.AddSeconds(5) };
        var staleReconnect = live with { Context = reconnectContext };
        var freshReconnect = FuelSnapshot(reconnectContext with { CapturedAt = Now.AddSeconds(6) }, new FuelLitres(32));
        ILmuSnapshotSource source = new CompatibleLmuSource(new ScriptedLmuSource(
            [live, disconnect, staleReconnect, freshReconnect]), Identity);

        Assert.Equal(40, source.ReadSnapshot().Telemetry.Fuel.Value.Value);
        Assert.False(source.ReadSnapshot().Telemetry.Fuel.IsAvailable);
        Assert.False(source.ReadSnapshot().Telemetry.Fuel.IsAvailable);
        Assert.Equal(32, source.ReadSnapshot().Telemetry.Fuel.Value.Value);
        Assert.False(source.ReadSnapshot().Telemetry.Fuel.IsAvailable); // Script exhausted.
        Assert.Equal(40, live.Telemetry.Fuel.Value.Value); // Old snapshot remains immutable.
    }

    [Theory]
    [InlineData("build")]
    [InlineData("schema")]
    [InlineData("adapter")]
    [InlineData("physics")]
    [InlineData("bop")]
    [InlineData("unknown")]
    public void Changed_or_unknown_identity_fails_closed(string change)
    {
        var changed = change switch
        {
            "build" => Identity with { GameBuild = "other" },
            "schema" => Identity with { SchemaVersion = "other" },
            "adapter" => Identity with { AdapterVersion = "other" },
            "physics" => Identity with { PhysicsVersion = "other" },
            "bop" => Identity with { BopVersion = "other" },
            _ => Identity with { GameBuild = null },
        };
        var input = FuelSnapshot(Context with { Identity = changed }) with
        {
            DryTyreInventory = Value(ImmutableArray<DryTyreStock>.Empty),
        };
        var result = Read(input);
        Assert.False(result.Telemetry.Fuel.IsAvailable);
        Assert.False(result.DryTyreInventory.IsAvailable);
        Assert.All(result.Capabilities.Entries, entry => Assert.Equal(CapabilityStatus.Unavailable, entry.Status));
        Assert.Contains("identity", result.Telemetry.Fuel.UnavailableReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cached_inventory_retains_origin_and_time_but_cannot_cross_session_or_build()
    {
        var provenance = Provenance() with { ObservedAt = Now.AddMinutes(-1) };
        var cached = SnapshotValue<ImmutableArray<DryTyreStock>>.Available([], CapabilityStatus.Cached, provenance);
        var input = FuelSnapshot() with { DryTyreInventory = cached };
        var result = Read(input);
        Assert.Equal(CapabilityStatus.Cached, result.DryTyreInventory.Status);
        Assert.Equal(provenance, result.DryTyreInventory.Provenance);
        Assert.False(Read(input with { Context = Context with { SessionId = "session-2" } }).DryTyreInventory.IsAvailable);
        var oldBuild = provenance with { Identity = Identity with { GameBuild = "old" } };
        Assert.False(Read(input with
        {
            DryTyreInventory = SnapshotValue<ImmutableArray<DryTyreStock>>.Available([], CapabilityStatus.Cached, oldBuild),
        }).DryTyreInventory.IsAvailable);
    }

    [Fact]
    public void Cached_telemetry_cannot_be_used_as_a_live_observation()
    {
        var result = Read(FuelSnapshot() with
        {
            Telemetry = new TelemetrySnapshot
            {
                Fuel = SnapshotValue<FuelLitres>.Available(new(40), CapabilityStatus.Cached, Provenance()),
            },
        });
        Assert.False(result.Telemetry.Fuel.IsAvailable);
    }

    [Theory]
    [InlineData(CapabilityStatus.VerifiedSharedMemory, ObservationSource.SharedMemory)]
    [InlineData(CapabilityStatus.VerifiedRest, ObservationSource.Rest)]
    [InlineData(CapabilityStatus.Derived, ObservationSource.Derived)]
    [InlineData(CapabilityStatus.Derived, ObservationSource.SyntheticFixture)]
    [InlineData(CapabilityStatus.Cached, ObservationSource.Rest)]
    public void Status_and_provenance_are_retained(CapabilityStatus status, ObservationSource source)
    {
        var provenance = Provenance() with { Source = source };
        var field = SnapshotValue<FuelLitres>.Available(new(40), status, provenance);
        Assert.Equal(status, field.Status);
        Assert.Equal(provenance, field.Provenance);
    }

    [Fact]
    public void Invalid_or_fabricated_verification_and_unknown_capabilities_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => SnapshotValue<FuelLitres>.Available(new(40),
            CapabilityStatus.VerifiedRest, Provenance()));
        Assert.Throws<ArgumentOutOfRangeException>(() => SnapshotValue<FuelLitres>.Available(new(40),
            (CapabilityStatus)999, Provenance()));
        Assert.Throws<ArgumentException>(() => new CompatibleLmuSource(new ScriptedLmuSource([]),
            Identity with { SchemaVersion = null }));
        var capabilities = new LmuCapabilities(Identity, [new(CapabilityId.FuelCapacity, (CapabilityStatus)999, null)]);
        Assert.Equal(CapabilityStatus.Unavailable, capabilities.StatusOf(CapabilityId.FuelCapacity));
        Assert.Equal(CapabilityStatus.Unavailable, new LmuCapabilities(Identity, default).StatusOf(CapabilityId.Telemetry));
    }

    [Fact]
    public void Future_or_foreign_field_provenance_is_unavailable_without_hiding_valid_fields()
    {
        var input = FuelSnapshot() with
        {
            Vehicle = new VehicleSnapshot
            {
                FuelCapacity = Value(new FuelLitres(100)),
                VeCapacity = SnapshotValue<VePercent>.Available(new(100), CapabilityStatus.Derived,
                    Provenance() with { ObservedAt = Now.AddSeconds(1) }),
            },
        };
        var result = Read(input);
        Assert.True(result.Telemetry.Fuel.IsAvailable);
        Assert.True(result.Vehicle.FuelCapacity.IsAvailable);
        Assert.False(result.Vehicle.VeCapacity.IsAvailable);
    }

    [Fact]
    public void Replayed_snapshot_after_disconnect_cannot_reestablish_live_data()
    {
        var live = FuelSnapshot();
        ILmuSnapshotSource source = new CompatibleLmuSource(new ScriptedLmuSource(
            [live, live with { Connection = ConnectionStatus.Disconnected }, live,
                FuelSnapshot(Context with { CapturedAt = Now.AddSeconds(1) })]), Identity);
        Assert.True(source.ReadSnapshot().Telemetry.Fuel.IsAvailable);
        Assert.False(source.ReadSnapshot().Telemetry.Fuel.IsAvailable);
        Assert.False(source.ReadSnapshot().Telemetry.Fuel.IsAvailable);
        Assert.True(source.ReadSnapshot().Telemetry.Fuel.IsAvailable);
    }

    [Fact]
    public void Default_collections_and_empty_pit_models_cannot_claim_available_data()
    {
        var result = Read(FuelSnapshot() with
        {
            DryTyreInventory = Value(default(ImmutableArray<DryTyreStock>)),
            AvailableCompounds = Value(default(ImmutableArray<TyreCompound>)),
            Scoring = Value(default(ImmutableArray<OpponentObservation>)),
            Forecast = Value(new Forecast(default)),
            PitEstimate = Value(new PitServiceModel([])),
        });
        Assert.False(result.DryTyreInventory.IsAvailable);
        Assert.False(result.AvailableCompounds.IsAvailable);
        Assert.False(result.Scoring.IsAvailable);
        Assert.False(result.Forecast.IsAvailable);
        Assert.False(result.PitEstimate.IsAvailable);
        Assert.True(result.Telemetry.Fuel.IsAvailable);
    }

    [Fact]
    public void Contradictory_session_identity_is_unavailable()
    {
        var result = Read(FuelSnapshot() with
        {
            Session = new SessionSnapshot
            {
                Identity = Value(new RaceIdentity("other-session", "driver", "car", "class", "track", "layout")),
            },
        });
        Assert.False(result.Session.Identity.IsAvailable);
    }

    [Fact]
    public void All_contract_sections_survive_a_valid_read_and_fail_closed_together()
    {
        var compound = new TyreCompound("event-dry", TyreSurface.Dry);
        var weather = new WeatherState(RoadConditionFamily.Dry, null, null, null, null);
        var tyres = new PerWheel<TyreState>(new(null, null, null), new(null, null, null),
            new(null, null, null), new(null, null, null));
        var full = new LmuSnapshot(Context, ConnectionStatus.Connected)
        {
            Session = new SessionSnapshot
            {
                Identity = Value(new RaceIdentity(Context.SessionId, "driver", "car", "class", "track", "layout")),
                Duration = Value(new Seconds(3600)), ElapsedTime = Value(new Seconds(10)),
                RemainingTime = Value(new Seconds(3590)), CompletedLaps = Value(new Laps(0)),
                RaceControl = Value(RaceControlState.Green), Weather = Value(weather),
            },
            Vehicle = new VehicleSnapshot { FuelCapacity = Value(new FuelLitres(100)), VeCapacity = Value(new VePercent(100)) },
            Telemetry = new TelemetrySnapshot
            {
                Fuel = Value(new FuelLitres(40)), Ve = Value(new VePercent(90)),
                FuelRatio = Value(new FuelRatio(1)), Tyres = Value(tyres),
            },
            Scoring = Value(ImmutableArray.Create(new OpponentObservation("opponent", "car", "class", new(1),
                new(120), [new Seconds(40), null, null], null, null, RaceControlState.Unknown))),
            Forecast = Value(new Forecast([new(new Seconds(60), weather, ModelEvidence.Unavailable)])),
            DryTyreInventory = Value(ImmutableArray.Create(new DryTyreStock(compound, new(4), null))),
            AvailableCompounds = Value(ImmutableArray.Create(compound)),
            RecommendedCompoundConditions = Value(ImmutableArray.Create(new CompoundConditionRecommendation(compound, RoadConditionFamily.Dry))),
            TyreServiceOptions = Value(ImmutableArray.Create(TyreServicePattern.Fronts)),
            PitEstimate = Value(new PitServiceModel([new(new(20), new(10), new(1),
                new(null, null, null, null), new(20), new(15), new(2), ModelEvidence.Unavailable)])),
        };
        var valid = Read(full);
        Assert.Equal(3600, valid.Session.Duration.Value.Value);
        Assert.Equal(100, valid.Vehicle.FuelCapacity.Value.Value);
        Assert.Equal(40, valid.Telemetry.Fuel.Value.Value);
        Assert.Null(Assert.Single(valid.Scoring.Value).Compound);
        Assert.Equal(CapabilityStatus.Unavailable, valid.Capabilities.StatusOf(CapabilityId.OpponentCompound));
        Assert.Single(valid.Forecast.Value.Points);
        Assert.Single(valid.PitEstimate.Value.Estimates);
        Assert.Single(valid.AvailableCompounds.Value);
        Assert.Single(valid.RecommendedCompoundConditions.Value);
        Assert.Single(valid.TyreServiceOptions.Value);
        Assert.Single(valid.DryTyreInventory.Value);

        foreach (var invalid in new[]
        {
            Read(full with { Connection = ConnectionStatus.Disconnected }),
            Read(full with { Context = Context with { Identity = Identity with { SchemaVersion = "unknown" } } }),
        })
        {
            Assert.False(invalid.Session.Duration.IsAvailable);
            Assert.False(invalid.Session.Identity.IsAvailable);
            Assert.False(invalid.Session.ElapsedTime.IsAvailable);
            Assert.False(invalid.Session.RemainingTime.IsAvailable);
            Assert.False(invalid.Session.CompletedLaps.IsAvailable);
            Assert.False(invalid.Session.RaceControl.IsAvailable);
            Assert.False(invalid.Session.Weather.IsAvailable);
            Assert.False(invalid.Vehicle.FuelCapacity.IsAvailable);
            Assert.False(invalid.Vehicle.VeCapacity.IsAvailable);
            Assert.False(invalid.Telemetry.Fuel.IsAvailable);
            Assert.False(invalid.Telemetry.Ve.IsAvailable);
            Assert.False(invalid.Telemetry.FuelRatio.IsAvailable);
            Assert.False(invalid.Telemetry.Tyres.IsAvailable);
            Assert.False(invalid.Scoring.IsAvailable);
            Assert.False(invalid.Forecast.IsAvailable);
            Assert.False(invalid.PitEstimate.IsAvailable);
            Assert.False(invalid.AvailableCompounds.IsAvailable);
            Assert.False(invalid.RecommendedCompoundConditions.IsAvailable);
            Assert.False(invalid.TyreServiceOptions.IsAvailable);
            Assert.False(invalid.DryTyreInventory.IsAvailable);
        }
    }

    private static LmuSnapshot FuelSnapshot(SnapshotContext? context = null, FuelLitres? fuel = null)
    {
        var actualContext = context ?? Context;
        return new(actualContext, ConnectionStatus.Connected)
        {
            Telemetry = new TelemetrySnapshot
            {
                Fuel = SnapshotValue<FuelLitres>.Available(fuel ?? new(40), CapabilityStatus.Derived,
                    Provenance() with { Identity = actualContext.Identity, SessionId = actualContext.SessionId,
                        ObservedAt = actualContext.CapturedAt }),
            },
        };
    }

    private static ValueProvenance Provenance() => new(Identity, Context.SessionId, Now,
        ObservationSource.SyntheticFixture, "contract fixture", "explicit synthetic units");

    private static SnapshotValue<T> Value<T>(T value) where T : notnull =>
        SnapshotValue<T>.Available(value, CapabilityStatus.Derived, Provenance());

    private static LmuSnapshot Read(LmuSnapshot snapshot) =>
        new CompatibleLmuSource(new ScriptedLmuSource([snapshot]), Identity).ReadSnapshot();
}
