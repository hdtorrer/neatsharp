namespace NeatSharp.Serialization.Dto;

/// <summary>
/// Data transfer object for <see cref="RngState"/> serialization.
/// </summary>
public class RngStateDto
{
    /// <summary>
    /// Gets or sets the 56-element seed array.
    /// </summary>
    public int[] SeedArray { get; set; } = [];

    /// <summary>
    /// Gets or sets the inext index.
    /// </summary>
    public int Inext { get; set; }

    /// <summary>
    /// Gets or sets the inextp index.
    /// </summary>
    public int Inextp { get; set; }

    /// <summary>
    /// Maps an <see cref="RngState"/> domain object to a <see cref="RngStateDto"/>.
    /// </summary>
    /// <param name="state">The RNG state to map.</param>
    /// <returns>A new DTO representing the RNG state.</returns>
    public static RngStateDto ToDto(RngState state) => new()
    {
        SeedArray = (int[])state.SeedArray.Clone(),
        Inext = state.Inext,
        Inextp = state.Inextp
    };

    /// <summary>
    /// Maps this DTO back to an <see cref="RngState"/> domain object.
    /// </summary>
    /// <returns>A new <see cref="RngState"/> instance.</returns>
    public RngState ToDomain() => new(
        (int[])SeedArray.Clone(),
        Inext,
        Inextp);
}
