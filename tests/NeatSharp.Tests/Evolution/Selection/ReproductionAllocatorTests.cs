using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Selection;
using NeatSharp.Evolution.Speciation;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Selection;

public class ReproductionAllocatorTests
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
        PopulationSize = 100,
        Selection =
        {
            ElitismThreshold = 5,
            StagnationThreshold = 15,
            SurvivalThreshold = 0.2,
            TournamentSize = 2
        }
    };

    private static ReproductionAllocator CreateSut(NeatSharpOptions? options = null) =>
        new(Options.Create(options ?? DefaultOptions()));

    private static Species MakeSpeciesWithMembers(int id, int memberCount, double fitness,
        double bestFitnessEver = 0.0, int generationsSinceImprovement = 0)
    {
        var representative = MakeGenome(id);
        var species = new Species(id, representative)
        {
            BestFitnessEver = bestFitnessEver > 0 ? bestFitnessEver : fitness,
            GenerationsSinceImprovement = generationsSinceImprovement
        };
        for (int i = 0; i < memberCount; i++)
        {
            species.Members.Add((MakeGenome(id + i * 0.01), fitness));
        }
        return species;
    }

    #region Proportional Offspring Allocation

    [Fact]
    public void AllocateOffspring_ProportionalToAverageFitness()
    {
        // Species with avg fitness 10, 5, 2.5 should get ~57%, ~29%, ~14% of 100
        var species = new List<Species>
        {
            MakeSpeciesWithMembers(1, 10, 10.0),
            MakeSpeciesWithMembers(2, 10, 5.0),
            MakeSpeciesWithMembers(3, 10, 2.5)
        };

        var sut = CreateSut();

        var result = sut.AllocateOffspring(species, 100);

        result.Should().HaveCount(3);
        int total = result.Values.Sum();
        total.Should().Be(100, "total offspring must equal population size");

        // Verify proportional allocation (with rounding tolerance)
        result[1].Should().BeGreaterThan(result[2],
            "species with higher avg fitness gets more offspring");
        result[2].Should().BeGreaterThan(result[3],
            "species with higher avg fitness gets more offspring");
    }

    [Fact]
    public void AllocateOffspring_SingleSpecies_GetsAllOffspring()
    {
        var species = new List<Species>
        {
            MakeSpeciesWithMembers(1, 10, 10.0)
        };

        var sut = CreateSut();

        var result = sut.AllocateOffspring(species, 100);

        result[1].Should().Be(100);
    }

    #endregion

    #region Stagnant Species Get Zero Offspring

    [Fact]
    public void AllocateOffspring_StagnantSpecies_GetsZeroOffspring()
    {
        var options = DefaultOptions();
        options.Selection.StagnationThreshold = 15;

        var species = new List<Species>
        {
            MakeSpeciesWithMembers(1, 10, 10.0, bestFitnessEver: 20.0),
            MakeSpeciesWithMembers(2, 10, 10.0, bestFitnessEver: 5.0, generationsSinceImprovement: 16),
            MakeSpeciesWithMembers(3, 10, 10.0, bestFitnessEver: 15.0)
        };

        var sut = CreateSut(options);

        var result = sut.AllocateOffspring(species, 100);

        // Species 2 is stagnant and NOT in top 2 by BestFitnessEver (5.0 < 15.0 and 20.0)
        result[2].Should().Be(0, "stagnant species should get 0 offspring");
        (result[1] + result[3]).Should().Be(100,
            "non-stagnant species should split the population");
    }

    [Fact]
    public void AllocateOffspring_AtExactThreshold_NotStagnant()
    {
        var options = DefaultOptions();
        options.Selection.StagnationThreshold = 15;

        var species = new List<Species>
        {
            MakeSpeciesWithMembers(1, 10, 10.0),
            MakeSpeciesWithMembers(2, 10, 10.0, bestFitnessEver: 10.0, generationsSinceImprovement: 15)
        };

        var sut = CreateSut(options);

        var result = sut.AllocateOffspring(species, 100);

        // At exactly the threshold (15), not stagnant (need > threshold)
        result[2].Should().BeGreaterThan(0,
            "species at exactly stagnation threshold should not be penalized");
    }

    #endregion

    #region Top 2 Species Protected

    [Fact]
    public void AllocateOffspring_Top2ByBestFitnessEver_NeverFullyEliminated()
    {
        var options = DefaultOptions();
        options.Selection.StagnationThreshold = 15;

        // All stagnant, but top 2 by BestFitnessEver should survive
        var species = new List<Species>
        {
            MakeSpeciesWithMembers(1, 10, 5.0, bestFitnessEver: 100.0, generationsSinceImprovement: 20),
            MakeSpeciesWithMembers(2, 10, 5.0, bestFitnessEver: 80.0, generationsSinceImprovement: 20),
            MakeSpeciesWithMembers(3, 10, 5.0, bestFitnessEver: 50.0, generationsSinceImprovement: 20),
            MakeSpeciesWithMembers(4, 10, 5.0, bestFitnessEver: 30.0, generationsSinceImprovement: 20)
        };

        var sut = CreateSut(options);

        var result = sut.AllocateOffspring(species, 100);

        // Top 2 (species 1 and 2 by BestFitnessEver) should have offspring
        result[1].Should().BeGreaterThan(0, "top-1 by BestFitnessEver should be protected");
        result[2].Should().BeGreaterThan(0, "top-2 by BestFitnessEver should be protected");

        // Species 3 and 4 are stagnant and not in top 2 — get 0
        result[3].Should().Be(0, "stagnant species not in top 2 should get 0");
        result[4].Should().Be(0, "stagnant species not in top 2 should get 0");
    }

    [Fact]
    public void AllocateOffspring_OnlyTwoSpecies_BothProtected()
    {
        var options = DefaultOptions();
        options.Selection.StagnationThreshold = 15;

        var species = new List<Species>
        {
            MakeSpeciesWithMembers(1, 10, 5.0, bestFitnessEver: 50.0, generationsSinceImprovement: 20),
            MakeSpeciesWithMembers(2, 10, 5.0, bestFitnessEver: 30.0, generationsSinceImprovement: 20)
        };

        var sut = CreateSut(options);

        var result = sut.AllocateOffspring(species, 100);

        result[1].Should().BeGreaterThan(0);
        result[2].Should().BeGreaterThan(0);
        (result[1] + result[2]).Should().Be(100);
    }

    #endregion

    #region Elitism Reserves Champion Slot

    [Fact]
    public void AllocateOffspring_SpeciesAboveElitismThreshold_ReservesChampionSlot()
    {
        var options = DefaultOptions();
        options.Selection.ElitismThreshold = 5;

        var species = new List<Species>
        {
            MakeSpeciesWithMembers(1, 10, 10.0), // 10 members >= 5 threshold
            MakeSpeciesWithMembers(2, 3, 10.0)   // 3 members < 5 threshold
        };

        var sut = CreateSut(options);

        var result = sut.AllocateOffspring(species, 100);

        // Both species should get offspring, and total = 100
        int total = result.Values.Sum();
        total.Should().Be(100);

        // Species 1 (>= ElitismThreshold) gets at least 1 for champion
        result[1].Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void AllocateOffspring_SpeciesBelowElitismThreshold_NoChampionReserved()
    {
        var options = DefaultOptions();
        options.Selection.ElitismThreshold = 5;

        // Single species with few members
        var species = new List<Species>
        {
            MakeSpeciesWithMembers(1, 3, 10.0)  // 3 members < 5 threshold
        };

        var sut = CreateSut(options);

        var result = sut.AllocateOffspring(species, 100);

        // Still gets all 100 offspring (no elitism reservation needed)
        result[1].Should().Be(100);
    }

    #endregion

    #region Total Offspring Equals PopulationSize

    [Fact]
    public void AllocateOffspring_TotalAlwaysEqualsPopulationSize()
    {
        // Test with various configurations that might cause rounding issues
        for (int popSize = 10; popSize <= 200; popSize += 7)
        {
            var options = DefaultOptions();
            options.PopulationSize = popSize;

            var species = new List<Species>
            {
                MakeSpeciesWithMembers(1, 10, 10.0),
                MakeSpeciesWithMembers(2, 10, 7.0),
                MakeSpeciesWithMembers(3, 10, 3.0)
            };

            var sut = CreateSut(options);

            var result = sut.AllocateOffspring(species, popSize);

            int total = result.Values.Sum();
            total.Should().Be(popSize,
                $"total offspring must equal populationSize={popSize}");
        }
    }

    [Fact]
    public void AllocateOffspring_ManySpecies_TotalCorrect()
    {
        var species = new List<Species>();
        for (int i = 1; i <= 20; i++)
        {
            species.Add(MakeSpeciesWithMembers(i, 5, i * 1.5));
        }

        var sut = CreateSut();

        var result = sut.AllocateOffspring(species, 100);

        result.Values.Sum().Should().Be(100);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void AllocateOffspring_AllSpeciesStagnantExceptTop2_OnlyTop2GetOffspring()
    {
        var options = DefaultOptions();
        options.Selection.StagnationThreshold = 5;

        var species = new List<Species>
        {
            MakeSpeciesWithMembers(1, 10, 10.0, bestFitnessEver: 20.0, generationsSinceImprovement: 10),
            MakeSpeciesWithMembers(2, 10, 8.0, bestFitnessEver: 15.0, generationsSinceImprovement: 10),
            MakeSpeciesWithMembers(3, 10, 12.0, bestFitnessEver: 12.0, generationsSinceImprovement: 10),
            MakeSpeciesWithMembers(4, 10, 3.0, bestFitnessEver: 5.0, generationsSinceImprovement: 10)
        };

        var sut = CreateSut(options);

        var result = sut.AllocateOffspring(species, 100);

        // Top 2 by BestFitnessEver: species 1 (20.0), species 2 (15.0)
        result[1].Should().BeGreaterThan(0);
        result[2].Should().BeGreaterThan(0);
        result[3].Should().Be(0);
        result[4].Should().Be(0);
    }

    [Fact]
    public void AllocateOffspring_EmptySpeciesList_ReturnsEmptyDictionary()
    {
        var sut = CreateSut();

        var result = sut.AllocateOffspring(new List<Species>(), 100);

        result.Should().BeEmpty();
    }

    #endregion
}
