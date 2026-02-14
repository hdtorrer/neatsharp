using System.Text.Json;
using FluentAssertions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Genetics;

public class SerializationReadinessTests
{
    [Fact]
    public void NodeGene_RoundTrips_ThroughSystemTextJson()
    {
        var original = new NodeGene(Id: 5, Type: NodeType.Hidden, ActivationFunction: "tanh");

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<NodeGene>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be(original.Id);
        deserialized.Type.Should().Be(original.Type);
        deserialized.ActivationFunction.Should().Be(original.ActivationFunction);
    }

    [Fact]
    public void NodeGene_RoundTrips_WithDefaultActivationFunction()
    {
        var original = new NodeGene(Id: 0, Type: NodeType.Input);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<NodeGene>(json);

        deserialized.Should().NotBeNull();
        deserialized!.ActivationFunction.Should().Be("sigmoid");
    }

    [Fact]
    public void ConnectionGene_RoundTrips_ThroughSystemTextJson()
    {
        var original = new ConnectionGene(
            InnovationNumber: 42,
            SourceNodeId: 1,
            TargetNodeId: 5,
            Weight: -0.75,
            IsEnabled: true);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ConnectionGene>(json);

        deserialized.Should().NotBeNull();
        deserialized!.InnovationNumber.Should().Be(original.InnovationNumber);
        deserialized.SourceNodeId.Should().Be(original.SourceNodeId);
        deserialized.TargetNodeId.Should().Be(original.TargetNodeId);
        deserialized.Weight.Should().Be(original.Weight);
        deserialized.IsEnabled.Should().Be(original.IsEnabled);
    }

    [Fact]
    public void ConnectionGene_RoundTrips_DisabledConnection()
    {
        var original = new ConnectionGene(
            InnovationNumber: 7,
            SourceNodeId: 0,
            TargetNodeId: 3,
            Weight: 0.5,
            IsEnabled: false);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ConnectionGene>(json);

        deserialized.Should().NotBeNull();
        deserialized!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Genome_RoundTrips_ThroughSystemTextJson()
    {
        var nodes = new NodeGene[]
        {
            new(Id: 0, Type: NodeType.Input),
            new(Id: 1, Type: NodeType.Input),
            new(Id: 2, Type: NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 2, Weight: 0.5, IsEnabled: true),
            new(InnovationNumber: 2, SourceNodeId: 1, TargetNodeId: 2, Weight: -0.3, IsEnabled: true),
        };
        var original = new Genome(nodes, connections);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Genome>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Nodes.Should().HaveCount(3);
        deserialized.Connections.Should().HaveCount(2);
        deserialized.InputCount.Should().Be(2);
        deserialized.OutputCount.Should().Be(1);

        deserialized.Nodes[0].Id.Should().Be(0);
        deserialized.Nodes[0].Type.Should().Be(NodeType.Input);
        deserialized.Nodes[1].Id.Should().Be(1);
        deserialized.Nodes[2].Id.Should().Be(2);
        deserialized.Nodes[2].Type.Should().Be(NodeType.Output);

        deserialized.Connections[0].InnovationNumber.Should().Be(1);
        deserialized.Connections[0].Weight.Should().Be(0.5);
        deserialized.Connections[1].InnovationNumber.Should().Be(2);
        deserialized.Connections[1].Weight.Should().Be(-0.3);
    }

    [Fact]
    public void Genome_RoundTrips_WithMixedNodeTypesAndDisabledConnections()
    {
        var nodes = new NodeGene[]
        {
            new(Id: 0, Type: NodeType.Input),
            new(Id: 1, Type: NodeType.Input),
            new(Id: 2, Type: NodeType.Bias),
            new(Id: 3, Type: NodeType.Hidden, ActivationFunction: "tanh"),
            new(Id: 4, Type: NodeType.Hidden, ActivationFunction: "relu"),
            new(Id: 5, Type: NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true),
            new(InnovationNumber: 2, SourceNodeId: 1, TargetNodeId: 3, Weight: 0.8, IsEnabled: false),
            new(InnovationNumber: 3, SourceNodeId: 2, TargetNodeId: 4, Weight: -0.3, IsEnabled: true),
            new(InnovationNumber: 4, SourceNodeId: 3, TargetNodeId: 5, Weight: 1.0, IsEnabled: true),
            new(InnovationNumber: 5, SourceNodeId: 4, TargetNodeId: 5, Weight: -1.0, IsEnabled: false),
        };
        var original = new Genome(nodes, connections);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<Genome>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Nodes.Should().HaveCount(6);
        deserialized.Connections.Should().HaveCount(5);
        deserialized.InputCount.Should().Be(2);
        deserialized.OutputCount.Should().Be(1);

        // Verify hidden node activation functions survive round-trip
        deserialized.Nodes[3].ActivationFunction.Should().Be("tanh");
        deserialized.Nodes[4].ActivationFunction.Should().Be("relu");

        // Verify disabled connections survive round-trip
        deserialized.Connections[1].IsEnabled.Should().BeFalse();
        deserialized.Connections[4].IsEnabled.Should().BeFalse();

        // Verify enabled connections survive round-trip
        deserialized.Connections[0].IsEnabled.Should().BeTrue();
        deserialized.Connections[2].IsEnabled.Should().BeTrue();
        deserialized.Connections[3].IsEnabled.Should().BeTrue();
    }
}
