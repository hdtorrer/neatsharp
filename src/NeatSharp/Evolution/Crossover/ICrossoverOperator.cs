using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Crossover;

/// <summary>
/// Combines two parent genomes into a single offspring genome using
/// innovation-number-aligned gene inheritance.
/// </summary>
public interface ICrossoverOperator
{
    /// <summary>
    /// Performs crossover between two parent genomes.
    /// </summary>
    /// <param name="parent1">First parent genome.</param>
    /// <param name="parent1Fitness">Fitness score of the first parent.</param>
    /// <param name="parent2">Second parent genome.</param>
    /// <param name="parent2Fitness">Fitness score of the second parent.</param>
    /// <param name="random">Seeded RNG for deterministic behavior.</param>
    /// <returns>A new offspring genome.</returns>
    Genome Cross(
        Genome parent1, double parent1Fitness,
        Genome parent2, double parent2Fitness,
        Random random);
}
