using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Selection;
using NeatSharp.Evolution.Speciation;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Selection;

public class ComplexityPenaltyTests
{
    // Simple genome: 2 nodes (input + output), 1 connection
    private static Genome MakeSimpleGenome() =>
        new(
            [new NodeGene(0, NodeType.Input), new NodeGene(1, NodeType.Output)],
            [new ConnectionGene(0, 0, 1, 1.0, true)]
        );

    // Complex genome: 4 nodes (input + output + 2 hidden), 3 connections
    private static Genome MakeComplexGenome() =>
        new(
            [
                new NodeGene(0, NodeType.Input),
                new NodeGene(1, NodeType.Output),
                new NodeGene(2, NodeType.Hidden),
                new NodeGene(3, NodeType.Hidden)
            ],
            [
                new ConnectionGene(0, 0, 2, 1.0, true),
                new ConnectionGene(1, 2, 3, 1.0, true),
                new ConnectionGene(2, 3, 1, 1.0, true)
            ]
        );

    private static Species MakeSpeciesWithGenome(
        int id, Genome genome, int memberCount, double fitness)
    {
        var species = new Species(id, genome)
        {
            BestFitnessEver = fitness,
            GenerationsSinceImprovement = 0
        };
        for (int i = 0; i < memberCount; i++)
        {
            species.Members.Add((genome, fitness));
        }
        return species;
    }

    private static NeatSharpOptions MakeOptions(
        double coefficient, ComplexityPenaltyMetric metric) => new()
    {
        PopulationSize = 100,
        ComplexityPenalty =
        {
            Coefficient = coefficient,
            Metric = metric
        }
    };

    private static ReproductionAllocator CreateSut(NeatSharpOptions options) =>
        new(Options.Create(options));

    #region NodeCount Metric

    [Fact]
    public void AllocateOffspring_NodeCountMetric_PenaltyProportionalToNodeCount()
    {
        // Simple: 2 nodes, Complex: 4 nodes, both raw fitness 10.0
        // With coeff=1.0, NodeCount: simple effective = 10-2=8, complex effective = 10-4=6
        // Total=14: simple = 8/14*100 ≈ 57, complex = 6/14*100 ≈ 43
        var options = MakeOptions(coefficient: 1.0, ComplexityPenaltyMetric.NodeCount);
        var species = new List<Species>
        {
            MakeSpeciesWithGenome(1, MakeSimpleGenome(), 10, 10.0),
            MakeSpeciesWithGenome(2, MakeComplexGenome(), 10, 10.0)
        };

        var sut = CreateSut(options);
        var result = sut.AllocateOffspring(species, 100);

        result[1].Should().Be(57,
            "simple species (2 nodes) should get 8/14 of 100 = 57 offspring");
        result[2].Should().Be(43,
            "complex species (4 nodes) should get 6/14 of 100 = 43 offspring");
        result.Values.Sum().Should().Be(100);
    }

    #endregion

    #region ConnectionCount Metric

    [Fact]
    public void AllocateOffspring_ConnectionCountMetric_PenaltyProportionalToConnectionCount()
    {
        // Simple: 1 connection, Complex: 3 connections, both raw fitness 10.0
        // With coeff=1.0, ConnectionCount: simple effective = 10-1=9, complex effective = 10-3=7
        // Total=16: simple = 9/16*100 = 56.25, complex = 7/16*100 = 43.75
        // After rounding: simple=56, complex=44 (remainder goes to complex with larger fraction)
        var options = MakeOptions(coefficient: 1.0, ComplexityPenaltyMetric.ConnectionCount);
        var species = new List<Species>
        {
            MakeSpeciesWithGenome(1, MakeSimpleGenome(), 10, 10.0),
            MakeSpeciesWithGenome(2, MakeComplexGenome(), 10, 10.0)
        };

        var sut = CreateSut(options);
        var result = sut.AllocateOffspring(species, 100);

        result[1].Should().Be(56,
            "simple species (1 conn) should get 9/16 of 100 = 56 offspring");
        result[2].Should().Be(44,
            "complex species (3 conns) should get 7/16 of 100 = 44 offspring");
        result.Values.Sum().Should().Be(100);
    }

    #endregion

    #region Both Metric

