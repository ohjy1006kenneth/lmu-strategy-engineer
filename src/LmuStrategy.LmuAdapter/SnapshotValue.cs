using System.Diagnostics.CodeAnalysis;
using LmuStrategy.Domain;

namespace LmuStrategy.LmuAdapter;

public enum ObservationSource { SharedMemory, Rest, Derived, SyntheticFixture }

/// <summary>Source semantics are a field/mapping reference or a derivation description, never guessed units.</summary>
public sealed record ValueProvenance(IntegrationIdentity Identity, string SessionId,
    DateTimeOffset ObservedAt, ObservationSource Source, string SourceReference, string Semantics);

/// <summary>
/// An explicit value or an unavailable reason. Cached values retain their original source and observation time.
/// Synthetic fixtures may be Derived or Cached, but cannot claim verified LMU provenance.
/// </summary>
public sealed class SnapshotValue<T> where T : notnull
{
    private readonly T? value;
    public CapabilityStatus Status { get; }
    public ValueProvenance? Provenance { get; }
    public string? UnavailableReason { get; }
    public bool IsAvailable => Status != CapabilityStatus.Unavailable;
    public T Value => IsAvailable ? value! : throw new InvalidOperationException(UnavailableReason);

    private SnapshotValue(T? value, CapabilityStatus status, ValueProvenance? provenance, string? reason)
    {
        this.value = value;
        Status = status;
        Provenance = provenance;
        UnavailableReason = reason;
    }

    public static SnapshotValue<T> Unavailable(string reason = "Not supplied by the source")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(default, CapabilityStatus.Unavailable, null, reason);
    }

    public static SnapshotValue<T> Available(T value, CapabilityStatus status, ValueProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(provenance.Identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance.SessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance.SourceReference);
        ArgumentException.ThrowIfNullOrWhiteSpace(provenance.Semantics);
        if (!Enum.IsDefined(status) || status == CapabilityStatus.Unavailable)
            throw new ArgumentOutOfRangeException(nameof(status));
        if (!Enum.IsDefined(provenance.Source)) throw new ArgumentOutOfRangeException(nameof(provenance));
        bool matches = status switch
        {
            CapabilityStatus.VerifiedSharedMemory => provenance.Source == ObservationSource.SharedMemory,
            CapabilityStatus.VerifiedRest => provenance.Source == ObservationSource.Rest,
            CapabilityStatus.Derived => provenance.Source is ObservationSource.Derived or ObservationSource.SyntheticFixture,
            CapabilityStatus.Cached => true,
            _ => false,
        };
        if (!matches) throw new ArgumentException("Status must match the declared source.", nameof(provenance));
        return new(value, status, provenance, null);
    }

    public bool TryGetValue([MaybeNullWhen(false)] out T result)
    {
        result = value!;
        return IsAvailable;
    }

    internal SnapshotValue<T> Require(Func<T, bool> valid, string reason) =>
        !IsAvailable || valid(Value) ? this : Unavailable(reason);

    internal SnapshotValue<T> Validate(SnapshotContext context, bool allowCached, string? failure)
    {
        if (failure is not null) return Unavailable(failure);
        if (!IsAvailable) return this;
        var evidence = Provenance!;
        if (evidence.Identity != context.Identity || evidence.SessionId != context.SessionId)
            return Unavailable("Field provenance belongs to a different identity or session");
        if (evidence.ObservedAt > context.CapturedAt)
            return Unavailable("Field observation is later than the snapshot");
        if (Status == CapabilityStatus.Cached)
            return allowCached ? this : Unavailable("Cached live observations cannot be used as current data");
        return evidence.ObservedAt == context.CapturedAt
            ? this : Unavailable("Older observations must be explicitly classified as cached");
    }
}
