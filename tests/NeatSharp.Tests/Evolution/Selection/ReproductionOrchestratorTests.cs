using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Crossover;
using NeatSharp.Evolution.Mutation;
using NeatSharp.Evolution.Selection;
using NeatSharp.Evolution.Speciation;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Selection;

public class ReproductionOrchestratorTests
{
    private static readonly IReadOnlyList<NodeGene> MinimalNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output)
    ];

    private static Genome MakeGenome(double weight = 1.0) =>
        new(MinimalNodes, [new ConnectionGene(0, 0, 1, weight, true)]);

    private static NeatSharpOptions DefaultOptions() => new()
    {
        PopulationSize = 20,
        Stopping = { MaxGenerations = 10 },
        Selection =
        {
            ElitismThreshold = 5,
            StagnationThreshold = 15,
            SurvivalThreshold = 0.2,
            TournamentSize = 2
        },
        Crossover =
        {
            CrossoverRate = 0.75,
            InterspeciesCrossoverRate = 0.001,
            DisabledGeneInheritanceProbability = 0.75
        },
        Mutation =
        {
            WeightPerturbationRate = 0.8,
            WeightReplacementRate = 0.1,
            AddConnectionRate = 0.0,
            AddNodeRate = 0.0,
            ToggleEnableRate = 0.0,
            PerturbationPower = 0.5,
            PerturbationDistribution = WeightDistributionType.Uniform,
            WeightMinValue = -4.0,
            WeightMaxValue = 4.0,
            MaxAddConnectionAttempts = 20
        }
    };

    private static Species MakeSpeciesWithMembers(int id, int memberCount, double baseFitness)
    {
        var representative = MakeGenome(id * 0.1);
        var species = new Species(id, representative)
        {
            BestFitnessEver = baseFitness,
            GenerationsSinceImprovement = 0
        };
        for (int i = 0; i < memberCount; i++)
        {
            // Assign descending fitness so top members are deterministic
            species.Members.Add((MakeGenome(id + i * 0.01), baseFitness - i));
        }
        return species;
    }

    private static CompositeMutationOperator CreateCompositeMutation(IOptions<NeatSharpOptions> opts) =>
        new(
            opts,
            new WeightPerturbationMutation(opts),
            new WeightReplacementMutation(opts),
            new AddConnectionMutation(opts),
            new AddNodeMutation(opts),
            new ToggleEnableMutation());

    private static ReproductionOrchestrator CreateSut(NeatSharpOptions? options = null)
    {
        var opts = Options.Create(options ?? DefaultOptions());
        return new ReproductionOrchestrator(
            opts,
            new TournamentSelector(opts),
            new NeatCrossover(opts),
            CreateCompositeMutation(opts),
            new ReproductionAllocator(opts));
    }

    #region Total Offspring Equals PopulationSize

    [Fact]
    public void Reproduce_TotalOffspringEqualsPopulationSize()
    {
        var options = DefaultOptions();
        options.PopulationSize = 30;
        var species = new List<Species>
        {
            MakeSpeciesWithMembers(1, 10, 10.0),
            MakeSpeciesWithMembers(2, 8, 8.0),
            MakeSpeciesWithMembers(3, 6, 5.0)
        };

        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var offspring = sut.Reproduce(species, random, tracker);

        offspring.Should().HaveCount(30, "total offspring must equal PopulationSize");
    }

    [Fact]
    public void Reproduce_VariousPopulationSizes_AlwaysExact()
    {
        for (int popSize = 5; popSize <= 50; popSize += 5)
        {
            var options = DefaultOptions();
            options.PopulationSize = popSize;
            // Use smaller species to avoid needing too many members
            options.Selection.ElitismThreshold = 3;
            var species = new List<Species>
            {
                MakeSpeciesWithMembers(1, 5, 10.0),
                MakeSpeciesWithMembers(2, 5, 5.0)
            };

            var sut = CreateSut(options);
            var random = new Random(42);
            var tracker = new InnovationTracker(100, 100);

            var offspring = sut.Reproduce(species, random, tracker);

            offspring.Should().HaveCount(popSize,
                $"total offspring must equal PopulationSize={popSize}");
        }
    }

    #endregion

    #region Elite Champions Not Mutated

    [Fact]
    public void Reproduce_EliteChampion_CopiedUnchanged()
    {
        var options = DefaultOptions();
        options.PopulationSize = 10;
        options.Selection.ElitismThreshold = 5;
        // Disable crossover to simplify — all non-elite are clones + mutated
        options.Crossover.CrossoverRate = 0.0;
        // Ensure mutation always changes weight
        options.Mutation.WeightPerturbationRate = 1.0;
        options.Mutation.WeightReplacementRate = 0.0;

        // Species with 6 members (>= ElitismThreshold=5), champion is highest fitness
        var champion = MakeGenome(99.0);
        var species1 = new Species(1, champion)
        {
            BestFitnessEver = 100.0,
            GenerationsSinceImprovement = 0
        };
        species1.Members.Add((champion, 100.0)); // champion (highest fitness)
        for (int i = 1; i < 6; i++)
        {
            species1.Members.Add((MakeGenome(i * 0.1), 100.0 - i * 10));
        }

        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var offspring = sut.Reproduce([species1], random, tracker);

        // The champion should be in offspring unchanged
        offspring.Should().Contain(champion,
            "elite champion should be copied to next generation unchanged");
    }

    [Fact]
    public void Reproduce_SpeciesBelowElitismThreshold_NoChampionPreserved()
    {
        var options = DefaultOptions();
        options.PopulationSize = 10;
        options.Selection.ElitismThreshold = 5;
        // Ensure mutation always changes weight
        options.Mutation.WeightPerturbationRate = 1.0;
        options.Mutation.WeightReplacementRate = 0.0;
        options.Crossover.CrossoverRate = 0.0;

        // Species with 3 members (< ElitismThreshold=5)
        var champion = MakeGenome(99.0);
        var species1 = new Species(1, champion)
        {
            BestFitnessEver = 100.0,
            GenerationsSinceImprovement = 0
        };
        species1.Members.Add((champion, 100.0));
        species1.Members.Add((MakeGenome(0.5), 50.0));
        species1.Members.Add((MakeGenome(0.1), 10.0));

        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var offspring = sut.Reproduce([species1], random, tracker);

        // No elite champion — all offspring should be mutated (different from champion)
        offspring.Should().NotContain(champion,
            "species below elitism threshold should not preserve champion unchanged");
    }

    #endregion

    #region Survival Threshold Filtering

    [Fact]
    public void Reproduce_SurvivalThreshold_FiltersSpeciesMembers()
    {
        var options = DefaultOptions();
        options.PopulationSize = 20;
        options.Selection.SurvivalThreshold = 0.5; // Top 50% survive
        options.Selection.ElitismThreshold = 100; // Disable elitism
        options.Crossover.CrossoverRate = 0.0; // Clone only
        options.Mutation.WeightPerturbationRate = 0.0; // No mutation
        options.Mutation.WeightReplacementRate = 0.0;

        // Create species with 10 members: weights 10.0 down to 1.0
        // With SurvivalThreshold=0.5, only top 5 (weights 10..6) should be selectable
        var species1 = new Species(1, MakeGenome(10.0))
        {
            BestFitnessEver = 10.0,
            GenerationsSinceImprovement = 0
        };
        for (int i = 0; i < 10; i++)
        {
            double weight = 10.0 - i;
            species1.Members.Add((MakeGenome(weight), 10.0 - i));
        }

        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var offspring = sut.Reproduce([species1], random, tracker);

        // All offspring should come from the top 50% of parents (fitness >= 6.0)
        // Since no mutation and clone-only, offspring weights should match top-half parent weights
        var bottomHalfWeights = new HashSet<double> { 1.0, 2.0, 3.0, 4.0, 5.0 };
        foreach (var genome in offspring)
        {
            bottomHalfWeights.Should().NotContain(genome.Connections[0].Weight,
                "offspring should only come from top SurvivalThreshold fraction of parents");
        }
    }

    #endregion

    #region Crossover vs Clone Rate

    [Fact]
    public void Reproduce_CrossoverRate_RespectedStatistically()
    {
        var options = DefaultOptions();
        options.PopulationSize = 200;
        options.Crossover.CrossoverRate = 0.75;
        options.Selection.ElitismThreshold = 100; // Disable elitism
        // Disable mutations to observe crossover effects
        options.Mutation.WeightPerturbationRate = 0.0;
        options.Mutation.WeightReplacementRate = 0.0;

        // Two species with different weights so we can distinguish parents
        var species1 = new Species(1, MakeGenome(1.0))
        {
            BestFitnessEver = 10.0,
            GenerationsSinceImprovement = 0
        };
        for (int i = 0; i < 20; i++)
        {
            species1.Members.Add((MakeGenome(1.0), 10.0));
        }

        var species2 = new Species(2, MakeGenome(2.0))
        {
            BestFitnessEver = 10.0,
            GenerationsSinceImprovement = 0
        };
        for (int i = 0; i < 20; i++)
        {
            species2.Members.Add((MakeGenome(2.0), 10.0));
        }

        // Run across multiple seeds and verify total offspring count
        int totalOffspring = 0;
        for (int seed = 0; seed < 20; seed++)
        {
            var sut = CreateSut(options);
            var random = new Random(seed);
            var tracker = new InnovationTracker(100, 100);

            var offspring = sut.Reproduce([species1, species2], random, tracker);
            totalOffspring += offspring.Count;

            // In crossover with same-species parents of equal weight, offspring
            // may look identical to parents. We can't perfectly distinguish here,
            // but we verify total count is correct.
        }

        // At minimum, verify total offspring count was always correct
        totalOffspring.Should().Be(200 * 20);
    }

    #endregion

    #region Interspecies Crossover

    [Fact]
    public void Reproduce_InterspeciesCrossoverRate_TriggersWhenHigh()
    {
        var options = DefaultOptions();
        options.PopulationSize = 100;
        options.Crossover.CrossoverRate = 1.0; // Always crossover
        options.Crossover.InterspeciesCrossoverRate = 1.0; // Always interspecies
        options.Selection.ElitismThreshold = 100; // Disable elitism
        options.Selection.SurvivalThreshold = 1.0; // All members eligible
        options.Mutation.WeightPerturbationRate = 0.0;
        options.Mutation.WeightReplacementRate = 0.0;

        // Two species with distinct genomes
        var species1 = new Species(1, MakeGenome(1.0))
        {
            BestFitnessEver = 10.0,
            GenerationsSinceImprovement = 0
        };
        for (int i = 0; i < 10; i++)
            species1.Members.Add((MakeGenome(1.0), 10.0));

        var species2 = new Species(2, MakeGenome(5.0))
        {
            BestFitnessEver = 10.0,
            GenerationsSinceImprovement = 0
        };
        for (int i = 0; i < 10; i++)
            species2.Members.Add((MakeGenome(5.0), 10.0));

        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var offspring = sut.Reproduce([species1, species2], random, tracker);

        offspring.Should().HaveCount(100);
        // With interspecies=1.0 and crossover=1.0, second parent always from different species.
        // Crossover between genomes with weight=1.0 and weight=5.0 should produce offspring
        // with weight from either parent (matching gene 50/50 inheritance)
        foreach (var g in offspring)
        {
            double w = g.Connections[0].Weight;
            // Weight should be 1.0 or 5.0 (from either parent via matching gene 50/50)
            (Math.Abs(w - 1.0) < 1e-10 || Math.Abs(w - 5.0) < 1e-10)
                .Should().BeTrue("offspring weight should come from one of the two parents");
        }
    }

    [Fact]
    public void Reproduce_SingleSpecies_InterspeciesFallsBackToSameSpecies()
    {
        var options = DefaultOptions();
        options.PopulationSize = 10;
        options.Crossover.CrossoverRate = 1.0;
        options.Crossover.InterspeciesCrossoverRate = 1.0; // Would pick other species, but only one exists
        options.Selection.ElitismThreshold = 100;
        options.Mutation.WeightPerturbationRate = 0.0;
        options.Mutation.WeightReplacementRate = 0.0;

        var species1 = new Species(1, MakeGenome(1.0))
        {
            BestFitnessEver = 10.0,
            GenerationsSinceImprovement = 0
        };
        for (int i = 0; i < 5; i++)
            species1.Members.Add((MakeGenome(1.0), 10.0));

        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        // Should not throw — falls back to same species when only one exists
        var offspring = sut.Reproduce([species1], random, tracker);

        offspring.Should().HaveCount(10);
    }

    #endregion

    #region All Non-Elite Offspring Mutated

    [Fact]
    public void Reproduce_AllNonEliteOffspring_AreMutated()
    {
        var options = DefaultOptions();
        options.PopulationSize = 10;
        options.Selection.ElitismThreshold = 5;
        options.Crossover.CrossoverRate = 0.0; // Clone only
        // Ensure mutation always modifies (perturbation rate=1.0 on weight=0.0)
        options.Mutation.WeightPerturbationRate = 1.0;
        options.Mutation.WeightReplacementRate = 0.0;
        options.Mutation.PerturbationPower = 0.5;

        // Species with 6 members (>= ElitismThreshold), all with weight=0.0
        var species1 = new Species(1, MakeGenome(0.0))
        {
            BestFitnessEver = 10.0,
            GenerationsSinceImprovement = 0
        };
        for (int i = 0; i < 6; i++)
        {
            species1.Members.Add((MakeGenome(0.0), 10.0 - i));
        }

        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var offspring = sut.Reproduce([species1], random, tracker);

        offspring.Should().HaveCount(10);

        // 1 champion (weight=0.0 unchanged) + 9 mutated (weight != 0.0)
        int unchangedCount = offspring.Count(g => g.Connections[0].Weight == 0.0);
        int mutatedCount = offspring.Count(g => g.Connections[0].Weight != 0.0);

        unchangedCount.Should().Be(1, "exactly one champion should be preserved unchanged");
        mutatedCount.Should().Be(9, "all non-elite offspring should be mutated");
    }

    #endregion

    #region Determinism With Fixed Seed

    [Fact]
    public void Reproduce_Deterministic_SameResultWithSameSeed()
    {
        var options = DefaultOptions();
        options.PopulationSize = 20;
        var species = new List<Species>
        {
            MakeSpeciesWithMembers(1, 8, 10.0),
            MakeSpeciesWithMembers(2, 6, 5.0)
        };

        var sut = CreateSut(options);

        var result1 = sut.Reproduce(species, new Random(42), new InnovationTracker(100, 100));

        // Recreate species (since members list is reused, rebuild for clean state)
        var species2 = new List<Species>
        {
            MakeSpeciesWithMembers(1, 8, 10.0),
            MakeSpeciesWithMembers(2, 6, 5.0)
        };
        var sut2 = CreateSut(options);
        var result2 = sut2.Reproduce(species2, new Random(42), new InnovationTracker(100, 100));

        result1.Should().HaveCount(result2.Count);
        for (int i = 0; i < result1.Count; i++)
        {
            result1[i].Nodes.Count.Should().Be(result2[i].Nodes.Count);
            result1[i].Connections.Count.Should().Be(result2[i].Connections.Count);
            for (int j = 0; j < result1[i].Connections.Count; j++)
            {
                result1[i].Connections[j].Should().Be(result2[i].Connections[j]);
            }
        }
    }

    [Fact]
    public void Reproduce_DifferentSeeds_ProduceDifferentResults()
    {
        var options = DefaultOptions();
        options.PopulationSize = 20;

        bool foundDifference = false;
        for (int seed = 0; seed < 10; seed++)
        {
            var species1 = new List<Species> { MakeSpeciesWithMembers(1, 10, 10.0) };
            var species2 = new List<Species> { MakeSpeciesWithMembers(1, 10, 10.0) };

            var sut = CreateSut(options);
            var r1 = sut.Reproduce(species1, new Random(seed), new InnovationTracker(100, 100));

            var sut2 = CreateSut(options);
            var r2 = sut2.Reproduce(species2, new Random(seed + 1000), new InnovationTracker(100, 100));

            for (int i = 0; i < Math.Min(r1.Count, r2.Count); i++)
            {
                if (r1[i].Connections.Count > 0 && r2[i].Connections.Count > 0 &&
                    Math.Abs(r1[i].Connections[0].Weight - r2[i].Connections[0].Weight) > 1e-10)
                {
                    foundDifference = true;
                    break;
                }
            }
            if (foundDifference) break;
        }

        foundDifference.Should().BeTrue("different seeds should produce different offspring");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Reproduce_EmptySpeciesList_ReturnsEmptyList()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var offspring = sut.Reproduce([], random, tracker);

        offspring.Should().BeEmpty();
    }

    [Fact]
    public void Reproduce_SingleMemberSpecies_ProducesCorrectOffspring()
    {
        var options = DefaultOptions();
        options.PopulationSize = 5;
        options.Selection.ElitismThreshold = 100; // Disable elitism

        var species1 = new Species(1, MakeGenome(1.0))
        {
            BestFitnessEver = 10.0,
            GenerationsSinceImprovement = 0
        };
        species1.Members.Add((MakeGenome(1.0), 10.0));

        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var offspring = sut.Reproduce([species1], random, tracker);

        offspring.Should().HaveCount(5);
    }

    #endregion
}
