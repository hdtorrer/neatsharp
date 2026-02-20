using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Speciation;

/// <summary>
/// Assigns genomes to species using compatibility distance thresholding.
/// Each genome joins the first species whose representative is within the
/// compatibility threshold, or creates a new species if none match.
/// </summary>
public sealed class CompatibilitySpeciation : ISpeciationStrategy
{
    private readonly NeatSharpOptions _options;
    private readonly ICompatibilityDistance _distanceCalculator;
    private int _nextSpeciesId = 1;

    public CompatibilitySpeciation(
        IOptions<NeatSharpOptions> options,
        ICompatibilityDistance distanceCalculator)
    {
        _options = options.Value;
        _distanceCalculator = distanceCalculator;
    }

    /// <inheritdoc />
    public int NextSpeciesId => _nextSpeciesId;

    /// <inheritdoc />
    public void RestoreState(int nextSpeciesId)
    {
        _nextSpeciesId = nextSpeciesId;
    }

    /// <inheritdoc />
    public void Speciate(
        IReadOnlyList<(Genome Genome, double Fitness)> population,
        List<Species> species)
    {
        var threshold = _options.Speciation.CompatibilityThreshold;

        // Update _nextSpeciesId to be beyond any existing species IDs
        if (species.Count > 0)
        {
            int maxExistingId = 0;
            foreach (var s in species)
            {
                if (s.Id > maxExistingId)
                {
                    maxExistingId = s.Id;
                }
            }
            if (_nextSpeciesId <= maxExistingId)
            {
                _nextSpeciesId = maxExistingId + 1;
            }
        }

        // Step 1: Clear members but preserve metadata
        foreach (var s in species)
        {
            s.Members.Clear();
        }

        // Step 2: Assign each genome to a species
        foreach (var (genome, fitness) in population)
        {
            bool assigned = false;

            foreach (var s in species)
            {
                double distance = _distanceCalculator.Compute(genome, s.Representative);
                if (distance <= threshold)
                {
                    s.Members.Add((genome, fitness));
                    assigned = true;
                    break;
                }
            }

            if (!assigned)
            {
                var newSpecies = new Species(_nextSpeciesId++, genome);
                newSpecies.Members.Add((genome, fitness));
                species.Add(newSpecies);
            }
        }

        // Step 3: Remove empty species
        species.RemoveAll(s => s.Members.Count == 0);

        // Step 4: Update representatives and stagnation counters
        foreach (var s in species)
        {
            var bestMember = s.Members[0];
            for (int i = 1; i < s.Members.Count; i++)
            {
                if (s.Members[i].Fitness > bestMember.Fitness)
                {
                    bestMember = s.Members[i];
                }
            }

            s.Representative = bestMember.Genome;

            if (bestMember.Fitness > s.BestFitnessEver)
            {
                s.BestFitnessEver = bestMember.Fitness;
                s.GenerationsSinceImprovement = 0;
            }
            else
            {
                s.GenerationsSinceImprovement++;
            }
        }
    }
}
