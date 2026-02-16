using System.Reflection;

namespace NeatSharp.Serialization;

/// <summary>
/// Captures and restores the internal state of a <see cref="Random"/> instance
/// using reflection to access the .NET runtime's private implementation fields.
/// </summary>
/// <remarks>
/// <para>
/// Access path: Random._impl -> Net5CompatSeedImpl._prng -> CompatPrng._seedArray/_inext/_inextp.
/// </para>
/// <para>
/// CompatPrng is a value type (struct), so reading/writing requires careful handling
/// to avoid working on a boxed copy.
/// </para>
/// </remarks>
public static class RngStateHelper
{
    private const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly Lazy<FieldInfo> ImplFieldLazy = new(() =>
        typeof(Random).GetField("_impl", NonPublicInstance)
        ?? throw new InvalidOperationException(
            $"Cannot find Random._impl field on .NET {Environment.Version}. " +
            "Runtime internals may have changed. RNG state capture/restore requires " +
            ".NET 8.0 or .NET 9.0 with the Net5CompatSeedImpl implementation."));

    private static readonly Lazy<(FieldInfo PrngField, FieldInfo SeedArrayField, FieldInfo InextField, FieldInfo InextpField)> Fields = new(DiscoverFields);

    /// <summary>
    /// Captures the internal RNG state from the specified <see cref="Random"/> instance.
    /// </summary>
    /// <param name="random">The Random instance to capture state from.</param>
    /// <returns>An <see cref="RngState"/> containing a copy of the internal state.</returns>
    /// <exception cref="InvalidOperationException">Thrown when reflection access fails due to runtime changes.</exception>
    public static RngState Capture(Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var (prngField, seedArrayField, inextField, inextpField) = Fields.Value;

        var impl = ImplFieldLazy.Value.GetValue(random)
            ?? throw new InvalidOperationException("Random._impl is null.");

        // Read the CompatPrng struct (boxed copy is fine for reading)
        var prng = prngField.GetValue(impl)
            ?? throw new InvalidOperationException("Net5CompatSeedImpl._prng is null.");

        var seedArray = (int[])(seedArrayField.GetValue(prng)
            ?? throw new InvalidOperationException("CompatPrng._seedArray is null."));
        var inext = (int)(inextField.GetValue(prng)
            ?? throw new InvalidOperationException("CompatPrng._inext is null."));
        var inextp = (int)(inextpField.GetValue(prng)
            ?? throw new InvalidOperationException("CompatPrng._inextp is null."));

        // Return a defensive copy of the seed array
        return new RngState((int[])seedArray.Clone(), inext, inextp);
    }

    /// <summary>
    /// Restores the internal RNG state of the specified <see cref="Random"/> instance.
    /// </summary>
    /// <param name="random">The Random instance to restore state to.</param>
    /// <param name="state">The RNG state to restore.</param>
    /// <exception cref="InvalidOperationException">Thrown when reflection access fails due to runtime changes.</exception>
    public static void Restore(Random random, RngState state)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(state);

        var (prngField, seedArrayField, inextField, inextpField) = Fields.Value;

        var impl = ImplFieldLazy.Value.GetValue(random)
            ?? throw new InvalidOperationException("Random._impl is null.");

        // Read the CompatPrng struct (boxed), modify it, then write it back.
        // Since CompatPrng is a value type, we must get a boxed copy, modify it,
        // and then set it back on the parent object.
        var prng = prngField.GetValue(impl)
            ?? throw new InvalidOperationException("Net5CompatSeedImpl._prng is null.");

        // Get the existing seed array reference and copy values into it
        var targetSeedArray = (int[])(seedArrayField.GetValue(prng)
            ?? throw new InvalidOperationException("CompatPrng._seedArray is null."));
        Array.Copy(state.SeedArray, targetSeedArray, RngState.SeedArrayLength);

        // Update the boxed struct fields
        seedArrayField.SetValue(prng, targetSeedArray);
        inextField.SetValue(prng, state.Inext);
        inextpField.SetValue(prng, state.Inextp);

        // Write the modified boxed struct back to the impl object
        prngField.SetValue(impl, prng);
    }

    private static (FieldInfo PrngField, FieldInfo SeedArrayField, FieldInfo InextField, FieldInfo InextpField) DiscoverFields()
    {
        var impl = ImplFieldLazy.Value.GetValue(new Random(0))
            ?? throw new InvalidOperationException("Random._impl is null.");

        var prngField = impl.GetType().GetField("_prng", NonPublicInstance)
            ?? throw new InvalidOperationException(
                $"Cannot find _prng field on {impl.GetType().Name}. Runtime internals may have changed.");

        var prngType = prngField.FieldType;

        var seedArrayField = prngType.GetField("_seedArray", NonPublicInstance)
            ?? throw new InvalidOperationException(
                $"Cannot find _seedArray field on {prngType.Name}. Runtime internals may have changed.");

        var inextField = prngType.GetField("_inext", NonPublicInstance)
            ?? throw new InvalidOperationException(
                $"Cannot find _inext field on {prngType.Name}. Runtime internals may have changed.");

        var inextpField = prngType.GetField("_inextp", NonPublicInstance)
            ?? throw new InvalidOperationException(
                $"Cannot find _inextp field on {prngType.Name}. Runtime internals may have changed.");

        return (prngField, seedArrayField, inextField, inextpField);
    }
}
