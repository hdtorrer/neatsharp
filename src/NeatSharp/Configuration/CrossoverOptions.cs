namespace NeatSharp.Configuration;

/// <summary>
/// Configuration for crossover operations.
/// Defaults are sourced from the original NEAT paper (Stanley &amp; Miikkulainen, 2002).
/// </summary>
public class CrossoverOptions
{
    /// <summary>
    /// Fraction of offspring produced via crossover. The remainder are cloned and mutated.
    /// Must be in [0.0, 1.0].
    /// </summary>
    public double CrossoverRate { get; set; } = 0.75;

    /// <summary>
    /// Probability that the second parent comes from a different species during crossover.
    /// Must be in [0.0, 1.0].
    /// </summary>
    public double InterspeciesCrossoverRate { get; set; } = 0.001;

    /// <summary>
    /// When a matching gene is disabled in either parent, probability it's disabled in offspring.
    /// Must be in [0.0, 1.0].
    /// </summary>
    public double DisabledGeneInheritanceProbability { get; set; } = 0.75;
}
