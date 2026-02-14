using FluentAssertions;
using NeatSharp.Exceptions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Genetics;

public class NetworkBuilderTests
{
    private readonly INetworkBuilder _builder;

    public NetworkBuilderTests()
    {
        var registry = new ActivationFunctionRegistry();
        _builder = new FeedForwardNetworkBuilder(registry);
    }

    [Fact]
    public void Build_ValidAcyclicGenome_ReturnsIGenome()
    {
        var genome = CreateSimpleGenome();

        var network = _builder.Build(genome);

        network.Should().NotBeNull();
        network.Should().BeAssignableTo<IGenome>();
    }

    [Fact]
    public void Build_ValidGenome_NodeCountReturnsReachableNodeCount()
    {
        // 2 inputs + 1 bias + 1 output = 4 reachable nodes
        var genome = CreateSimpleGenome();

        var network = _builder.Build(genome);

        network.NodeCount.Should().Be(4);
    }

    [Fact]
    public void Build_ValidGenome_ConnectionCountReturnsEnabledReachableConnectionCount()
    {
        // 3 enabled connections
        var genome = CreateSimpleGenome();

        var network = _builder.Build(genome);

        network.ConnectionCount.Should().Be(3);
    }

    [Fact]
    public void Build_DisabledConnections_ExcludedFromConnectionCount()
    {
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
            new(2, SourceNodeId: 1, TargetNodeId: 3, Weight: 0.8, IsEnabled: false), // Disabled
            new(3, SourceNodeId: 2, TargetNodeId: 3, Weight: -0.3, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);

        network.ConnectionCount.Should().Be(2);
    }

    [Fact]
    public void Build_UnreachableHiddenNode_PrunedFromNodeCount()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden), // Unreachable — no path to output
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 0, TargetNodeId: 2, Weight: 1.0, IsEnabled: true), // Leads to unreachable node
        };

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);

        // Node 2 is forward-reachable from input but not backward-reachable from output
        network.NodeCount.Should().Be(2); // Only input(0) + output(1)
    }

    [Fact]
    public void Build_ZeroConnectionGenome_BuildsSuccessfully()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
        };

        var connections = Array.Empty<ConnectionGene>();
        var genome = new Genome(nodes, connections);

        var network = _builder.Build(genome);

        network.Should().NotBeNull();
        network.ConnectionCount.Should().Be(0);
    }

    [Fact]
    public void Build_ZeroConnectionGenome_OutputIsActivationOfZero()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output), // Default sigmoid
        };

        var connections = Array.Empty<ConnectionGene>();
        var genome = new Genome(nodes, connections);

        var network = _builder.Build(genome);
        Span<double> outputs = stackalloc double[1];
        network.Activate([1.0], outputs);

        // sigmoid(0.0) = 0.5
        outputs[0].Should().BeApproximately(0.5, 1e-10);
    }

    [Fact]
    public void Build_SimpleCycle_ThrowsCycleDetectedException()
    {
        // hidden A → hidden B → hidden A
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden),
            new(3, NodeType.Hidden),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 2, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
            new(3, SourceNodeId: 3, TargetNodeId: 2, Weight: 1.0, IsEnabled: true), // Cycle!
            new(4, SourceNodeId: 3, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);

        var act = () => _builder.Build(genome);

        act.Should().Throw<CycleDetectedException>();
    }

    [Fact]
    public void Build_LongerCycle_ThrowsCycleDetectedException()
    {
        // A → B → C → A
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden),
            new(3, NodeType.Hidden),
            new(4, NodeType.Hidden),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 2, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
            new(3, SourceNodeId: 3, TargetNodeId: 4, Weight: 1.0, IsEnabled: true),
            new(4, SourceNodeId: 4, TargetNodeId: 2, Weight: 1.0, IsEnabled: true), // Cycle!
            new(5, SourceNodeId: 4, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);

        var act = () => _builder.Build(genome);

        act.Should().Throw<CycleDetectedException>();
    }

    [Fact]
    public void Build_CycleAmongHiddenNodesWithInputAndOutputConnected_ThrowsCycleDetectedException()
    {
        // Inputs connected, cycle among hiddens, output reachable
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Hidden),
            new(3, NodeType.Hidden),
            new(4, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
            new(3, SourceNodeId: 2, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
            new(4, SourceNodeId: 3, TargetNodeId: 2, Weight: 1.0, IsEnabled: true), // Cycle!
            new(5, SourceNodeId: 3, TargetNodeId: 4, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);

        var act = () => _builder.Build(genome);

        act.Should().Throw<CycleDetectedException>();
    }

    [Fact]
    public void Build_DisabledConnectionThatWouldCreateCycle_DoesNotThrow()
    {
        // The cycle-creating connection is disabled, so it should be excluded
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden),
            new(3, NodeType.Hidden),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 2, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
            new(3, SourceNodeId: 3, TargetNodeId: 2, Weight: 1.0, IsEnabled: false), // Disabled cycle
            new(4, SourceNodeId: 3, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);

        var act = () => _builder.Build(genome);

        act.Should().NotThrow();
    }

    [Fact]
    public void Build_DisconnectedSubgraph_UnreachableHiddenNodesDoNotAffectOutput()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
            new(2, NodeType.Hidden), // Unreachable — connected to input but no path to output
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 0, TargetNodeId: 2, Weight: 5.0, IsEnabled: true), // Goes to unreachable node
        };

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);

        Span<double> outputs = stackalloc double[1];
        network.Activate([1.0], outputs);

        // Only input(0) → output(1) with weight 1.0: sigmoid(1.0) ≈ 0.7311
        outputs[0].Should().BeApproximately(ActivationFunctions.SigmoidFunction(1.0), 1e-10);
    }

    [Fact]
    public void Build_AllDisabledConnections_OutputIsActivationOfZero()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: false),
        };

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);

        Span<double> outputs = stackalloc double[1];
        network.Activate([1.0], outputs);

        // No enabled connections → output = sigmoid(0.0) = 0.5
        outputs[0].Should().BeApproximately(0.5, 1e-10);
    }

    [Fact]
    public void Build_ValidAcyclicGenomeWithDeepHiddenLayers_BuildsSuccessfully()
    {
        // Input → Hidden1 → Hidden2 → Hidden3 → Hidden4 → Output
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Hidden),
            new(2, NodeType.Hidden),
            new(3, NodeType.Hidden),
            new(4, NodeType.Hidden),
            new(5, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
            new(3, SourceNodeId: 2, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
            new(4, SourceNodeId: 3, TargetNodeId: 4, Weight: 1.0, IsEnabled: true),
            new(5, SourceNodeId: 4, TargetNodeId: 5, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);

        network.Should().NotBeNull();
        network.NodeCount.Should().Be(6);
        network.ConnectionCount.Should().Be(5);

        // Verify it actually runs without error
        Span<double> outputs = stackalloc double[1];
        network.Activate([1.0], outputs);

        // Each layer applies sigmoid: deep chain should produce a valid output
        outputs[0].Should().BeInRange(0.0, 1.0);
    }

    private static Genome CreateSimpleGenome()
    {
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

        return new Genome(nodes, connections);
    }
}
