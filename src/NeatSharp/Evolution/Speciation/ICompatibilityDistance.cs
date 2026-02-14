using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Speciation;

/// <summary>
/// Computes the structural and parametric distance between two genomes.
/// </summary>
public interface ICompatibilityDistance
{
    /// <summary>
    /// Computes the compatibility distance between two genomes.
    /// </summary>
    /// <param name="genome1">First genome.</param>
    /// <param name="genome2">Second genome.</param>
    /// <returns>A non-negative distance value.</returns>
    double Compute(Genome genome1, Genome genome2);
}
