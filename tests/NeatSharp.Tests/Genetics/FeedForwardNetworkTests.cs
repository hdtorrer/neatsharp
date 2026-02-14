using FluentAssertions;
using NeatSharp.Exceptions;
using NeatSharp.Genetics;
using Xunit;

namespace NeatSharp.Tests.Genetics;

public class FeedForwardNetworkTests
{
    private readonly INetworkBuilder _builder;

    public FeedForwardNetworkTests()
    {
        var registry = new ActivationFunctionRegistry();
        _builder = new FeedForwardNetworkBuilder(registry);
    }

    [Fact]
    public void Activate_Simple2Input1Output_ProducesExpectedSigmoidOutput()
    {
        // 2 inputs + 1 bias + 1 output
        // Connections: input0→out (0.5), input1→out (0.8), bias→out (-0.3)
        // sigmoid(1.0*0.5 + 0.5*0.8 + 1.0*(-0.3)) = sigmoid(0.6)
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
        var network = _builder.Build(genome);
        Span<double> outputs = stackalloc double[1];
        network.Activate([1.0, 0.5], outputs);

        double expected = 1.0 / (1.0 + Math.Exp(-0.6)); // sigmoid(0.6)
        outputs[0].Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void Activate_HiddenLayerWithDefaultSigmoid_ProducesCorrectOutput()
    {
        // input0 → hidden(3) → output(4)
        // input0=1.0, weight(0→3)=2.0, weight(3→4)=1.0
        // hidden = sigmoid(1.0 * 2.0) = sigmoid(2.0)
        // output = sigmoid(sigmoid(2.0) * 1.0)
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(3, NodeType.Hidden),
            new(4, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 3, Weight: 2.0, IsEnabled: true),
            new(2, SourceNodeId: 3, TargetNodeId: 4, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);
        Span<double> outputs = stackalloc double[1];
        network.Activate([1.0], outputs);

        double hiddenValue = 1.0 / (1.0 + Math.Exp(-2.0)); // sigmoid(2.0)
        double expected = 1.0 / (1.0 + Math.Exp(-hiddenValue)); // sigmoid(sigmoid(2.0))
        outputs[0].Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void Activate_DisabledConnection_DoesNotContributeToOutput()
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
        Span<double> outputs = stackalloc double[1];
        network.Activate([1.0, 0.5], outputs);

        // Only connections 1 and 3: sigmoid(1.0*0.5 + 1.0*(-0.3)) = sigmoid(0.2)
        double expected = 1.0 / (1.0 + Math.Exp(-0.2));
        outputs[0].Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void Activate_BiasNode_ContributesConstantOneTimesWeight()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Bias),
            new(2, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 2, Weight: 0.0, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 2, Weight: 0.5, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);
        Span<double> outputs = stackalloc double[1];
        network.Activate([0.0], outputs);

        // input contributes 0.0*0.0=0.0, bias contributes 1.0*0.5=0.5
        // sigmoid(0.5)
        double expected = 1.0 / (1.0 + Math.Exp(-0.5));
        outputs[0].Should().BeApproximately(expected, 1e-10);
    }

