using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NeatSharp.Exceptions;
using NeatSharp.Extensions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Genetics;

/// <summary>
/// Validates that all code samples from specs/002-genome-phenotype/quickstart.md
/// compile and produce the expected outputs.
/// </summary>
public class QuickstartValidationTests
{
    /// <summary>
    /// Quickstart Sample 1: Build a Simple Genome and Run Inference.
    /// 2 inputs + 1 bias + 1 output, verify sigmoid(0.6) ≈ 0.6457.
    /// </summary>
    [Fact]
    public void Sample1_SimpleGenomeInference_ProducesExpectedOutput()
    {
        // Set up DI
        var services = new ServiceCollection();
        services.AddNeatSharp();
        var provider = services.BuildServiceProvider();

        // Define node genes: 2 inputs + 1 bias + 1 output
        var nodes = new NodeGene[]
        {
            new(Id: 0, Type: NodeType.Input),
            new(Id: 1, Type: NodeType.Input),
            new(Id: 2, Type: NodeType.Bias),
            new(Id: 3, Type: NodeType.Output),
        };

        // Define connection genes with known weights
        var connections = new ConnectionGene[]
        {
            new(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true),
            new(InnovationNumber: 2, SourceNodeId: 1, TargetNodeId: 3, Weight: 0.8, IsEnabled: true),
            new(InnovationNumber: 3, SourceNodeId: 2, TargetNodeId: 3, Weight: -0.3, IsEnabled: true),
        };

        // Create the immutable genome
        var genome = new Genome(nodes, connections);

        // Build the feed-forward phenotype
        var builder = provider.GetRequiredService<INetworkBuilder>();
        var network = builder.Build(genome);

        // Run inference
        ReadOnlySpan<double> inputs = [1.0, 0.5];
        Span<double> outputs = stackalloc double[1];
        network.Activate(inputs, outputs);

        // Output: sigmoid(1.0*0.5 + 0.5*0.8 + 1.0*(-0.3)) = sigmoid(0.6) ≈ 0.6457
        double expected = 1.0 / (1.0 + Math.Exp(-0.6));
        outputs[0].Should().BeApproximately(expected, 1e-10);
    }

