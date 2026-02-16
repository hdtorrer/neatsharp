namespace NeatSharp.Serialization;

/// <summary>
/// Captures the internal state of a <see cref="Random"/> instance for deterministic
/// checkpoint/resume. Stores the three fields from the .NET runtime's Net5CompatSeedImpl.
/// </summary>
public sealed class RngState
{
    /// <summary>
    /// The required length of <see cref="SeedArray"/>.
    /// </summary>
    public const int SeedArrayLength = 56;

    /// <summary>
    /// Gets the 56-element seed array.
    /// </summary>
    public int[] SeedArray { get; }

    /// <summary>
    /// Gets the inext index (in [0, 55]).
    /// </summary>
    public int Inext { get; }

    /// <summary>
    /// Gets the inextp index (in [0, 55]).
    /// </summary>
    public int Inextp { get; }

    /// <summary>
    /// Initializes a new validated <see cref="RngState"/> instance.
    /// </summary>
    /// <param name="seedArray">The 56-element seed array.</param>
    /// <param name="inext">The inext index (must be in [0, 55]).</param>
    /// <param name="inextp">The inextp index (must be in [0, 55]).</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="seedArray"/> length is not 56 or
    /// <paramref name="inext"/>/<paramref name="inextp"/> is not in [0, 55].
    /// </exception>
    public RngState(int[] seedArray, int inext, int inextp)
    {
        ArgumentNullException.ThrowIfNull(seedArray);

        if (seedArray.Length != SeedArrayLength)
        {
            throw new ArgumentException(
                $"SeedArray must have exactly {SeedArrayLength} elements, but has {seedArray.Length}.",
                nameof(seedArray));
        }

        if (inext < 0 || inext > SeedArrayLength - 1)
        {
            throw new ArgumentException(
                $"Inext must be in [0, {SeedArrayLength - 1}], but was {inext}.",
                nameof(inext));
        }

        if (inextp < 0 || inextp > SeedArrayLength - 1)
        {
            throw new ArgumentException(
                $"Inextp must be in [0, {SeedArrayLength - 1}], but was {inextp}.",
                nameof(inextp));
        }

        SeedArray = (int[])seedArray.Clone();
        Inext = inext;
        Inextp = inextp;
    }
}
