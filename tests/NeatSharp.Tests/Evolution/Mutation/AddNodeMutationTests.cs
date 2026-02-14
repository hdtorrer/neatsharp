using FluentAssertions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evolution.Mutation;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Evolution.Mutation;

public class AddNodeMutationTests
{
    private static readonly IReadOnlyList<NodeGene> MinimalNodes =
    [
        new(0, NodeType.Input),
        new(1, NodeType.Output)
    ];

    private static NeatSharpOptions DefaultOptions() => new();

    private static AddNodeMutation CreateSut(NeatSharpOptions? options = null) =>
        new(Options.Create(options ?? DefaultOptions()));

    [Fact]
    public void Mutate_DisablesSelectedConnection()
    {
        var connection = new ConnectionGene(0, 0, 1, 0.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        // Original connection should be disabled
        var originalConn = result.Connections.First(c => c.InnovationNumber == 0);
        originalConn.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Mutate_CreatesNewHiddenNode()
    {
        var connection = new ConnectionGene(0, 0, 1, 0.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        result.Nodes.Count.Should().Be(3);
        var newNode = result.Nodes.First(n => n.Type == NodeType.Hidden);
        newNode.ActivationFunction.Should().Be(ActivationFunctions.Identity);
    }

    [Fact]
    public void Mutate_SourceToNewConnectionHasWeight1()
    {
        var connection = new ConnectionGene(0, 0, 1, 0.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        // Find the new hidden node
        var newNode = result.Nodes.First(n => n.Type == NodeType.Hidden);

        // Connection from source (0) to new node should have weight 1.0
        var sourceToNew = result.Connections.First(c =>
            c.SourceNodeId == 0 && c.TargetNodeId == newNode.Id && c.IsEnabled);
        sourceToNew.Weight.Should().Be(1.0);
    }

    [Fact]
    public void Mutate_NewToTargetConnectionHasOriginalWeight()
    {
        var connection = new ConnectionGene(0, 0, 1, 0.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        var newNode = result.Nodes.First(n => n.Type == NodeType.Hidden);

        // Connection from new node to target (1) should have original weight
        var newToTarget = result.Connections.First(c =>
            c.SourceNodeId == newNode.Id && c.TargetNodeId == 1 && c.IsEnabled);
        newToTarget.Weight.Should().Be(0.5);
    }

    [Fact]
    public void Mutate_InnovationIdsFromTracker()
    {
        var connection = new ConnectionGene(0, 0, 1, 0.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        // The tracker should have assigned the split innovation
        var splitResult = new InnovationTracker(10, 10).GetNodeSplitInnovation(0);

        var newNode = result.Nodes.First(n => n.Type == NodeType.Hidden);
        newNode.Id.Should().Be(splitResult.NewNodeId);

        var sourceToNew = result.Connections.First(c =>
            c.SourceNodeId == 0 && c.TargetNodeId == newNode.Id && c.IsEnabled);
        sourceToNew.InnovationNumber.Should().Be(splitResult.IncomingConnectionInnovation);

        var newToTarget = result.Connections.First(c =>
            c.SourceNodeId == newNode.Id && c.TargetNodeId == 1 && c.IsEnabled);
        newToTarget.InnovationNumber.Should().Be(splitResult.OutgoingConnectionInnovation);
    }

    [Fact]
    public void Mutate_MaxNodesReached_ReturnsUnchanged()
    {
        var options = DefaultOptions();
        options.Complexity.MaxNodes = 2; // Already has 2 nodes
        var connection = new ConnectionGene(0, 0, 1, 0.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut(options);
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        result.Nodes.Count.Should().Be(2);
        result.Connections.Count.Should().Be(1);
    }

    [Fact]
    public void Mutate_NoEnabledConnections_ReturnsUnchanged()
    {
        var connection = new ConnectionGene(0, 0, 1, 0.5, false);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        result.Should().BeSameAs(genome);
    }

    [Fact]
    public void Mutate_ZeroConnections_ReturnsUnchanged()
    {
        var genome = new Genome(MinimalNodes, Array.Empty<ConnectionGene>());
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        result.Should().BeSameAs(genome);
    }

    [Fact]
    public void Mutate_Deterministic_SameResultWithSameSeed()
    {
        var connection = new ConnectionGene(0, 0, 1, 0.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();

        var result1 = sut.Mutate(genome, new Random(42), new InnovationTracker(10, 10));
        var result2 = sut.Mutate(genome, new Random(42), new InnovationTracker(10, 10));

        result1.Nodes.Count.Should().Be(result2.Nodes.Count);
        result1.Connections.Count.Should().Be(result2.Connections.Count);
        for (int i = 0; i < result1.Connections.Count; i++)
        {
            result1.Connections[i].Should().Be(result2.Connections[i]);
        }
    }

    [Fact]
    public void Mutate_OriginalGenomeNotModified()
    {
        var connection = new ConnectionGene(0, 0, 1, 0.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        _ = sut.Mutate(genome, random, tracker);

        genome.Nodes.Count.Should().Be(2);
        genome.Connections.Count.Should().Be(1);
        genome.Connections[0].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Mutate_PhenotypeEquivalence_OriginalAndMutatedProduceSameOutputs()
    {
        // SC-003: Build phenotype via INetworkBuilder for original and mutated genomes,
        // activate with 100 random input vectors using fixed seed,
        // assert outputs match within 1e-6 epsilon
        var connection = new ConnectionGene(0, 0, 1, 0.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var mutated = sut.Mutate(genome, random, tracker);

        // Build phenotypes
        var registry = new ActivationFunctionRegistry();
        var builder = new FeedForwardNetworkBuilder(registry);
        var originalNetwork = builder.Build(genome);
        var mutatedNetwork = builder.Build(mutated);

        // Activate with 100 random input vectors
        var inputRng = new Random(123);
        Span<double> originalInputs = stackalloc double[1];
        Span<double> originalOutputs = stackalloc double[1];
        Span<double> mutatedInputs = stackalloc double[1];
        Span<double> mutatedOutputs = stackalloc double[1];

        for (int i = 0; i < 100; i++)
        {
            double input = inputRng.NextDouble() * 2.0 - 1.0;

            originalInputs[0] = input;
            mutatedInputs[0] = input;

            originalNetwork.Activate(originalInputs, originalOutputs);
            mutatedNetwork.Activate(mutatedInputs, mutatedOutputs);

            mutatedOutputs[0].Should().BeApproximately(originalOutputs[0], 1e-6,
                $"output should match for input {input} (iteration {i})");
        }
    }

    [Fact]
    public void Mutate_ResultHasCorrectStructure()
    {
        var connection = new ConnectionGene(0, 0, 1, 0.5, true);
        var genome = new Genome(MinimalNodes, [connection]);
        var sut = CreateSut();
        var random = new Random(42);
        var tracker = new InnovationTracker(10, 10);

        var result = sut.Mutate(genome, random, tracker);

        // Should have 3 nodes (input, output, new hidden)
        result.Nodes.Count.Should().Be(3);
        // Should have 3 connections (original disabled + 2 new)
        result.Connections.Count.Should().Be(3);

        // Verify we have exactly 1 disabled and 2 enabled connections
        result.Connections.Count(c => !c.IsEnabled).Should().Be(1);
        result.Connections.Count(c => c.IsEnabled).Should().Be(2);
    }
}
