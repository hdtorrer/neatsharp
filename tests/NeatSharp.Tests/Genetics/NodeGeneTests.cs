using FluentAssertions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Genetics;

public class NodeGeneTests
{
    [Fact]
    public void Constructor_WithAllParameters_SetsProperties()
    {
        var gene = new NodeGene(Id: 5, Type: NodeType.Hidden, ActivationFunction: "tanh");

        gene.Id.Should().Be(5);
        gene.Type.Should().Be(NodeType.Hidden);
        gene.ActivationFunction.Should().Be("tanh");
    }

    [Fact]
    public void Constructor_WithDefaultActivationFunction_UsesSigmoid()
    {
        var gene = new NodeGene(Id: 0, Type: NodeType.Input);

        gene.ActivationFunction.Should().Be("sigmoid");
    }

    [Theory]
    [InlineData(NodeType.Input)]
    [InlineData(NodeType.Hidden)]
    [InlineData(NodeType.Output)]
    [InlineData(NodeType.Bias)]
    public void Constructor_WithEachNodeType_SetsType(NodeType type)
    {
        var gene = new NodeGene(Id: 1, Type: type);

        gene.Type.Should().Be(type);
    }

    [Fact]
    public void Equality_IdenticalRecords_AreEqual()
    {
        var gene1 = new NodeGene(Id: 3, Type: NodeType.Output, ActivationFunction: "relu");
        var gene2 = new NodeGene(Id: 3, Type: NodeType.Output, ActivationFunction: "relu");

        gene1.Should().Be(gene2);
        (gene1 == gene2).Should().BeTrue();
        gene1.GetHashCode().Should().Be(gene2.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentId_AreNotEqual()
    {
        var gene1 = new NodeGene(Id: 1, Type: NodeType.Input);
        var gene2 = new NodeGene(Id: 2, Type: NodeType.Input);

        gene1.Should().NotBe(gene2);
        (gene1 != gene2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentType_AreNotEqual()
    {
        var gene1 = new NodeGene(Id: 1, Type: NodeType.Input);
        var gene2 = new NodeGene(Id: 1, Type: NodeType.Output);

        gene1.Should().NotBe(gene2);
    }

    [Fact]
    public void Equality_DifferentActivationFunction_AreNotEqual()
    {
        var gene1 = new NodeGene(Id: 1, Type: NodeType.Hidden, ActivationFunction: "sigmoid");
        var gene2 = new NodeGene(Id: 1, Type: NodeType.Hidden, ActivationFunction: "tanh");

        gene1.Should().NotBe(gene2);
    }
}
