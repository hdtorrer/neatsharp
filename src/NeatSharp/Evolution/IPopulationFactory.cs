using NeatSharp.Genetics;

namespace NeatSharp.Evolution;

/// <summary>
/// Creates the initial population of genomes for generation 0.
/// </summary>
/// <remarks>
/// The default implementation creates minimal-topology genomes (input + bias
/// nodes connected directly to output nodes) with randomized weights.
/// Consumers can replace this via DI to customize initial population structure.
/// </remarks>
public interface IPopulationFactory
{
    /// <summary>
    /// Creates the initial population of genomes.
    /// </summary>
    /// <param name="populationSize">Number of genomes to create.</param>
    /// <param name="inputCount">Number of input nodes per genome.</param>
    /// <param name="outputCount">Number of output nodes per genome.</param>
    /// <param name="random">Seeded RNG for deterministic weight initialization.</param>
    /// <param name="tracker">
    /// Innovation tracker for assigning connection innovation numbers.
    /// All genomes share the same topology, so connections get identical
    /// innovation numbers via the tracker's dedup cache.
    /// </param>
    /// <returns>A list of genomes with minimal topology and randomized weights.</returns>
    IReadOnlyList<Genome> CreateInitialPopulation(
        int populationSize,
        int inputCount,
        int outputCount,
        Random random,
        IInnovationTracker tracker);
}
