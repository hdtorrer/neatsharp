using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Selection;

/// <summary>
/// Selects a parent genome from a pool of candidates for reproduction.
/// </summary>
public interface IParentSelector
{
    /// <summary>
    /// Selects a single parent from the given candidates.
    /// </summary>
    /// <param name="candidates">
    /// Eligible members of a species with their fitness scores.
    /// Guaranteed to have at least one member.
    /// </param>
    /// <param name="random">Seeded RNG for deterministic behavior.</param>
    /// <returns>The selected parent genome.</returns>
    Genome Select(IReadOnlyList<(Genome Genome, double Fitness)> candidates, Random random);
}