    [Fact]
    public void AllocateOffspring_BothMetric_PenaltyProportionalToNodesPlusConnections()
    {
        // Simple: 2+1=3, Complex: 4+3=7, both raw fitness 10.0
        // With coeff=1.0, Both: simple effective = 10-3=7, complex effective = 10-7=3
        // Total=10: simple = 7/10*100 = 70 (exact), complex = 3/10*100 = 30 (exact)
        var options = MakeOptions(coefficient: 1.0, ComplexityPenaltyMetric.Both);
        var species = new List<Species>
        {
            MakeSpeciesWithGenome(1, MakeSimpleGenome(), 10, 10.0),
            MakeSpeciesWithGenome(2, MakeComplexGenome(), 10, 10.0)
        };

        var sut = CreateSut(options);
        var result = sut.AllocateOffspring(species, 100);

        result[1].Should().Be(70,
            "simple species (complexity 3) should get 7/10 of 100 = 70 offspring");
        result[2].Should().Be(30,
            "complex species (complexity 7) should get 3/10 of 100 = 30 offspring");
        result.Values.Sum().Should().Be(100);
    }

    #endregion

    #region Coefficient Zero (Disabled)

    [Fact]
    public void AllocateOffspring_CoefficientZero_OriginalFitnessUnchanged()
    {
        // With coefficient=0.0, penalty is disabled — complexity doesn't affect allocation.
        // Both species have same raw fitness 10.0 → 50/50 split regardless of complexity.
        var options = MakeOptions(coefficient: 0.0, ComplexityPenaltyMetric.Both);
        var species = new List<Species>
        {
            MakeSpeciesWithGenome(1, MakeSimpleGenome(), 10, 10.0),
            MakeSpeciesWithGenome(2, MakeComplexGenome(), 10, 10.0)
        };

        var sut = CreateSut(options);
        var result = sut.AllocateOffspring(species, 100);

        result[1].Should().Be(50,
            "with coefficient=0.0, complexity has no effect — equal fitness means equal offspring");
        result[2].Should().Be(50,
            "with coefficient=0.0, complexity has no effect — equal fitness means equal offspring");
    }

    [Fact]
    public void AllocateOffspring_DefaultOptions_PenaltyDisabled()
    {
        // Default NeatSharpOptions has ComplexityPenalty.Coefficient=0.0, verifying
        // the penalty is disabled by default without explicit configuration.
        var options = new NeatSharpOptions { PopulationSize = 100 };
        var species = new List<Species>
        {
            MakeSpeciesWithGenome(1, MakeSimpleGenome(), 10, 10.0),
            MakeSpeciesWithGenome(2, MakeComplexGenome(), 10, 10.0)
        };

        var sut = CreateSut(options);
        var result = sut.AllocateOffspring(species, 100);

        result[1].Should().Be(50,
            "default options should disable penalty — equal fitness means equal offspring");
        result[2].Should().Be(50,
            "default options should disable penalty — equal fitness means equal offspring");
    }

    #endregion

    #region Negative Adjusted Fitness

    [Fact]
    public void AllocateOffspring_NegativeAdjustedFitness_IsAllowed()
    {
        // Species 1: simple (2 nodes), fitness=10.0 → effective = 10 - 1*2 = 8.0
        // Species 2: complex (4 nodes), fitness=1.0 → effective = 1 - 1*4 = -3.0 (negative!)
        // Negative effective fitness is clamped to zero, so the species gets 0 offspring in allocation.
        var options = MakeOptions(coefficient: 1.0, ComplexityPenaltyMetric.NodeCount);
        var species = new List<Species>
        {
            MakeSpeciesWithGenome(1, MakeSimpleGenome(), 10, 10.0),
            MakeSpeciesWithGenome(2, MakeComplexGenome(), 10, 1.0)
        };

        var sut = CreateSut(options);

        // Should not throw — negative adjusted fitness is clamped to zero
        var result = sut.AllocateOffspring(species, 100);

        result.Values.Sum().Should().Be(100,
            "total offspring must equal population size even with negative effective fitness");
        result[1].Should().Be(100,
            "species with positive effective fitness gets all offspring");
        result[2].Should().Be(0,
            "species with negative effective fitness gets 0 offspring");
    }

    [Fact]
    public void AllocateOffspring_AllNegativeEffectiveFitness_DistributesViaProtection()
    {
        // Both species have negative effective fitness after penalty.
        // Species 1: simple (2 nodes), fitness=0.5 → effective = 0.5 - 1*2 = -1.5
        // Species 2: complex (4 nodes), fitness=0.5 → effective = 0.5 - 1*4 = -3.5
        // Total adjusted <= 0, so protected species distribute evenly.
        var options = MakeOptions(coefficient: 1.0, ComplexityPenaltyMetric.NodeCount);
        var species = new List<Species>
        {
            MakeSpeciesWithGenome(1, MakeSimpleGenome(), 10, 0.5),
            MakeSpeciesWithGenome(2, MakeComplexGenome(), 10, 0.5)
        };

        var sut = CreateSut(options);
        var result = sut.AllocateOffspring(species, 100);

        result.Values.Sum().Should().Be(100,
            "total offspring must equal population size even when all effective fitness is negative");
    }

    #endregion
}