    [Fact]
    public void Activate_InputSpanTooShort_ThrowsInputDimensionMismatchException()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 2, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);
        var outputs = new double[1];

        var ex = Assert.Throws<InputDimensionMismatchException>(
            () => network.Activate([1.0], outputs)); // Expected 2, got 1

        ex.Expected.Should().Be(2);
        ex.Actual.Should().Be(1);
    }

    [Fact]
    public void Activate_InputSpanTooLong_ThrowsInputDimensionMismatchException()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);
        var outputs = new double[1];

        var ex = Assert.Throws<InputDimensionMismatchException>(
            () => network.Activate([1.0, 2.0], outputs)); // Expected 1, got 2

        ex.Expected.Should().Be(1);
        ex.Actual.Should().Be(2);
    }

    [Fact]
    public void Activate_OutputSpanWrongSize_ThrowsArgumentException()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 1, Weight: 1.0, IsEnabled: true),
        };

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);
        var outputs = new double[2]; // Expected 1 output

        Assert.Throws<ArgumentException>(
            () => network.Activate([1.0], outputs));
    }

    [Fact]
    public void Activate_1000TimesWithIdenticalInputs_ProducesBitIdenticalOutputs()
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

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);

        var outputs = new double[1];
        network.Activate([1.0, 0.5], outputs);
        double firstResult = outputs[0];

        for (int i = 1; i < 1000; i++)
        {
            network.Activate([1.0, 0.5], outputs);
            outputs[0].Should().Be(firstResult,
                because: $"activation {i} should be bit-identical to the first");
        }
    }

    [Fact]
    public void Activate_DifferentInputSets_ProducesCorrectHandCalculatedResults()
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

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);
        var outputs = new double[1];

        // [0.0, 0.0]: sigmoid(0.0*0.5 + 0.0*0.8 + 1.0*(-0.3)) = sigmoid(-0.3)
        network.Activate([0.0, 0.0], outputs);
        double expected00 = 1.0 / (1.0 + Math.Exp(0.3));
        outputs[0].Should().BeApproximately(expected00, 1e-10);

        // [1.0, 1.0]: sigmoid(1.0*0.5 + 1.0*0.8 + 1.0*(-0.3)) = sigmoid(1.0)
        network.Activate([1.0, 1.0], outputs);
        double expected11 = 1.0 / (1.0 + Math.Exp(-1.0));
        outputs[0].Should().BeApproximately(expected11, 1e-10);

        // [0.5, 0.5]: sigmoid(0.5*0.5 + 0.5*0.8 + 1.0*(-0.3)) = sigmoid(0.35)
        network.Activate([0.5, 0.5], outputs);
        double expected55 = 1.0 / (1.0 + Math.Exp(-0.35));
        outputs[0].Should().BeApproximately(expected55, 1e-10);
    }

    [Fact]
    public void Activate_InterleavedDifferentInputs_NoCrossContaminationFromBufferReuse()
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

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);
        var outputs = new double[1];

        double expectedA = 1.0 / (1.0 + Math.Exp(-0.6));   // sigmoid(1.0*0.5 + 0.5*0.8 + 1.0*(-0.3))
        double expectedB = 1.0 / (1.0 + Math.Exp(0.3));     // sigmoid(0.0*0.5 + 0.0*0.8 + 1.0*(-0.3))

        // Interleave: A, B, A, B, A, B...
        for (int i = 0; i < 100; i++)
        {
            network.Activate([1.0, 0.5], outputs);
            outputs[0].Should().BeApproximately(expectedA, 1e-10,
                because: $"iteration {i} with inputs [1.0, 0.5] should not be contaminated by prior activation");

            network.Activate([0.0, 0.0], outputs);
            outputs[0].Should().BeApproximately(expectedB, 1e-10,
                because: $"iteration {i} with inputs [0.0, 0.0] should not be contaminated by prior activation");
        }
    }

    [Fact]
    public void NodeCountAndConnectionCount_RemainConsistentAcrossActivations()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Bias),
            new(3, NodeType.Hidden),
            new(4, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, SourceNodeId: 0, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
            new(2, SourceNodeId: 1, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
            new(3, SourceNodeId: 2, TargetNodeId: 3, Weight: -0.5, IsEnabled: true),
            new(4, SourceNodeId: 3, TargetNodeId: 4, Weight: 1.0, IsEnabled: true),
            new(5, SourceNodeId: 0, TargetNodeId: 4, Weight: 0.3, IsEnabled: false), // Disabled
        };

        var genome = new Genome(nodes, connections);
        var network = _builder.Build(genome);

        int initialNodeCount = network.NodeCount;
        int initialConnectionCount = network.ConnectionCount;
        var outputs = new double[1];

        for (int i = 0; i < 100; i++)
        {
            network.Activate([1.0, 0.5], outputs);

            network.NodeCount.Should().Be(initialNodeCount,
                because: $"NodeCount should not change after activation {i}");
            network.ConnectionCount.Should().Be(initialConnectionCount,
                because: $"ConnectionCount should not change after activation {i}");
        }
    }
}
