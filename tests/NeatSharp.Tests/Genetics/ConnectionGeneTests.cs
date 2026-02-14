using FluentAssertions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Genetics;

public class ConnectionGeneTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        var gene = new ConnectionGene(
            InnovationNumber: 42,
            SourceNodeId: 0,
            TargetNodeId: 3,
            Weight: 0.75,
            IsEnabled: true);

        gene.InnovationNumber.Should().Be(42);
        gene.SourceNodeId.Should().Be(0);
        gene.TargetNodeId.Should().Be(3);
        gene.Weight.Should().Be(0.75);
        gene.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithDisabledConnection_SetsIsEnabledFalse()
    {
        var gene = new ConnectionGene(
            InnovationNumber: 1,
            SourceNodeId: 0,
            TargetNodeId: 1,
            Weight: -0.5,
            IsEnabled: false);

        gene.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Equality_IdenticalRecords_AreEqual()
    {
        var gene1 = new ConnectionGene(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true);
        var gene2 = new ConnectionGene(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true);

        gene1.Should().Be(gene2);
        (gene1 == gene2).Should().BeTrue();
        gene1.GetHashCode().Should().Be(gene2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentInnovationNumber_AreNotEqual()
    {
        var gene1 = new ConnectionGene(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true);
        var gene2 = new ConnectionGene(InnovationNumber: 2, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true);

        gene1.Should().NotBe(gene2);
        (gene1 != gene2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentSourceNodeId_AreNotEqual()
    {
        var gene1 = new ConnectionGene(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true);
        var gene2 = new ConnectionGene(InnovationNumber: 1, SourceNodeId: 1, TargetNodeId: 3, Weight: 0.5, IsEnabled: true);

        gene1.Should().NotBe(gene2);
    }

    [Fact]
    public void Equality_DifferentTargetNodeId_AreNotEqual()
    {
        var gene1 = new ConnectionGene(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true);
        var gene2 = new ConnectionGene(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 4, Weight: 0.5, IsEnabled: true);

        gene1.Should().NotBe(gene2);
    }

    [Fact]
    public void Equality_DifferentWeight_AreNotEqual()
    {
        var gene1 = new ConnectionGene(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true);
        var gene2 = new ConnectionGene(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.9, IsEnabled: true);

        gene1.Should().NotBe(gene2);
    }

    [Fact]
    public void Equality_DifferentIsEnabled_AreNotEqual()
    {
        var gene1 = new ConnectionGene(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true);
        var gene2 = new ConnectionGene(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: false);

        gene1.Should().NotBe(gene2);
    }
}
