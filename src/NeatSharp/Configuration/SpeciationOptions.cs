namespace NeatSharp.Configuration;

/// <summary>
/// Configuration for compatibility distance and speciation.
/// Coefficients correspond to c1, c2, c3 in the NEAT compatibility distance formula.
/// Defaults are sourced from the original NEAT paper (Stanley &amp; Miikkulainen, 2002).
/// </summary>
public class SpeciationOptions
{
    /// <summary>
    /// Coefficient for excess genes (c1) in the compatibility distance formula.
    /// Must be non-negative.
    /// </summary>
    public double ExcessCoefficient { get; set; } = 1.0;

    /// <summary>
    /// Coefficient for disjoint genes (c2) in the compatibility distance formula.
    /// Must be non-negative.
    /// </summary>
    public double DisjointCoefficient { get; set; } = 1.0;

    /// <summary>
    /// Coefficient for average weight difference (c3) in the compatibility distance formula.
    /// Must be non-negative.
    /// </summary>
    public double WeightDifferenceCoefficient { get; set; } = 0.4;

    /// <summary>
    /// Maximum compatibility distance for same-species assignment.
    /// Must be greater than 0.
    /// </summary>
    public double CompatibilityThreshold { get; set; } = 3.0;
}
