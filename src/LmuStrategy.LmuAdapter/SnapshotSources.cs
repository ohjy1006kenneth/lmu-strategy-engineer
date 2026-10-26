using System.Collections.Immutable;
using LmuStrategy.Domain;

namespace LmuStrategy.LmuAdapter;

/// <summary>Reads one atomic snapshot. Transport implementations must translate disconnects to explicit snapshots.</summary>
public interface ILmuSnapshotSource
{
    LmuSnapshot ReadSnapshot();
}

/// <summary>
/// A portable boundary for a mapping verified against one exact integration identity.
/// This validates declared identity/provenance; it does not verify real LMU fields or parse native schemas.
/// Capture times must strictly advance, including after reconnect. Single-consumer only.
/// </summary>
public sealed class CompatibleLmuSource : ILmuSnapshotSource
{
    private readonly ILmuSnapshotSource source;
    private readonly IntegrationIdentity acceptedIdentity;
    private DateTimeOffset? lastAcceptedCapture;

    public CompatibleLmuSource(ILmuSnapshotSource source, IntegrationIdentity acceptedIdentity)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(acceptedIdentity);
        if (!KnownIdentity(acceptedIdentity))
            throw new ArgumentException("A known game build, adapter version and schema version are required.", nameof(acceptedIdentity));
        this.source = source;
        this.acceptedIdentity = acceptedIdentity;
    }

    public LmuSnapshot ReadSnapshot()
    {
        var snapshot = source.ReadSnapshot();
        string? failure = snapshot.Connection != ConnectionStatus.Connected ? "Source disconnected" :
            !KnownIdentity(snapshot.Context.Identity) || snapshot.Context.Identity != acceptedIdentity
                ? "Unknown or incompatible integration identity" :
            string.IsNullOrWhiteSpace(snapshot.Context.SessionId) ? "Unknown session instance" :
            lastAcceptedCapture is { } previous && snapshot.Context.CapturedAt <= previous
                ? "Snapshot capture time did not advance; replayed observations are unavailable" : null;
        if (failure is null) lastAcceptedCapture = snapshot.Context.CapturedAt;
        return snapshot.Validate(failure);
    }

    private static bool KnownIdentity(IntegrationIdentity? identity) =>
        identity is not null && !string.IsNullOrWhiteSpace(identity.GameBuild) &&
        !string.IsNullOrWhiteSpace(identity.AdapterVersion) &&
        !string.IsNullOrWhiteSpace(identity.SchemaVersion);
}

/// <summary>
/// Deterministic fake: consumes the script once, then reports disconnect without replaying values.
/// It never opens LMU, native handles, WPF, storage or a network connection. Single-consumer only.
/// Wrap this in CompatibleLmuSource just like a future real transport.
/// </summary>
public sealed class ScriptedLmuSource : ILmuSnapshotSource
{
    private readonly ImmutableArray<LmuSnapshot> snapshots;
    private readonly SnapshotContext finalContext;
    private int index;

    public ScriptedLmuSource(IEnumerable<LmuSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        this.snapshots = snapshots.ToImmutableArray();
        if (this.snapshots.Any(snapshot => snapshot is null))
            throw new ArgumentException("A script cannot contain null snapshots.", nameof(snapshots));
        finalContext = this.snapshots.IsEmpty
            ? new(new(null, null, null, null, null), "", DateTimeOffset.MinValue)
            : this.snapshots[^1].Context;
    }

    public LmuSnapshot ReadSnapshot() => index < snapshots.Length
        ? snapshots[index++] : new(finalContext, ConnectionStatus.Disconnected);
}
