namespace LmuStrategy.Domain;

/// <summary>Four distinct corners. Updating a corner with 'with' creates a new snapshot.</summary>
public sealed record PerWheel<T>(T FL, T FR, T RL, T RR);

public enum WheelPosition { FL, FR, RL, RR }
public enum TyreSurface { Dry, Wet }

/// <summary>An opaque event-specific compound ID, with no implied Soft/Medium/Hard pace ordering.</summary>
public sealed record TyreCompound(string Id, TyreSurface Surface);

/// <summary>Unknown observations remain null; age is laps on this individual tyre.</summary>
public sealed record TyreState(TyreCompound? Compound, TyreRemainingPercent? RemainingLife, Laps? Age);

/// <summary>Null per-corner replacement means keep that tyre; a value names the replacement compound.</summary>
public sealed record TyreReplacement(TyreCompound Compound);

[Flags]
public enum TyreServicePattern { None = 0, FL = 1, FR = 2, RL = 4, RR = 8, Fronts = FL | FR, All = FL | FR | RL | RR }

/// <summary>
/// Available individual dry tyres of a compound. Null RestrictedTo means a shared pool;
/// otherwise this stock is usable at that corner only. Wet tyres have no entry in a dry inventory.
/// </summary>
public sealed record DryTyreStock
{
    public TyreCompound Compound { get; }
    public TyreCount Available { get; }
    public WheelPosition? RestrictedTo { get; }

    public DryTyreStock(TyreCompound compound, TyreCount available, WheelPosition? restrictedTo)
    {
        ArgumentNullException.ThrowIfNull(compound);
        if (compound.Surface != TyreSurface.Dry)
            throw new ArgumentException("Wet tyres do not belong to the dry inventory.", nameof(compound));
        Compound = compound;
        Available = available;
        RestrictedTo = restrictedTo;
    }
}
