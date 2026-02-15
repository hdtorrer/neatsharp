using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution;

public class PopulationFactoryTests
{
    private static NeatSharpOptions DefaultOptions() => new();

    private static PopulationFactory CreateSut(NeatSharpOptions? options = null) =>
        new(Options.Create(options ?? DefaultOptions()));

    #region Correct Genome Count

    [Fact]
    public void CreateInitialPopulation_ReturnsCorrectGenomeCount()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(150, 2, 1, random, tracker);

        population.Should().HaveCount(150);
    }

    [Fact]
    public void CreateInitialPopulation_SingleGenome_ReturnsOne()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(1, 2, 1, random, tracker);

        population.Should().HaveCount(1);
    }

    #endregion

    #region Node Layout

    [Fact]
    public void CreateInitialPopulation_TwoInputsOneOutput_HasCorrectNodeLayout()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(1, 2, 1, random, tracker);
        var genome = population[0];

        // 2 inputs + 1 bias + 1 output = 4 nodes
        genome.Nodes.Should().HaveCount(4);

        // Inputs at 0, 1
        genome.Nodes[0].Should().Be(new NodeGene(0, NodeType.Input));
        genome.Nodes[1].Should().Be(new NodeGene(1, NodeType.Input));

        // Bias at 2
        genome.Nodes[2].Should().Be(new NodeGene(2, NodeType.Bias));

        // Output at 3
        genome.Nodes[3].Should().Be(new NodeGene(3, NodeType.Output));
    }

    [Fact]
    public void CreateInitialPopulation_ThreeInputsTwoOutputs_HasCorrectNodeLayout()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(1, 3, 2, random, tracker);
        var genome = population[0];

        // 3 inputs + 1 bias + 2 outputs = 6 nodes
        genome.Nodes.Should().HaveCount(6);

        // Inputs at 0, 1, 2
        genome.Nodes[0].Type.Should().Be(NodeType.Input);
        genome.Nodes[1].Type.Should().Be(NodeType.Input);
        genome.Nodes[2].Type.Should().Be(NodeType.Input);

        // Bias at 3
        genome.Nodes[3].Id.Should().Be(3);
        genome.Nodes[3].Type.Should().Be(NodeType.Bias);

        // Outputs at 4, 5
        genome.Nodes[4].Id.Should().Be(4);
        genome.Nodes[4].Type.Should().Be(NodeType.Output);
        genome.Nodes[5].Id.Should().Be(5);
        genome.Nodes[5].Type.Should().Be(NodeType.Output);
    }

    [Fact]
    public void CreateInitialPopulation_ReportsCorrectInputAndOutputCounts()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(1, 3, 2, random, tracker);
        var genome = population[0];

        genome.InputCount.Should().Be(3);
        genome.OutputCount.Should().Be(2);
    }

    #endregion

    #region Full Connectivity

    [Fact]
    public void CreateInitialPopulation_TwoInputsOneOutput_HasFullConnectivity()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(1, 2, 1, random, tracker);
        var genome = population[0];

        // (2 inputs + 1 bias) * 1 output = 3 connections
        genome.Connections.Should().HaveCount(3);

        // Input 0 → Output 3
        genome.Connections.Should().Contain(c => c.SourceNodeId == 0 && c.TargetNodeId == 3);
        // Input 1 → Output 3
        genome.Connections.Should().Contain(c => c.SourceNodeId == 1 && c.TargetNodeId == 3);
        // Bias 2 → Output 3
        genome.Connections.Should().Contain(c => c.SourceNodeId == 2 && c.TargetNodeId == 3);
    }

    [Fact]
    public void CreateInitialPopulation_ThreeInputsTwoOutputs_HasFullConnectivity()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(1, 3, 2, random, tracker);
        var genome = population[0];

        // (3 inputs + 1 bias) * 2 outputs = 8 connections
        genome.Connections.Should().HaveCount(8);

        // Each input and bias should connect to each output
        int[] sources = [0, 1, 2, 3]; // 3 inputs + 1 bias
        int[] targets = [4, 5]; // 2 outputs
        foreach (var src in sources)
        {
            foreach (var tgt in targets)
            {
                genome.Connections.Should().Contain(
                    c => c.SourceNodeId == src && c.TargetNodeId == tgt,
                    $"expected connection from {src} → {tgt}");
            }
        }
    }

    [Fact]
    public void CreateInitialPopulation_AllConnectionsEnabled()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(1, 2, 1, random, tracker);
        var genome = population[0];

        genome.Connections.Should().AllSatisfy(c => c.IsEnabled.Should().BeTrue());
    }

    #endregion

    #region Innovation Numbers via Tracker Dedup

    [Fact]
    public void CreateInitialPopulation_InnovationNumbersStartAtZero()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(1, 2, 1, random, tracker);
        var genome = population[0];

        // 3 connections: innovation numbers 0, 1, 2
        genome.Connections.Select(c => c.InnovationNumber).Should()
            .BeEquivalentTo([0, 1, 2]);
    }

    [Fact]
    public void CreateInitialPopulation_AllGenomesShareSameInnovationNumbers()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(10, 2, 1, random, tracker);

        // All genomes should have identical innovation numbers
        var expectedInnovations = population[0].Connections
            .Select(c => c.InnovationNumber)
            .ToList();

        foreach (var genome in population)
        {
            genome.Connections.Select(c => c.InnovationNumber).Should()
                .BeEquivalentTo(expectedInnovations);
        }
    }

    [Fact]
    public void CreateInitialPopulation_AllGenomesShareSameTopology()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(10, 2, 1, random, tracker);

        var expectedConnections = population[0].Connections
            .Select(c => (c.SourceNodeId, c.TargetNodeId, c.InnovationNumber))
            .ToList();

        foreach (var genome in population)
        {
            genome.Connections.Select(c => (c.SourceNodeId, c.TargetNodeId, c.InnovationNumber)).Should()
                .BeEquivalentTo(expectedConnections);
        }
    }

    #endregion

    #region Randomized Weights

    [Fact]
    public void CreateInitialPopulation_WeightsWithinDefaultBounds()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(50, 2, 1, random, tracker);

        foreach (var genome in population)
        {
            genome.Connections.Should().AllSatisfy(c =>
            {
                c.Weight.Should().BeGreaterThanOrEqualTo(-4.0);
                c.Weight.Should().BeLessThanOrEqualTo(4.0);
            });
        }
    }

    [Fact]
    public void CreateInitialPopulation_WeightsWithinCustomBounds()
    {
        var options = new NeatSharpOptions();
        options.Mutation.WeightMinValue = -1.0;
        options.Mutation.WeightMaxValue = 1.0;
        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(50, 2, 1, random, tracker);

        foreach (var genome in population)
        {
            genome.Connections.Should().AllSatisfy(c =>
            {
                c.Weight.Should().BeGreaterThanOrEqualTo(-1.0);
                c.Weight.Should().BeLessThanOrEqualTo(1.0);
            });
        }
    }

    [Fact]
    public void CreateInitialPopulation_DifferentGenomesHaveDifferentWeights()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(10, 2, 1, random, tracker);

        // At least some genomes should have different weights
        var allFirstWeights = population.Select(g => g.Connections[0].Weight).ToList();
        allFirstWeights.Distinct().Count().Should().BeGreaterThan(1,
            "different genomes should have different randomized weights");
    }

    #endregion

    #region Deterministic Output with Same Seed

    [Fact]
    public void CreateInitialPopulation_SameSeed_ProducesIdenticalResults()
    {
        var sut = CreateSut();

        var population1 = sut.CreateInitialPopulation(
            10, 2, 1, new Random(42), new InnovationTracker());
        var population2 = sut.CreateInitialPopulation(
            10, 2, 1, new Random(42), new InnovationTracker());

        population1.Should().HaveCount(population2.Count);

        for (int i = 0; i < population1.Count; i++)
        {
            var g1 = population1[i];
            var g2 = population2[i];

            g1.Nodes.Should().BeEquivalentTo(g2.Nodes);

            for (int j = 0; j < g1.Connections.Count; j++)
            {
                g1.Connections[j].InnovationNumber.Should().Be(g2.Connections[j].InnovationNumber);
                g1.Connections[j].SourceNodeId.Should().Be(g2.Connections[j].SourceNodeId);
                g1.Connections[j].TargetNodeId.Should().Be(g2.Connections[j].TargetNodeId);
                g1.Connections[j].Weight.Should().Be(g2.Connections[j].Weight);
                g1.Connections[j].IsEnabled.Should().Be(g2.Connections[j].IsEnabled);
            }
        }
    }

    [Fact]
    public void CreateInitialPopulation_DifferentSeeds_ProduceDifferentWeights()
    {
        var sut = CreateSut();

        var population1 = sut.CreateInitialPopulation(
            10, 2, 1, new Random(42), new InnovationTracker());
        var population2 = sut.CreateInitialPopulation(
            10, 2, 1, new Random(99), new InnovationTracker());

        // Weights should differ between different seeds
        var weights1 = population1.SelectMany(g => g.Connections.Select(c => c.Weight)).ToList();
        var weights2 = population2.SelectMany(g => g.Connections.Select(c => c.Weight)).ToList();

        weights1.Should().NotBeEquivalentTo(weights2,
            "different seeds should produce different weight distributions");
    }

    #endregion

    #region Single Input Single Output

    [Fact]
    public void CreateInitialPopulation_OneInputOneOutput_HasCorrectStructure()
    {
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker();

        var population = sut.CreateInitialPopulation(1, 1, 1, random, tracker);
        var genome = population[0];

        // 1 input + 1 bias + 1 output = 3 nodes
        genome.Nodes.Should().HaveCount(3);
        genome.Nodes[0].Should().Be(new NodeGene(0, NodeType.Input));
        genome.Nodes[1].Should().Be(new NodeGene(1, NodeType.Bias));
        genome.Nodes[2].Should().Be(new NodeGene(2, NodeType.Output));

        // (1 input + 1 bias) * 1 output = 2 connections
        genome.Connections.Should().HaveCount(2);
    }

    #endregion
}
