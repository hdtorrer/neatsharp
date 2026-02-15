using FluentAssertions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Genetics;

/// <summary>
/// End-to-end integration tests verifying custom activation functions
/// are correctly registered, resolved during phenotype construction,
/// and applied during network evaluation.
/// </summary>
public class CustomActivationIntegrationTests
{
    [Fact]
    public void CustomActivation_RegisterLeakyReLU_BuildAndActivate_AppliesCustomFunction()
    {
        // Arrange: register custom leaky_relu
        var registry = new ActivationFunctionRegistry();
        registry.Register("leaky_relu", x => x > 0.0 ? x : 0.01 * x);
        var builder = new FeedForwardNetworkBuilder(registry);

        // 1 input → 1 hidden (leaky_relu) → 1 output (default sigmoid)
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Hidden, "leaky_relu"),
            new(2, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = builder.Build(genome);

        // Act: activate with negative input to exercise leaky_relu path
        Span<double> outputs = stackalloc double[1];
        network.Activate([-2.0], outputs);

        // Assert: hidden = leaky_relu(-2.0) = 0.01 * -2.0 = -0.02
        //         output = sigmoid(-0.02)
        double hiddenValue = 0.01 * -2.0; // -0.02
        double expected = ActivationFunctions.SigmoidFunction(hiddenValue);
        outputs[0].Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void CustomActivation_RegisterLeakyReLU_PositiveInput_AppliesCustomFunction()
    {
        // Verify the positive branch of leaky_relu (identity for x > 0)
        var registry = new ActivationFunctionRegistry();
        registry.Register("leaky_relu", x => x > 0.0 ? x : 0.01 * x);
        var builder = new FeedForwardNetworkBuilder(registry);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Hidden, "leaky_relu"),
            new(2, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = builder.Build(genome);

        Span<double> outputs = stackalloc double[1];
        network.Activate([3.0], outputs);

        // hidden = leaky_relu(3.0) = 3.0 (positive branch)
        // output = sigmoid(3.0)
        double expected = ActivationFunctions.SigmoidFunction(3.0);
        outputs[0].Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void CustomActivation_NegativeInput_ProducesDifferentOutputThanSigmoid()
    {
        // Verify custom function produces a result different from default sigmoid
        var registry = new ActivationFunctionRegistry();
        registry.Register("leaky_relu", x => x > 0.0 ? x : 0.01 * x);
        var builder = new FeedForwardNetworkBuilder(registry);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Hidden, "leaky_relu"),
            new(2, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = builder.Build(genome);

        Span<double> outputsCustom = stackalloc double[1];
        network.Activate([-2.0], outputsCustom);

        // Now build the same topology with default sigmoid on hidden node
        var defaultRegistry = new ActivationFunctionRegistry();
        var defaultBuilder = new FeedForwardNetworkBuilder(defaultRegistry);

        var sigmoidNodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Hidden), // default sigmoid
            new(2, NodeType.Output),
        };

        var sigmoidGenome = new Genome(sigmoidNodes, connections);
        var sigmoidNetwork = defaultBuilder.Build(sigmoidGenome);

        Span<double> outputsSigmoid = stackalloc double[1];
        sigmoidNetwork.Activate([-2.0], outputsSigmoid);

        // The outputs should differ since leaky_relu(-2.0) != sigmoid(-2.0)
        outputsCustom[0].Should().NotBeApproximately(outputsSigmoid[0], 1e-5);
    }

    [Fact]
    public void DefaultActivation_NoActivationFunctionSpecified_AppliesSigmoid()
    {
        var registry = new ActivationFunctionRegistry();
        var builder = new FeedForwardNetworkBuilder(registry);

        // Hidden node with no explicit activation function → default "sigmoid"
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Hidden), // defaults to "sigmoid"
            new(2, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = builder.Build(genome);

        Span<double> outputs = stackalloc double[1];
        network.Activate([2.0], outputs);

        // hidden = sigmoid(2.0), output = sigmoid(sigmoid(2.0))
        double hiddenValue = ActivationFunctions.SigmoidFunction(2.0);
        double expected = ActivationFunctions.SigmoidFunction(hiddenValue);
        outputs[0].Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void UnregisteredActivation_Build_ThrowsArgumentException()
    {
        var registry = new ActivationFunctionRegistry();
        var builder = new FeedForwardNetworkBuilder(registry);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Hidden, "nonexistent_function"),
            new(2, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);

        var act = () => builder.Build(genome);

        act.Should().Throw<ArgumentException>()
            .And.Message.Should().Contain("nonexistent_function");
    }

    [Fact]
    public void CustomActivation_MultipleHiddenNodes_EachAppliesOwnFunction()
    {
        // Two hidden nodes with different custom activations
        var registry = new ActivationFunctionRegistry();
        registry.Register("double", x => x * 2.0);
        registry.Register("negate", x => -x);
        var builder = new FeedForwardNetworkBuilder(registry);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Hidden, "double"),
            new(2, NodeType.Hidden, "negate"),
            new(3, NodeType.Output, ActivationFunctions.Identity),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 0, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
            new(3, SourceNodeId: 1, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
            new(4, SourceNodeId: 2, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = builder.Build(genome);

        Span<double> outputs = stackalloc double[1];
        network.Activate([5.0], outputs);

        // hidden1 = double(5.0) = 10.0
        // hidden2 = negate(5.0) = -5.0
        // output = identity(10.0 + (-5.0)) = 5.0
        outputs[0].Should().BeApproximately(5.0, 1e-10);
    }
}
