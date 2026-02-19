using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Selection;

/// <summary>
/// Selects a parent with probability proportional to fitness.
/// If any fitness value is less than or equal to zero, all values are shifted
/// by |min| + epsilon to ensure positive selection weights.
/// </summary>
public sealed class RouletteWheelSelector : IParentSelector
{
    private const double Epsilon = 1e-6;

    /// <inheritdoc />
    public Genome Select(IReadOnlyList<(Genome Genome, double Fitness)> candidates, Random random)
    {
        if (candidates.Count == 1)
        {
            return candidates[0].Genome;
        }

        double minFitness = candidates[0].Fitness;
        for (int i = 1; i < candidates.Count; i++)
        {
            if (candidates[i].Fitness < minFitness)
            {
                minFitness = candidates[i].Fitness;
            }
        }

        double shift = minFitness <= 0 ? Math.Abs(minFitness) + Epsilon : 0.0;

        double totalFitness = 0.0;
        for (int i = 0; i < candidates.Count; i++)
        {
            totalFitness += candidates[i].Fitness + shift;
        }

        double roll = random.NextDouble() * totalFitness;
        double accumulated = 0.0;

        for (int i = 0; i < candidates.Count; i++)
        {
            accumulated += candidates[i].Fitness + shift;
            if (accumulated > roll)
            {
                return candidates[i].Genome;
            }
        }

        // Fallback for floating-point edge case
        return candidates[^1].Genome;
    }
}
