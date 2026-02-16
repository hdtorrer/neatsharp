using FluentAssertions;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Evaluation;
using NeatSharp.Gpu.Exceptions;
using Xunit;

namespace NeatSharp.Gpu.Tests.Evaluation;

public class GpuNetworkBuilderTests
{
    private static readonly ActivationFunctionRegistry Registry = new();
    private static readonly FeedForwardNetworkBuilder CpuBuilder = new(Registry);
    private static readonly GpuNetworkBuilder GpuBuilder = new(CpuBuilder);

    // --- Decorator pattern ---

    [Fact]
    public void Build_ReturnsGpuFeedForwardNetwork()
    {
        var genome = CreateSimpleGenome();

        var result = GpuBuilder.Build(genome);

        result.Should().BeOfType<GpuFeedForwardNetwork>();
    }

    [Fact]
    public void Build_NodeCountMatchesGpuTopology()
    {
        var genome = CreateSimpleGenome();
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.NodeCount.Should().Be(gpuNetwork.NodeActivationTypes.Length);
    }

    [Fact]
    public void Build_DelegatesToInnerBuilder_ConnectionCountMatches()
    {
        var genome = CreateSimpleGenome();
        var cpuNetwork = CpuBuilder.Build(genome);
        var gpuNetwork = GpuBuilder.Build(genome);

        gpuNetwork.ConnectionCount.Should().Be(cpuNetwork.ConnectionCount);
    }

