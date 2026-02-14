using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Mutation;

/// <summary>
/// Applies a single type of mutation to a genome, producing a new immutable genome.
/// The original genome is never modified.
/// </summary>
public interface IMutationOperator
{
    /// <summary>
    /// Applies this mutation to the given genome.
    /// </summary>
    /// <param name="genome">The source genome. Not modified.</param>
    /// <param name="random">Seeded RNG for deterministic behavior.</param>
    /// <param name="tracker">Innovation tracker for structural mutations.</param>
    /// <returns>
    /// A new genome with the mutation applied, or the original genome unchanged
    /// if the mutation was not applicable (e.g., complexity limits reached, fully connected).
    /// </returns>
    Genome Mutate(Genome genome, Random random, IInnovationTracker tracker);
}
