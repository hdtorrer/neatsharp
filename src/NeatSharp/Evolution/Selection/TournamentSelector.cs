using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Selection;

/// <summary>
/// Selects a parent by picking <see cref="SelectionOptions.TournamentSize"/> random candidates
/// (with replacement) and returning the one with the highest fitness.
/// </summary>
public sealed class TournamentSelector : IParentSelector
{
    private readonly NeatSharpOptions _options;

    public TournamentSelector(IOptions<NeatSharpOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public Genome Select(IReadOnlyList<(Genome Genome, double Fitness)> candidates, Random random)
    {
        int tournamentSize = _options.Selection.TournamentSize;
        int bestIndex = random.Next(candidates.Count);
        double bestFitness = candidates[bestIndex].Fitness;

        for (int i = 1; i < tournamentSize; i++)
        {
            int idx = random.Next(candidates.Count);
            if (candidates[idx].Fitness > bestFitness)
            {
                bestIndex = idx;
                bestFitness = candidates[idx].Fitness;
            }
        }

        return candidates[bestIndex].Genome;
    }
}
