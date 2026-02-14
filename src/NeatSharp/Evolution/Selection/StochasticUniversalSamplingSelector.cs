using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Selection;

/// <summary>
/// Selects a parent using Stochastic Universal Sampling (SUS).
/// Uses a single random start point with evenly spaced pointers for more
/// uniform selection compared to roulette wheel.
/// </summary>
/// <remarks>
/// When called repeatedly with the same <see cref="Random"/> instance,
/// each call selects one parent using a single pointer at a random start
/// position within [0, totalFitness), providing the SUS spacing benefit
/// across sequential calls.
/// </remarks>
public sealed class StochasticUniversalSamplingSelector : IParentSelector
{
    private const double Epsilon = 1e-6;

    /// <inheritdoc />
    public Genome Select(IReadOnlyList<(Genome Genome, double Fitness)> candidates, Random random)
    {
        if (candidates.Count == 1)
            return candidates[0].Genome;

        double minFitness = candidates[0].Fitness;
        for (int i = 1; i < candidates.Count; i++)
        {
            if (candidates[i].Fitness < minFitness)
                minFitness = candidates[i].Fitness;
        }

        double shift = minFitness <= 0 ? Math.Abs(minFitness) + Epsilon : 0.0;

        double totalFitness = 0.0;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalFitness += candidates[i].Fitness + shift;
        }

        // Single pointer: spacing = totalFitness (selecting 1 out of N)
        // Start at random position in [0, totalFitness)
        double pointer = random.NextDouble() * totalFitness;
        double accumulated = 0.0;

        for (int i = 0; i < candidates.Count; i++)
        {
            accumulated += candidates[i].Fitness + shift;
            if (accumulated > pointer)
                return candidates[i].Genome;
        }

        // Fallback for floating-point edge case
        return candidates[^1].Genome;
    }
}
