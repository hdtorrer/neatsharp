using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Crossover;
using NeatSharp.Evolution.Mutation;
using NeatSharp.Evolution.Speciation;
using NeatSharp.Genetics;

namespace NeatSharp.Evolution.Selection;

/// <summary>
/// Orchestrates the per-offspring reproduction loop: applies crossover rate,
/// interspecies crossover, survival threshold filtering, elitism, and mutation
/// to produce the next generation of genomes.
/// </summary>
public sealed class ReproductionOrchestrator
{
    private readonly NeatSharpOptions _options;
    private readonly IParentSelector _parentSelector;
    private readonly ICrossoverOperator _crossoverOperator;
    private readonly CompositeMutationOperator _mutationOperator;
    private readonly ReproductionAllocator _allocator;

    public ReproductionOrchestrator(
        IOptions<NeatSharpOptions> options,
        IParentSelector parentSelector,
        ICrossoverOperator crossoverOperator,
        CompositeMutationOperator mutationOperator,
        ReproductionAllocator allocator)
    {
        _options = options.Value;
        _parentSelector = parentSelector;
        _crossoverOperator = crossoverOperator;
        _mutationOperator = mutationOperator;
        _allocator = allocator;
    }

    /// <summary>
    /// Produces the next generation of genomes from the current species.
    /// Each offspring is tagged with its source species ID so that
    /// complexity-limit enforcement can replace over-limit genomes
    /// with parents from the same species.
    /// </summary>
    /// <param name="species">The current species with assigned members and fitness data.</param>
    /// <param name="random">Seeded RNG for deterministic behavior.</param>
    /// <param name="tracker">Innovation tracker for structural mutations.</param>
    /// <returns>A list of offspring genomes paired with their source species ID.</returns>
    public IReadOnlyList<(Genome Offspring, int SourceSpeciesId)> Reproduce(
        IReadOnlyList<Species> species,
        Random random,
        IInnovationTracker tracker)
    {
        var offspring = new List<(Genome, int)>();

        if (species.Count == 0)
            return offspring;

        var allocation = _allocator.AllocateOffspring(species, _options.PopulationSize);
        var selectionOptions = _options.Selection;
        var crossoverOptions = _options.Crossover;

        foreach (var s in species)
        {
            if (!allocation.TryGetValue(s.Id, out int offspringCount) || offspringCount <= 0)
                continue;

            int remaining = offspringCount;

            // Elitism: copy champion unchanged if species >= ElitismThreshold
            if (s.Members.Count >= selectionOptions.ElitismThreshold && remaining > 0)
            {
                var champion = GetChampion(s);
                offspring.Add((champion, s.Id));
                remaining--;
            }

            if (remaining <= 0)
                continue;

            // Filter to top SurvivalThreshold fraction for parent selection
            var candidates = GetSurvivors(s, selectionOptions.SurvivalThreshold);
            var fitnessMap = BuildFitnessMap(candidates);

            // Produce remaining offspring
            for (int i = 0; i < remaining; i++)
            {
                Genome child;

                if (random.NextDouble() < crossoverOptions.CrossoverRate)
                {
                    // Crossover
                    var parent1 = _parentSelector.Select(candidates, random);
                    double parent1Fitness = fitnessMap[parent1];

                    Genome parent2;
                    double parent2Fitness;

                    if (random.NextDouble() < crossoverOptions.InterspeciesCrossoverRate
                        && species.Count > 1)
                    {
                        // Interspecies crossover: pick parent from a different species
                        var otherSpecies = PickDifferentSpecies(species, s, random);
                        var otherCandidates = GetSurvivors(otherSpecies, selectionOptions.SurvivalThreshold);
                        var otherFitnessMap = BuildFitnessMap(otherCandidates);
                        parent2 = _parentSelector.Select(otherCandidates, random);
                        parent2Fitness = otherFitnessMap[parent2];
                    }
                    else
                    {
                        // Same-species crossover
                        parent2 = _parentSelector.Select(candidates, random);
                        parent2Fitness = fitnessMap[parent2];
                    }

                    child = _crossoverOperator.Cross(parent1, parent1Fitness, parent2, parent2Fitness, random);
                }
                else
                {
                    // Clone: select one parent and copy
                    child = _parentSelector.Select(candidates, random);
                }

                // Mutate all non-elite offspring
                child = _mutationOperator.Mutate(child, random, tracker);
                offspring.Add((child, s.Id));
            }
        }

        return offspring;
    }

    private static Genome GetChampion(Species species)
    {
        var best = species.Members[0];
        for (int i = 1; i < species.Members.Count; i++)
        {
            if (species.Members[i].Fitness > best.Fitness)
                best = species.Members[i];
        }
        return best.Genome;
    }

    private static IReadOnlyList<(Genome Genome, double Fitness)> GetSurvivors(
        Species species, double survivalThreshold)
    {
        if (species.Members.Count <= 1)
            return species.Members;

        // Stable sort descending by fitness, take top fraction
        var sorted = species.Members
            .OrderByDescending(m => m.Fitness)
            .ToList();

        int surviveCount = Math.Max(1, (int)Math.Ceiling(sorted.Count * survivalThreshold));
        return surviveCount >= sorted.Count ? sorted : sorted.GetRange(0, surviveCount);
    }

    private static Dictionary<Genome, double> BuildFitnessMap(
        IReadOnlyList<(Genome Genome, double Fitness)> candidates)
    {
        var map = new Dictionary<Genome, double>(candidates.Count);
        foreach (var (genome, fitness) in candidates)
        {
            map.TryAdd(genome, fitness);
        }
        return map;
    }

    private static Species PickDifferentSpecies(
        IReadOnlyList<Species> allSpecies, Species current, Random random)
    {
        // Count eligible species (different from current, with members)
        int eligibleCount = 0;
        foreach (var s in allSpecies)
        {
            if (s.Id != current.Id && s.Members.Count > 0)
                eligibleCount++;
        }

        if (eligibleCount == 0)
            return current; // Fallback to same species

        // Pick a random index among eligible species and iterate to it
        int target = random.Next(eligibleCount);
        int seen = 0;
        foreach (var s in allSpecies)
        {
            if (s.Id != current.Id && s.Members.Count > 0)
            {
                if (seen == target)
                    return s;
                seen++;
            }
        }

        return current; // Unreachable, but satisfies compiler
    }
}
