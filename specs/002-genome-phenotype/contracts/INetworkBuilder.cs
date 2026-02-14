// Contract definition — not compilable source code.
// Defines the network builder interface for phenotype construction.

namespace NeatSharp.Genetics;

/// <summary>
/// Converts a <see cref="Genome"/> (genotype) into an executable
/// <see cref="IGenome"/> (phenotype) for feed-forward inference.
/// </summary>
/// <remarks>
/// The builder performs topological sorting, cycle detection, and
/// reachability analysis during construction. The resulting network
/// evaluates nodes in topological order from inputs to outputs.
/// </remarks>
public interface INetworkBuilder
{
    /// <summary>
    /// Builds an executable feed-forward network from the specified genome.
    /// </summary>
    /// <param name="genome">The genome to convert.</param>
    /// <returns>
    /// An <see cref="IGenome"/> implementation that can be activated
    /// for feed-forward inference.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="genome"/> is null.
    /// </exception>
    /// <exception cref="CycleDetectedException">
    /// Thrown when the genome's enabled connections form a cycle,
    /// making feed-forward evaluation impossible.
    /// </exception>
    IGenome Build(Genome genome);
}
