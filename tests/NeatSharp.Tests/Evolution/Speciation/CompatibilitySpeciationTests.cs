using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Speciation;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Speciation;

public class CompatibilitySpeciationTests
{
    private static readonly IReadOnlyList<NodeGene> MinimalNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output)
    ];

    private static NeatSharpOptions DefaultOptions() => new()
    {
        Speciation =
        {
            ExcessCoefficient = 1.0,
            DisjointCoefficient = 1.0,
            WeightDifferenceCoefficient = 0.4,
            CompatibilityThreshold = 3.0
        }
    };

    private static Genome MakeGenome(IReadOnlyList<NodeGene> nodes, params ConnectionGene[] connections) =>
        new(nodes, connections);

    private static CompatibilitySpeciation CreateSut(
        NeatSharpOptions? options = null,
        ICompatibilityDistance? distance = null)
    {
        var opts = options ?? DefaultOptions();
        var dist = distance ?? new CompatibilityDistance(Options.Create(opts));
        return new CompatibilitySpeciation(Options.Create(opts), dist);
    }

    #region First Genome Creates New Species

    [Fact]
    public void Speciate_FirstGenome_CreatesNewSpecies()
    {
        var genome = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome, 5.0)
        };
        var species = new List<Species>();

        var sut = CreateSut();

        sut.Speciate(population, species);

        species.Should().HaveCount(1);
        species[0].Members.Should().HaveCount(1);
        species[0].Members[0].Genome.Should().BeSameAs(genome);
        species[0].Members[0].Fitness.Should().Be(5.0);
    }

    [Fact]
    public void Speciate_EmptyPopulation_NoSpeciesCreated()
    {
        var population = new List<(Genome Genome, double Fitness)>();
        var species = new List<Species>();

        var sut = CreateSut();

        sut.Speciate(population, species);

        species.Should().BeEmpty();
    }

    #endregion

    #region Genome Within Threshold Joins Existing Species

    [Fact]
    public void Speciate_GenomeWithinThreshold_JoinsExistingSpecies()
    {
        // Two identical genomes — distance=0, well within threshold=3.0
        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var genome2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));

        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 5.0),
            (genome2, 6.0)
        };
        var species = new List<Species>();

        var sut = CreateSut();

        sut.Speciate(population, species);

        species.Should().HaveCount(1, "both genomes should be in the same species");
        species[0].Members.Should().HaveCount(2);
    }

    [Fact]
    public void Speciate_GenomeJoinsFirstMatchingSpecies()
    {
        // Create two existing species; genome matches both representatives
        // Should join the FIRST match
        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var genome2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.1, true));
        var genome3 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.05, true));

        // Create species manually with genome1 and genome2 as representatives
        var species1 = new Species(1, genome1);
        var species2 = new Species(2, genome2);
        var speciesList = new List<Species> { species1, species2 };

        // genome3 is very close to both — should join species1 (first match)
        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 5.0),
            (genome2, 6.0),
            (genome3, 7.0)
        };

        var sut = CreateSut();

        sut.Speciate(population, speciesList);

        // All three are structurally very similar (same innovations, similar weights)
        // They should all be in the first species
        speciesList.Should().HaveCount(1, "all genomes are close enough for one species");
        speciesList[0].Members.Should().HaveCount(3);
    }

    #endregion

    #region Genome Above Threshold Creates New Species

    [Fact]
    public void Speciate_GenomeAboveThreshold_CreatesNewSpecies()
    {
        // Use a very low threshold so even slightly different genomes are separated
        var options = DefaultOptions();
        options.Speciation.CompatibilityThreshold = 0.01;

        // Two genomes with different structures
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var genome2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 2.0, true),
            new ConnectionGene(2, 2, 1, 3.0, true));

        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 5.0),
            (genome2, 6.0)
        };
        var species = new List<Species>();

        var sut = CreateSut(options);

        sut.Speciate(population, species);

        species.Should().HaveCount(2, "genomes with high distance should be in different species");
    }

    #endregion

    #region Empty Species Removed

    [Fact]
    public void Speciate_EmptySpeciesRemoved()
    {
        var genome = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));

        // Create an existing species with a very different representative
        var differentNodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden),
            new(3, NodeType.Hidden)
        };
        var differentGenome = MakeGenome(differentNodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 2.0, true),
            new ConnectionGene(2, 2, 3, 3.0, true),
            new ConnectionGene(3, 3, 1, 4.0, true));

        // Use a low threshold so genome won't match the existing species
        var options = DefaultOptions();
        options.Speciation.CompatibilityThreshold = 0.01;

        var existingSpecies = new Species(1, differentGenome);
        var speciesList = new List<Species> { existingSpecies };

        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome, 5.0)
        };

        var sut = CreateSut(options);

        sut.Speciate(population, speciesList);

        // The existing species should be removed (no members) and a new one created
        speciesList.Should().HaveCount(1);
        speciesList.Should().NotContain(s => s.Id == 1,
            "species with no members should be removed");
    }

    #endregion

    #region Representative Updated to Best Member

    [Fact]
    public void Speciate_RepresentativeUpdatedToBestMember()
    {
        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var genome2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.05, true));
        var genome3 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.1, true));

        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 3.0),
            (genome2, 10.0), // Best fitness
            (genome3, 5.0)
        };
        var species = new List<Species>();

        var sut = CreateSut();

        sut.Speciate(population, species);

        species.Should().HaveCount(1);
        species[0].Representative.Should().BeSameAs(genome2,
            "representative should be the best-performing member");
    }

    #endregion

    #region Stagnation Tracking

    [Fact]
    public void Speciate_FirstGeneration_BestFitnessRecorded()
    {
        var genome = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));

        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome, 7.5)
        };
        var species = new List<Species>();

        var sut = CreateSut();

        sut.Speciate(population, species);

        species[0].BestFitnessEver.Should().Be(7.5);
        species[0].GenerationsSinceImprovement.Should().Be(0);
    }

    [Fact]
    public void Speciate_FitnessImproves_StagnationReset()
    {
        var genome = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));

        // Setup: existing species with BestFitnessEver=5.0, stagnation=3
        var existingSpecies = new Species(1, genome)
        {
            BestFitnessEver = 5.0,
            GenerationsSinceImprovement = 3
        };
        var speciesList = new List<Species> { existingSpecies };

        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome, 8.0) // Better than 5.0
        };

        var sut = CreateSut();

        sut.Speciate(population, speciesList);

        speciesList[0].BestFitnessEver.Should().Be(8.0);
        speciesList[0].GenerationsSinceImprovement.Should().Be(0,
            "stagnation counter should reset on improvement");
    }

    [Fact]
    public void Speciate_NoImprovement_StagnationIncremented()
    {
        var genome = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));

        // Setup: existing species with BestFitnessEver=10.0, stagnation=3
        var existingSpecies = new Species(1, genome)
        {
            BestFitnessEver = 10.0,
            GenerationsSinceImprovement = 3
        };
        var speciesList = new List<Species> { existingSpecies };

        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome, 8.0) // Worse than 10.0
        };

        var sut = CreateSut();

        sut.Speciate(population, speciesList);

        speciesList[0].BestFitnessEver.Should().Be(10.0, "BestFitnessEver should not decrease");
        speciesList[0].GenerationsSinceImprovement.Should().Be(4,
            "stagnation counter should increment when no improvement");
    }

    [Fact]
    public void Speciate_FitnessEqualToBest_StagnationIncremented()
    {
        var genome = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));

        var existingSpecies = new Species(1, genome)
        {
            BestFitnessEver = 8.0,
            GenerationsSinceImprovement = 2
        };
        var speciesList = new List<Species> { existingSpecies };

        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome, 8.0) // Equal to BestFitnessEver — no improvement
        };

        var sut = CreateSut();

        sut.Speciate(population, speciesList);

        speciesList[0].BestFitnessEver.Should().Be(8.0);
        speciesList[0].GenerationsSinceImprovement.Should().Be(3,
            "equal fitness is NOT improvement — stagnation incremented");
    }

    #endregion

    #region Determinism

    [Fact]
    public void Speciate_SameInputsSameOrder_ProducesSameResult()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };

        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var genome2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 2.0, true),
            new ConnectionGene(2, 2, 1, 3.0, true));
        var genome3 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.1, true));

        var options = DefaultOptions();
        options.Speciation.CompatibilityThreshold = 0.5; // Low threshold to force separation

        // Run 1
        var species1 = new List<Species>();
        var pop1 = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 5.0), (genome2, 6.0), (genome3, 7.0)
        };
        var sut1 = CreateSut(options);
        sut1.Speciate(pop1, species1);

        // Run 2 — same inputs, same order
        var species2 = new List<Species>();
        var pop2 = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 5.0), (genome2, 6.0), (genome3, 7.0)
        };
        var sut2 = CreateSut(options);
        sut2.Speciate(pop2, species2);

        species1.Should().HaveCount(species2.Count);
        for (int i = 0; i < species1.Count; i++)
        {
            species1[i].Members.Should().HaveCount(species2[i].Members.Count,
                $"species {i} should have same member count in both runs");
        }
    }

    #endregion

    #region Species IDs Are Stable and Monotonically Increasing

    [Fact]
    public void Speciate_NewSpeciesIdsAreMonotonicallyIncreasing()
    {
        var options = DefaultOptions();
        options.Speciation.CompatibilityThreshold = 0.01; // Force each genome into its own species

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden),
            new(3, NodeType.Hidden)
        };

        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var genome2 = MakeGenome(nodes,
            new ConnectionGene(0, 0, 1, 1.0, true),
            new ConnectionGene(1, 0, 2, 2.0, true),
            new ConnectionGene(2, 2, 3, 3.0, true),
            new ConnectionGene(3, 3, 1, 4.0, true));
        var genome3 = MakeGenome(nodes,
            new ConnectionGene(4, 0, 3, 5.0, true),
            new ConnectionGene(5, 3, 2, 6.0, true),
            new ConnectionGene(6, 2, 1, 7.0, true));

        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 5.0), (genome2, 6.0), (genome3, 7.0)
        };
        var species = new List<Species>();

        var sut = CreateSut(options);

        sut.Speciate(population, species);

        species.Should().HaveCountGreaterOrEqualTo(2);
        for (int i = 1; i < species.Count; i++)
        {
            species[i].Id.Should().BeGreaterThan(species[i - 1].Id,
                "species IDs should be monotonically increasing");
        }
    }

    #endregion

    #region AverageFitness Computed Correctly

    [Fact]
    public void Speciate_AverageFitnessComputedFromMembers()
    {
        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var genome2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var genome3 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));

        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 4.0),
            (genome2, 6.0),
            (genome3, 8.0)
        };
        var species = new List<Species>();

        var sut = CreateSut();

        sut.Speciate(population, species);

        species.Should().HaveCount(1);
        species[0].AverageFitness.Should().Be(6.0, "average of 4,6,8 = 6");
    }

    #endregion

    #region Multiple Generations — Species Persistence

    [Fact]
    public void Speciate_ExistingSpeciesRetainIdAcrossGenerations()
    {
        var genome = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));

        var population = new List<(Genome Genome, double Fitness)>
        {
            (genome, 5.0)
        };
        var species = new List<Species>();

        var sut = CreateSut();

        // Generation 1
        sut.Speciate(population, species);
        int originalId = species[0].Id;

        // Generation 2 — same genome re-assigned
        var genome2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.05, true));
        var pop2 = new List<(Genome Genome, double Fitness)>
        {
            (genome2, 6.0)
        };
        sut.Speciate(pop2, species);

        species.Should().HaveCount(1);
        species[0].Id.Should().Be(originalId, "species ID should persist across generations");
    }

    #endregion

    #region Members Cleared at Start of Speciation

    [Fact]
    public void Speciate_ClearsMembersFromPreviousGeneration()
    {
        var genome1 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));
        var genome2 = MakeGenome(MinimalNodes,
            new ConnectionGene(0, 0, 1, 1.0, true));

        var sut = CreateSut();

        // Gen 1: two genomes in one species
        var pop1 = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 5.0), (genome2, 6.0)
        };
        var species = new List<Species>();
        sut.Speciate(pop1, species);
        species[0].Members.Should().HaveCount(2);

        // Gen 2: only one genome
        var pop2 = new List<(Genome Genome, double Fitness)>
        {
            (genome1, 7.0)
        };
        sut.Speciate(pop2, species);

        species[0].Members.Should().HaveCount(1,
            "previous generation's members should be cleared");
    }

    #endregion

    #region Every Genome Assigned to Exactly One Species

    [Fact]
    public void Speciate_EveryGenomeAssignedToExactlyOneSpecies()
    {
        var options = DefaultOptions();
        options.Speciation.CompatibilityThreshold = 1.0;

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };

        var genomes = new List<Genome>();
        for (int i = 0; i < 10; i++)
        {
            genomes.Add(MakeGenome(nodes,
                new ConnectionGene(0, 0, 1, 1.0 + i * 0.1, true),
                new ConnectionGene(1, 0, 2, 2.0 + i * 0.2, true)));
        }

        var population = genomes.Select((g, i) => (g, (double)(i + 1))).ToList();
        var species = new List<Species>();

        var sut = CreateSut(options);
        sut.Speciate(population, species);

        // Count total members across all species
        int totalMembers = species.Sum(s => s.Members.Count);
        totalMembers.Should().Be(10, "every genome should be assigned exactly once");

        // Verify no genome appears in multiple species
        var allGenomes = species.SelectMany(s => s.Members.Select(m => m.Genome)).ToList();
        allGenomes.Should().OnlyHaveUniqueItems("each genome should be in exactly one species");
    }

    #endregion
}
