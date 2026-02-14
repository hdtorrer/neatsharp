using FluentAssertions;
using NeatSharp.Evolution.Mutation;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Mutation;

public class ToggleEnableMutationTests
{
    private static readonly IReadOnlyList<NodeGene> MinimalNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output)
    ];

    private static ToggleEnableMutation CreateSut() => new();

    [Fact]
    public void Mutate_EnabledConnection_BecomesDisabled()
    {
        var connection = new ConnectionGene(0, 0, 1, 1.0, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        result.Connections[0].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Mutate_DisabledConnection_BecomesEnabled()
    {
        var connection = new ConnectionGene(0, 0, 1, 1.0, false);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        result.Connections[0].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Mutate_MultipleConnections_TogglesExactlyOne()
    {
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true),
            new(1, 0, 1, 2.0, true),
            new(2, 0, 1, 3.0, false)
        };
        var genome = new Genome(MinimalNodes, connections);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        int toggledCount = 0;
        for (int i = 0; i < result.Connections.Count; i++)
        {
            if (result.Connections[i].IsEnabled != connections[i].IsEnabled)
                toggledCount++;
        }

        toggledCount.Should().Be(1, "exactly one connection should be toggled");
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
        var connection = new ConnectionGene(0, 0, 1, 1.0, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        _ = sut.Mutate(genome, random, tracker);

        genome.Connections[0].IsEnabled.Should().BeTrue();
        genome.Connections[0].Weight.Should().Be(1.0);
    }

    [Fact]
    public void Mutate_WeightPreserved()
    {
        var connection = new ConnectionGene(0, 0, 1, 1.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(100, 100);

        var result = sut.Mutate(genome, random, tracker);

        result.Connections[0].Weight.Should().Be(1.5);
    }

    [Fact]
    public void Mutate_Deterministic_SameResultWithSameSeed()
    {
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true),
            new(1, 0, 1, 2.0, true),
            new(2, 0, 1, 3.0, false)
        };
        var genome = new Genome(MinimalNodes, connections);
        var sut = CreateSut();

        var result1 = sut.Mutate(genome, new Random(42), new InnovationTracker(100, 100));
        var result2 = sut.Mutate(genome, new Random(42), new InnovationTracker(100, 100));

        for (int i = 0; i < result1.Connections.Count; i++)
        {
            result1.Connections[i].IsEnabled.Should().Be(result2.Connections[i].IsEnabled);
        }
    }
}