    /// <summary>
    /// Quickstart Sample 2: Genome with Hidden Layers and Custom Activation.
    /// Hidden node with tanh, output with default sigmoid.
    /// </summary>
    [Fact]
    public void Sample2_HiddenLayerWithTanh_ProducesExpectedOutput()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp();
        var provider = services.BuildServiceProvider();
        var builder = provider.GetRequiredService<INetworkBuilder>();

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Bias),
            new(3, NodeType.Hidden, ActivationFunctions.Tanh),
            new(4, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 3, Weight: -1.0, IsEnabled: true),
            new(3, SourceNodeId: 2, TargetNodeId: 3, Weight: 0.0, IsEnabled: true),
            new(4, SourceNodeId: 3, TargetNodeId: 4, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = builder.Build(genome);

        Span<double> outputs = stackalloc double[1];
        network.Activate([1.0, 0.0], outputs);

        // Hidden node: tanh(1.0*1.0 + 0.0*(-1.0) + 1.0*0.0) = tanh(1.0) ≈ 0.7616
        double hiddenValue = Math.Tanh(1.0);
        // Output node: sigmoid(0.7616*1.0) ≈ 0.6819
        double expected = 1.0 / (1.0 + Math.Exp(-hiddenValue));
        outputs[0].Should().BeApproximately(expected, 1e-10);
    }

    /// <summary>
    /// Quickstart Sample 3: Disabled Connections.
    /// Disabled connection does not contribute to output.
    /// </summary>
    [Fact]
    public void Sample3_DisabledConnections_ExcludedFromOutput()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp();
        var provider = services.BuildServiceProvider();
        var builder = provider.GetRequiredService<INetworkBuilder>();

        var nodes = new NodeGene[]
        {
            new(Id: 0, Type: NodeType.Input),
            new(Id: 1, Type: NodeType.Input),
            new(Id: 2, Type: NodeType.Bias),
            new(Id: 3, Type: NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 3, Weight: 0.8, IsEnabled: false), // Disabled!
            new(3, SourceNodeId: 2, TargetNodeId: 3, Weight: -0.3, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = builder.Build(genome);

        Span<double> outputs = stackalloc double[1];
        network.Activate([1.0, 0.5], outputs);

        // Only connections 1 and 3 contribute; connection 2 is ignored
        // sigmoid(1.0*0.5 + 1.0*(-0.3)) = sigmoid(0.2) ≈ 0.5498
        double expected = 1.0 / (1.0 + Math.Exp(-0.2));
        outputs[0].Should().BeApproximately(expected, 1e-10);
    }

    /// <summary>
    /// Quickstart Sample 4: Innovation Tracking.
    /// Deterministic deduplication within a generation, fresh IDs after NextGeneration().
    /// </summary>
    [Fact]
    public void Sample4_InnovationTracking_DeterministicDeduplication()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp();
        var provider = services.BuildServiceProvider();

        using var scope = provider.CreateScope();
        var tracker = scope.ServiceProvider.GetRequiredService<IInnovationTracker>();

        // Same structural change in the same generation → same innovation ID
        int id1 = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);
        int id2 = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);
        id1.Should().Be(id2);

        // Different structural change → different innovation ID
        int id3 = tracker.GetConnectionInnovation(sourceNodeId: 1, targetNodeId: 5);
        id3.Should().NotBe(id1);

        // Node split: splitting a connection produces deterministic IDs
        var split = tracker.GetNodeSplitInnovation(connectionInnovation: 1);
        split.NewNodeId.Should().BeGreaterThanOrEqualTo(0);
        split.IncomingConnectionInnovation.Should().BeGreaterThanOrEqualTo(0);
        split.OutgoingConnectionInnovation.Should().BeGreaterThanOrEqualTo(0);

        // Same split in same generation → same result
        var split2 = tracker.GetNodeSplitInnovation(connectionInnovation: 1);
        split2.Should().Be(split);

        // Advance generation (clears dedup cache, preserves counters)
        tracker.NextGeneration();

        // Same connection after NextGeneration → new ID (cache cleared)
        int id4 = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);
        id4.Should().NotBe(id1);
        id4.Should().BeGreaterThan(id1);
    }

    /// <summary>
    /// Quickstart Sample 5: Custom Activation Functions.
    /// Register and use a custom activation function.
    /// </summary>
    [Fact]
    public void Sample5_CustomActivationFunction_RegisterAndUse()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp();
        var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<IActivationFunctionRegistry>();

        // Register a custom activation function before evolution
        registry.Register("leaky_relu", x => x > 0.0 ? x : 0.01 * x);

        // Use it in a node gene
        var hiddenNode = new NodeGene(Id: 5, Type: NodeType.Hidden, ActivationFunction: "leaky_relu");
        hiddenNode.ActivationFunction.Should().Be("leaky_relu");

        // Verify it can be retrieved
        var func = registry.Get("leaky_relu");
        func(-1.0).Should().BeApproximately(-0.01, 1e-10);
        func(1.0).Should().BeApproximately(1.0, 1e-10);
    }

    /// <summary>
    /// Quickstart Sample 6: Error Handling.
    /// CycleDetectedException, InputDimensionMismatchException, InvalidGenomeException.
    /// </summary>
    [Fact]
    public void Sample6_CycleDetection_ThrowsCycleDetectedException()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp();
        var provider = services.BuildServiceProvider();
        var builder = provider.GetRequiredService<INetworkBuilder>();

        var cyclicNodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(3, NodeType.Hidden),
            new(4, NodeType.Hidden),
            new(5, NodeType.Output),
        };
        var cyclicConnections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 3, TargetNodeId: 4, Weight: 1.0, IsEnabled: true),
            new(3, SourceNodeId: 4, TargetNodeId: 3, Weight: 1.0, IsEnabled: true), // Cycle!
            new(4, SourceNodeId: 4, TargetNodeId: 5, Weight: 1.0, IsEnabled: true),
        };
        var cyclicGenome = new Genome(cyclicNodes, cyclicConnections);

        var act = () => builder.Build(cyclicGenome);

        act.Should().Throw<CycleDetectedException>();
    }

    [Fact]
    public void Sample6_InputDimensionMismatch_ThrowsInputDimensionMismatchException()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp();
        var provider = services.BuildServiceProvider();
        var builder = provider.GetRequiredService<INetworkBuilder>();

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
        };
        var genome = new Genome(nodes, connections);
        var network = builder.Build(genome);

        var outputBuffer = new double[1];
        var act = () => network.Activate([1.0], outputBuffer); // Expected 2 inputs, got 1

        act.Should().Throw<InputDimensionMismatchException>()
            .Where(ex => ex.Expected == 2 && ex.Actual == 1);
    }

    [Fact]
    public void Sample6_InvalidGenome_ThrowsInvalidGenomeException()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
        };
        var badConnection = new ConnectionGene(1, SourceNodeId: 0, TargetNodeId: 99, Weight: 1.0, IsEnabled: true);

        var act = () => new Genome(nodes, [badConnection]); // Node 99 doesn't exist

        act.Should().Throw<InvalidGenomeException>();
    }

    /// <summary>
    /// Quickstart Sample 7: Champion Inference (Post-Evolution).
    /// Deterministic repeated activation.
    /// </summary>
    [Fact]
    public void Sample7_ChampionInference_DeterministicRepeatedActivation()
    {
        var services = new ServiceCollection();
        services.AddNeatSharp();
        var provider = services.BuildServiceProvider();
        var builder = provider.GetRequiredService<INetworkBuilder>();

        // Build a known genome to simulate a champion
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Bias),
            new(3, NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 3, Weight: 0.8, IsEnabled: true),
            new(3, SourceNodeId: 2, TargetNodeId: 3, Weight: -0.3, IsEnabled: true),
        };
        var genome = new Genome(nodes, connections);
        IGenome champion = builder.Build(genome);

        // Activate repeatedly with same inputs — deterministic results
        Span<double> outputs = stackalloc double[1];

        champion.Activate([0.0, 0.0], outputs);
        double firstResult00 = outputs[0];

        champion.Activate([1.0, 1.0], outputs);
        double result11 = outputs[0];

        // Same inputs always produce same outputs
        champion.Activate([0.0, 0.0], outputs);
        outputs[0].Should().Be(firstResult00);

        // Different inputs produce different results
        result11.Should().NotBe(firstResult00);

        // Verify [0,0] result: sigmoid(0*0.5 + 0*0.8 + 1.0*(-0.3)) = sigmoid(-0.3)
        double expected00 = 1.0 / (1.0 + Math.Exp(0.3));
        firstResult00.Should().BeApproximately(expected00, 1e-10);

        // Verify [1,1] result: sigmoid(1.0*0.5 + 1.0*0.8 + 1.0*(-0.3)) = sigmoid(1.0)
        double expected11 = 1.0 / (1.0 + Math.Exp(-1.0));
        result11.Should().BeApproximately(expected11, 1e-10);
    }
}
