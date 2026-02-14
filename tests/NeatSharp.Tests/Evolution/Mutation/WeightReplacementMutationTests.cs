using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Mutation;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Mutation;

public class WeightReplacementMutationTests
{
    private static readonly IReadOnlyList<NodeGene> MinimalNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output)
    ];

    private static NeatSharpOptions DefaultOptions() => new()
    {
        Mutation =
        {
            WeightMinValue = -4.0,
            WeightMaxValue = 4.0
        }
    };

    private static WeightReplacementMutation CreateSut(NeatSharpOptions? options = null) =>
        new(Options.Create(options ?? DefaultOptions()));

    [Fact]
    public void Mutate_ReplacesOneConnectionWeight()
    {
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true),
            new(1, 0, 1, 2.0, true),
            new(2, 0, 1, 3.0, true)
        };
        var genome = new Genome(MinimalNodes, connections);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        // Exactly one weight should differ
        int changedCount = 0;
        for (int i = 0; i < result.Connections.Count; i++)
        {
            if (result.Connections[i].Weight != connections[i].Weight)
                changedCount++;
        }

        changedCount.Should().Be(1, "exactly one connection weight should be replaced");
    }

    [Fact]
    public void Mutate_ReplacedWeightWithinRange()
    {
        var options = DefaultOptions();
        options.Mutation.WeightMinValue = -2.0;
        options.Mutation.WeightMaxValue = 3.0;
        var connection = new ConnectionGene(0, 0, 1, 0.0, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut(options);

        for (int seed = 0; seed < 100; seed++)
        {
            var random = new Random(seed);
            var tracker = new InnovationTracker(100, 100);
            var result = sut.Mutate(genome, random, tracker);
            result.Connections[0].Weight.Should().BeInRange(-2.0, 3.0,
                $"replaced weight should be in [min, max] (seed={seed})");
        }
    }

    [Fact]
    public void Mutate_ZeroConnections_ReturnsGenomeUnchanged()
    {
        var genome = new Genome(MinimalNodes, Array.Empty<ConnectionGene>());
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        result.Should().BeSameAs(genome);
    }

    [Fact]
    public void Mutate_OriginalGenomeNotModified()
    {
        var connection = new ConnectionGene(0, 0, 1, 1.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        _ = sut.Mutate(genome, random, tracker);

        genome.Connections[0].Weight.Should().Be(1.5);
    }

    [Fact]
    public void Mutate_Deterministic_SameResultWithSameSeed()
    {
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true),
            new(1, 0, 1, 2.0, true)
        };
        var genome = new Genome(MinimalNodes, connections);
        var sut = CreateSut();

        var result1 = sut.Mutate(genome, new Random(42), new InnovationTracker(100, 100));
        var result2 = sut.Mutate(genome, new Random(42), new InnovationTracker(100, 100));

        for (int i = 0; i < result1.Connections.Count; i++)
        {
            result1.Connections[i].Weight.Should().Be(result2.Connections[i].Weight);
        }
    }

    [Fact]
    public void Mutate_StructureUnchanged()
    {
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true),
            new(1, 0, 1, 2.0, true)
        };
        var genome = new Genome(MinimalNodes, connections);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        result.Nodes.Should().HaveCount(genome.Nodes.Count);
        result.Connections.Should().HaveCount(genome.Connections.Count);
        for (int i = 0; i < result.Connections.Count; i++)
        {
            result.Connections[i].InnovationNumber.Should().Be(connections[i].InnovationNumber);
            result.Connections[i].SourceNodeId.Should().Be(connections[i].SourceNodeId);
            result.Connections[i].TargetNodeId.Should().Be(connections[i].TargetNodeId);
            result.Connections[i].IsEnabled.Should().Be(connections[i].IsEnabled);
        }
    }
}