    [Fact]
    public void Build_WithNullGenome_ThrowsArgumentNullException()
    {
        var act = () => GpuBuilder.Build(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullInnerBuilder_ThrowsArgumentNullException()
    {
        var act = () => new GpuNetworkBuilder(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // --- Activation function mapping ---

    [Fact]
    public void Build_SigmoidActivation_MapsToGpuSigmoid()
    {
        var genome = CreateGenomeWithActivation(ActivationFunctions.Sigmoid);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        // Output node is in EvalOrder — check its activation type
        gpuNetwork.EvalOrder.Should().ContainSingle()
            .Which.ActivationType.Should().Be((int)GpuActivationFunction.Sigmoid);
    }

    [Fact]
    public void Build_TanhActivation_MapsToGpuTanh()
    {
        var genome = CreateGenomeWithActivation(ActivationFunctions.Tanh);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.EvalOrder.Should().ContainSingle()
            .Which.ActivationType.Should().Be((int)GpuActivationFunction.Tanh);
    }

    [Fact]
    public void Build_ReLUActivation_MapsToGpuReLU()
    {
        var genome = CreateGenomeWithActivation(ActivationFunctions.ReLU);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.EvalOrder.Should().ContainSingle()
            .Which.ActivationType.Should().Be((int)GpuActivationFunction.ReLU);
    }

    [Fact]
    public void Build_StepActivation_MapsToGpuStep()
    {
        var genome = CreateGenomeWithActivation(ActivationFunctions.Step);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.EvalOrder.Should().ContainSingle()
            .Which.ActivationType.Should().Be((int)GpuActivationFunction.Step);
    }

    [Fact]
    public void Build_IdentityActivation_MapsToGpuIdentity()
    {
        var genome = CreateGenomeWithActivation(ActivationFunctions.Identity);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.EvalOrder.Should().ContainSingle()
            .Which.ActivationType.Should().Be((int)GpuActivationFunction.Identity);
    }

    [Theory]
    [InlineData(ActivationFunctions.Sigmoid, (int)GpuActivationFunction.Sigmoid)]
    [InlineData(ActivationFunctions.Tanh, (int)GpuActivationFunction.Tanh)]
    [InlineData(ActivationFunctions.ReLU, (int)GpuActivationFunction.ReLU)]
    [InlineData(ActivationFunctions.Step, (int)GpuActivationFunction.Step)]
    [InlineData(ActivationFunctions.Identity, (int)GpuActivationFunction.Identity)]
    public void Build_AllBuiltInActivations_MapCorrectly(string activationName, int expectedGpuType)
    {
        var genome = CreateGenomeWithActivation(activationName);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.EvalOrder.Should().ContainSingle()
            .Which.ActivationType.Should().Be(expectedGpuType);
    }

    [Fact]
    public void Build_MixedActivationFunctions_EachNodeMappedCorrectly()
    {
        // Create a genome with 2 hidden nodes using different activation functions
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output, ActivationFunctions.Sigmoid),
            new(2, NodeType.Hidden, ActivationFunctions.Tanh),
            new(3, NodeType.Hidden, ActivationFunctions.ReLU),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 2, 1.0, true),
            new(2, 0, 3, 1.0, true),
            new(3, 2, 1, 1.0, true),
            new(4, 3, 1, 1.0, true),
        };
        var genome = new Genome(nodes, connections);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        // Should have 3 eval nodes (2 hidden + 1 output)
        gpuNetwork.EvalOrder.Should().HaveCount(3);

        var activationTypes = gpuNetwork.EvalOrder
            .Select(e => e.ActivationType)
            .ToList();

        activationTypes.Should().Contain((int)GpuActivationFunction.Tanh);
        activationTypes.Should().Contain((int)GpuActivationFunction.ReLU);
        activationTypes.Should().Contain((int)GpuActivationFunction.Sigmoid);
    }

    // --- Unknown activation function ---

    [Fact]
    public void Build_UnknownActivationFunction_ThrowsGpuEvaluationException()
    {
        // Register a custom activation function in the CPU registry so inner builder succeeds
        var customRegistry = new ActivationFunctionRegistry();
        customRegistry.Register("custom", x => x * 2.0);
        var innerBuilder = new FeedForwardNetworkBuilder(customRegistry);
        var gpuBuilder = new GpuNetworkBuilder(innerBuilder);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output, "custom"),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 1, 1.0, true),
        };
        var genome = new Genome(nodes, connections);

        var act = () => gpuBuilder.Build(genome);

        act.Should().Throw<GpuEvaluationException>()
            .WithMessage("*custom*");
    }

    [Fact]
    public void Build_UnknownActivation_ExceptionContainsSupportedList()
    {
        var customRegistry = new ActivationFunctionRegistry();
        customRegistry.Register("my_func", x => x);
        var innerBuilder = new FeedForwardNetworkBuilder(customRegistry);
        var gpuBuilder = new GpuNetworkBuilder(innerBuilder);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output, "my_func"),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 1, 1.0, true),
        };
        var genome = new Genome(nodes, connections);

        var act = () => gpuBuilder.Build(genome);

        act.Should().Throw<GpuEvaluationException>()
            .WithMessage("*Supported*");
    }

    // --- Heterogeneous topologies ---

    [Fact]
    public void Build_MinimalGenome_ProducesCorrectSizedArrays()
    {
        // 1 input -> 1 output
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 1, 1.0, true),
        };
        var genome = new Genome(nodes, connections);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.InputIndices.Should().HaveCount(1);
        gpuNetwork.OutputIndices.Should().HaveCount(1);
        gpuNetwork.BiasIndices.Should().BeEmpty();
        gpuNetwork.EvalOrder.Should().HaveCount(1);
        gpuNetwork.NodeActivationTypes.Should().HaveCount(2);
    }

    [Fact]
    public void Build_ComplexGenome_ProducesCorrectSizedArrays()
    {
        // 3 inputs + bias + 2 hidden + 2 outputs
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Input),
            new(3, NodeType.Bias),
            new(4, NodeType.Hidden),
            new(5, NodeType.Hidden),
            new(6, NodeType.Output),
            new(7, NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 4, 1.0, true),
            new(2, 1, 4, 1.0, true),
            new(3, 2, 5, 1.0, true),
            new(4, 3, 4, -1.0, true),
            new(5, 3, 5, -1.0, true),
            new(6, 4, 6, 1.0, true),
            new(7, 5, 7, 1.0, true),
        };
        var genome = new Genome(nodes, connections);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.InputIndices.Should().HaveCount(3);
        gpuNetwork.OutputIndices.Should().HaveCount(2);
        gpuNetwork.BiasIndices.Should().HaveCount(1);
        // hidden(4) + hidden(5) + output(6) + output(7) = 4 eval nodes
        gpuNetwork.EvalOrder.Should().HaveCount(4);
        gpuNetwork.NodeActivationTypes.Should().HaveCount(8);
    }

    [Fact]
    public void Build_MultipleGenomes_EachProducesIndependentArrays()
    {
        var genome1 = CreateSimpleGenome();
        var genome2 = CreateGenomeWithActivation(ActivationFunctions.Tanh);

        var gpuNetwork1 = (GpuFeedForwardNetwork)GpuBuilder.Build(genome1);
        var gpuNetwork2 = (GpuFeedForwardNetwork)GpuBuilder.Build(genome2);

        // They should have different activation types for the output node
        gpuNetwork1.EvalOrder[0].ActivationType.Should().Be((int)GpuActivationFunction.Sigmoid);
        gpuNetwork2.EvalOrder[0].ActivationType.Should().Be((int)GpuActivationFunction.Tanh);
    }

    [Fact]
    public void Build_EvalOrderWeights_AreFp32CastsOfOriginal()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 1, 3.14159265358979, true),
        };
        var genome = new Genome(nodes, connections);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.EvalOrder[0].IncomingWeights[0]
            .Should().Be((float)3.14159265358979);
    }

    // --- Helpers ---

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
            new(1, 0, 3, 1.0, true),
            new(2, 1, 3, 1.0, true),
            new(3, 2, 3, -0.5, true),
        };
        return new Genome(nodes, connections);
    }

    private static Genome CreateGenomeWithActivation(string activationFunction)
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output, activationFunction),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 1, 1.0, true),
        };
        return new Genome(nodes, connections);
    }
}
