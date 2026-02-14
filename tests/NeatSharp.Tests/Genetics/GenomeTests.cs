using FluentAssertions;
using NeatSharp.Exceptions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Genetics;

public class GenomeTests
{
    private static NodeGene[] CreateValidNodes() =>
    [
        new(Id: 0, Type: NodeType.Input),
        new(Id: 1, Type: NodeType.Input),
        new(Id: 2, Type: NodeType.Bias),
        new(Id: 3, Type: NodeType.Hidden),
        new(Id: 4, Type: NodeType.Output),
    ];

    private static ConnectionGene[] CreateValidConnections() =>
    [
        new(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true),
        new(InnovationNumber: 2, SourceNodeId: 1, TargetNodeId: 3, Weight: 0.8, IsEnabled: true),
        new(InnovationNumber: 3, SourceNodeId: 3, TargetNodeId: 4, Weight: 1.0, IsEnabled: true),
    ];

    [Fact]
    public void Constructor_WithValidNodesAndConnections_CreatesGenome()
    {
        var nodes = CreateValidNodes();
        var connections = CreateValidConnections();

        var genome = new Genome(nodes, connections);

        genome.Nodes.Should().HaveCount(5);
        genome.Connections.Should().HaveCount(3);
    }

    [Fact]
    public void InputCount_ReturnsNumberOfInputNodes()
    {
        var nodes = CreateValidNodes();
        var connections = CreateValidConnections();

        var genome = new Genome(nodes, connections);

        genome.InputCount.Should().Be(2);
    }

    [Fact]
    public void OutputCount_ReturnsNumberOfOutputNodes()
    {
        var nodes = CreateValidNodes();
        var connections = CreateValidConnections();

        var genome = new Genome(nodes, connections);

        genome.OutputCount.Should().Be(1);
    }

    [Fact]
    public void Constructor_DefensivelyCopiesNodes_MutatingOriginalDoesNotAffectGenome()
    {
        var nodes = new List<NodeGene>
        {
            new(Id: 0, Type: NodeType.Input),
            new(Id: 1, Type: NodeType.Output),
        };
        var connections = Array.Empty<ConnectionGene>();

        var genome = new Genome(nodes, connections);
        nodes.Add(new NodeGene(Id: 99, Type: NodeType.Hidden));

        genome.Nodes.Should().HaveCount(2);
    }

    [Fact]
    public void Constructor_DefensivelyCopiesConnections_MutatingOriginalDoesNotAffectGenome()
    {
        var nodes = new List<NodeGene>
        {
            new(Id: 0, Type: NodeType.Input),
            new(Id: 1, Type: NodeType.Output),
        };
        var connections = new List<ConnectionGene>
        {
            new(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 1, Weight: 0.5, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        connections.Add(new ConnectionGene(InnovationNumber: 2, SourceNodeId: 0, TargetNodeId: 1, Weight: 0.9, IsEnabled: true));

        genome.Connections.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_DuplicateNodeIds_ThrowsInvalidGenomeException()
    {
        var nodes = new NodeGene[]
        {
            new(Id: 0, Type: NodeType.Input),
            new(Id: 0, Type: NodeType.Output), // Duplicate ID
        };
        var connections = Array.Empty<ConnectionGene>();

        var act = () => new Genome(nodes, connections);

        act.Should().Throw<InvalidGenomeException>();
    }

    [Fact]
    public void Constructor_ConnectionReferencingNonexistentSourceNode_ThrowsInvalidGenomeException()
    {
        var nodes = new NodeGene[]
        {
            new(Id: 0, Type: NodeType.Input),
            new(Id: 1, Type: NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(InnovationNumber: 1, SourceNodeId: 99, TargetNodeId: 1, Weight: 0.5, IsEnabled: true),
        };

        var act = () => new Genome(nodes, connections);

        act.Should().Throw<InvalidGenomeException>();
    }

    [Fact]
    public void Constructor_ConnectionReferencingNonexistentTargetNode_ThrowsInvalidGenomeException()
    {
        var nodes = new NodeGene[]
        {
            new(Id: 0, Type: NodeType.Input),
            new(Id: 1, Type: NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 99, Weight: 0.5, IsEnabled: true),
        };

        var act = () => new Genome(nodes, connections);

        act.Should().Throw<InvalidGenomeException>();
    }

    [Fact]
    public void Constructor_MissingInputNodes_ThrowsInvalidGenomeException()
    {
        var nodes = new NodeGene[]
        {
            new(Id: 0, Type: NodeType.Output),
        };
        var connections = Array.Empty<ConnectionGene>();

        var act = () => new Genome(nodes, connections);

        act.Should().Throw<InvalidGenomeException>();
    }

    [Fact]
    public void Constructor_MissingOutputNodes_ThrowsInvalidGenomeException()
    {
        var nodes = new NodeGene[]
        {
            new(Id: 0, Type: NodeType.Input),
        };
        var connections = Array.Empty<ConnectionGene>();

        var act = () => new Genome(nodes, connections);

        act.Should().Throw<InvalidGenomeException>();
    }

    [Fact]
    public void Constructor_NullNodes_ThrowsArgumentNullException()
    {
        var act = () => new Genome(null!, Array.Empty<ConnectionGene>());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_NullConnections_ThrowsArgumentNullException()
    {
        var nodes = new NodeGene[]
        {
            new(Id: 0, Type: NodeType.Input),
            new(Id: 1, Type: NodeType.Output),
        };

        var act = () => new Genome(nodes, null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
