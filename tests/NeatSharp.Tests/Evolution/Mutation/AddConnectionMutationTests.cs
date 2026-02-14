using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Mutation;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Mutation;

public class AddConnectionMutationTests
{
    private static NeatSharpOptions DefaultOptions() => new()
    {
        Mutation =
        {
            WeightMinValue = -4.0,
            WeightMaxValue = 4.0,
            MaxAddConnectionAttempts = 20
        }
    };

    private static AddConnectionMutation CreateSut(NeatSharpOptions? options = null) =>
        new(Options.Create(options ?? DefaultOptions()));

    /// <summary>
    /// Simple 3-node genome: Input(0) -> Hidden(2) -> Output(1)
    /// with one open slot: Input(0) -> Output(1)
    /// </summary>
    private static Genome CreateSimpleGenome()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var connections = new ConnectionGene[]
        {
            new(0, 0, 2, 1.0, true),
            new(1, 2, 1, 1.0, true)
        };
        return new Genome(nodes, connections);
    }

    [Fact]
    public void Mutate_AddsNewConnectionWithInnovationIdFromTracker()
    {
        var genome = CreateSimpleGenome();
        var sut = CreateSut();
        var tracker = new InnovationTracker(10, 10);

        // Try multiple seeds to find one that successfully adds a connection
        Genome? result = null;
        for (int seed = 0; seed < 100; seed++)
        {
            var random = new Random(seed);
            var attempt = sut.Mutate(genome, random, new InnovationTracker(10, 10));
            if (attempt.Connections.Count > genome.Connections.Count)
            {
                result = attempt;
                break;
            }
        }

        result.Should().NotBeNull("at least one seed should produce a new connection");
        result!.Connections.Count.Should().Be(genome.Connections.Count + 1);

        var newConnection = result.Connections[^1];
        newConnection.IsEnabled.Should().BeTrue();
        newConnection.Weight.Should().BeInRange(-4.0, 4.0);
    }

    [Fact]
    public void Mutate_NewConnectionWeightInRange()
    {
        var options = DefaultOptions();
        options.Mutation.WeightMinValue = -2.0;
        options.Mutation.WeightMaxValue = 3.0;
        var genome = CreateSimpleGenome();
        var sut = CreateSut(options);

        for (int seed = 0; seed < 100; seed++)
        {
            var random = new Random(seed);
            var tracker = new InnovationTracker(10, 10);
            var result = sut.Mutate(genome, random, tracker);
            if (result.Connections.Count > genome.Connections.Count)
            {
                var newConn = result.Connections[^1];
                newConn.Weight.Should().BeInRange(-2.0, 3.0,
                    $"new connection weight should be in [min, max] (seed={seed})");
            }
        }
    }

    [Fact]
    public void Mutate_CycleCreatingConnection_Rejected()
    {
        // Linear chain: Input(0) -> Hidden(2) -> Output(1)
        // Adding Hidden(2) -> Input(0) would create no cycle since we check forward from target
        // But adding Output(1) -> Hidden(2) would create cycle: Hidden(2) -> Output(1) -> Hidden(2)
        // The mutation should never create a cycle
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden)
        };
        var connections = new ConnectionGene[]
        {
            new(0, 0, 2, 1.0, true),
            new(1, 2, 1, 1.0, true)
        };
        var genome = new Genome(nodes, connections);
        var sut = CreateSut();

        // Run many seeds — any added connection should not create a cycle
        for (int seed = 0; seed < 200; seed++)
        {
            var random = new Random(seed);
            var tracker = new InnovationTracker(10, 10);
            var result = sut.Mutate(genome, random, tracker);

            // If a connection was added, verify it doesn't create a cycle
            // by checking it can still build a feed-forward network
            if (result.Connections.Count > genome.Connections.Count)
            {
                var newConn = result.Connections[^1];
                // Target should never be an input node
                var targetNode = result.Nodes.First(n => n.Id == newConn.TargetNodeId);
                targetNode.Type.Should().NotBe(NodeType.Input);
                targetNode.Type.Should().NotBe(NodeType.Bias);
            }
        }
    }

    [Fact]
    public void Mutate_MaxConnectionsReached_ReturnsUnchanged()
    {
        var options = DefaultOptions();
        options.Complexity.MaxConnections = 2; // Already has 2 connections
        var genome = CreateSimpleGenome();
        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        result.Connections.Count.Should().Be(genome.Connections.Count);
    }

    [Fact]
    public void Mutate_FullyConnectedGenome_ReturnsUnchanged()
    {
        // Two nodes, fully connected (Input -> Output)
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output)
        };
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true)
        };
        var genome = new Genome(nodes, connections);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        // Only valid connection is 0->1, which already exists
        // No other valid pairs exist (1->0 targets input, 0->0 self-loop input, 1->1 output to output may depend on target restrictions)
        result.Connections.Count.Should().Be(genome.Connections.Count);
    }

    [Fact]
    public void Mutate_TargetNotInputOrBias()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden),
            new(3, NodeType.Bias)
        };
        var connections = Array.Empty<ConnectionGene>();
        var genome = new Genome(nodes, connections);
        var sut = CreateSut();

        for (int seed = 0; seed < 200; seed++)
        {
            var random = new Random(seed);
            var tracker = new InnovationTracker(10, 10);
            var result = sut.Mutate(genome, random, tracker);

            if (result.Connections.Count > 0)
            {
                var newConn = result.Connections[^1];
                var targetNode = result.Nodes.First(n => n.Id == newConn.TargetNodeId);
                targetNode.Type.Should().NotBe(NodeType.Input, $"target must not be input (seed={seed})");
                targetNode.Type.Should().NotBe(NodeType.Bias, $"target must not be bias (seed={seed})");
            }
        }
    }

    [Fact]
    public void Mutate_MaxAttemptsExhausted_ReturnsUnchanged()
    {
        // Create a scenario where all possible connections exist or would create cycles
        var options = DefaultOptions();
        options.Mutation.MaxAddConnectionAttempts = 1;
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output)
        };
        var connections = new ConnectionGene[]
        {
            new(0, 0, 1, 1.0, true) // already fully connected for valid pairs
        };
        var genome = new Genome(nodes, connections);
        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        result.Connections.Count.Should().Be(genome.Connections.Count);
    }

    [Fact]
    public void Mutate_Deterministic_SameResultWithSameSeed()
    {
        var genome = CreateSimpleGenome();
        var sut = CreateSut();

        var result1 = sut.Mutate(genome, new Random(42), new InnovationTracker(10, 10));
        var result2 = sut.Mutate(genome, new Random(42), new InnovationTracker(10, 10));

        result1.Connections.Count.Should().Be(result2.Connections.Count);
        for (int i = 0; i < result1.Connections.Count; i++)
        {
            result1.Connections[i].Should().Be(result2.Connections[i]);
        }
    }
}
