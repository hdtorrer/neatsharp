using FluentAssertions;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Evaluation;
using Xunit;

namespace NeatSharp.Gpu.Tests.Evaluation;

public class GpuFeedForwardNetworkTests
{
    private static readonly ActivationFunctionRegistry Registry = new();
    private static readonly FeedForwardNetworkBuilder CpuBuilder = new(Registry);
    private static readonly GpuNetworkBuilder GpuBuilder = new(CpuBuilder);

    // --- Construction ---

    [Fact]
    public void Constructor_WithValidArguments_PopulatesAllProperties()
    {
        var genome = CreateSimpleGenome();
        var cpuNetwork = CpuBuilder.Build(genome);

        var gpuNetwork = new GpuFeedForwardNetwork(
            cpuNetwork,
            inputIndices: [0, 1],
            biasIndices: [2],
            outputIndices: [3],
            nodeActivationTypes: [0, 0, 0, 0],
            evalOrder: [new GpuEvalNode(3, [0, 1, 2], [1.0f, 2.0f, -1.0f], 0)]);

        gpuNetwork.InputIndices.Should().Equal(0, 1);
        gpuNetwork.BiasIndices.Should().Equal(2);
        gpuNetwork.OutputIndices.Should().Equal(3);
        gpuNetwork.NodeActivationTypes.Should().HaveCount(4);
        gpuNetwork.EvalOrder.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_WithNullCpuNetwork_ThrowsArgumentNullException()
    {
        var act = () => new GpuFeedForwardNetwork(
            null!,
            inputIndices: [0],
            biasIndices: [],
            outputIndices: [1],
            nodeActivationTypes: [0, 0],
            evalOrder: []);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("cpuNetwork");
    }

    [Fact]
    public void Constructor_WithNullInputIndices_ThrowsArgumentNullException()
    {
        var genome = CreateSimpleGenome();
        var cpuNetwork = CpuBuilder.Build(genome);

        var act = () => new GpuFeedForwardNetwork(
            cpuNetwork,
            inputIndices: null!,
            biasIndices: [],
            outputIndices: [1],
            nodeActivationTypes: [0, 0],
            evalOrder: []);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("inputIndices");
    }

    // --- IGenome delegation to CPU fallback ---

    [Fact]
    public void NodeCount_DelegatesToCpuNetwork()
    {
        var genome = CreateSimpleGenome();
        var cpuNetwork = CpuBuilder.Build(genome);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.NodeCount.Should().Be(cpuNetwork.NodeCount);
    }

    [Fact]
    public void ConnectionCount_DelegatesToCpuNetwork()
    {
        var genome = CreateSimpleGenome();
        var cpuNetwork = CpuBuilder.Build(genome);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.ConnectionCount.Should().Be(cpuNetwork.ConnectionCount);
    }

    [Fact]
    public void Activate_DelegatesToCpuNetwork_ProducesSameOutputs()
    {
        var genome = CreateSimpleGenome();
        var cpuNetwork = CpuBuilder.Build(genome);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        double[] inputs = [0.5, 0.8];
        double[] cpuOutputs = new double[1];
        double[] gpuOutputs = new double[1];

        cpuNetwork.Activate(inputs, cpuOutputs);
        gpuNetwork.Activate(inputs, gpuOutputs);

        gpuOutputs[0].Should().Be(cpuOutputs[0],
            because: "GpuFeedForwardNetwork.Activate delegates to the CPU network");
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 0.0)]
    [InlineData(0.0, 1.0)]
    [InlineData(1.0, 1.0)]
    public void Activate_WithVariousInputs_MatchesCpuNetworkExactly(double in0, double in1)
    {
        var genome = CreateSimpleGenome();
        var cpuNetwork = CpuBuilder.Build(genome);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        double[] inputs = [in0, in1];
        double[] cpuOutputs = new double[1];
        double[] gpuOutputs = new double[1];

        cpuNetwork.Activate(inputs, cpuOutputs);
        gpuNetwork.Activate(inputs, gpuOutputs);

        gpuOutputs[0].Should().Be(cpuOutputs[0]);
    }

    // --- Flat topology extraction ---

    [Fact]
    public void Build_SimpleGenome_ExtractsCorrectInputIndices()
    {
        var genome = CreateSimpleGenome();
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        // 2 inputs in declaration order
        gpuNetwork.InputIndices.Should().HaveCount(2);
    }

    [Fact]
    public void Build_SimpleGenome_ExtractsCorrectOutputIndices()
    {
        var genome = CreateSimpleGenome();
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        // 1 output
        gpuNetwork.OutputIndices.Should().HaveCount(1);
    }

    [Fact]
    public void Build_SimpleGenome_ExtractsCorrectBiasIndices()
    {
        var genome = CreateSimpleGenome();
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        // 1 bias node
        gpuNetwork.BiasIndices.Should().HaveCount(1);
    }

    [Fact]
    public void Build_GenomeWithHiddenNode_ExtractsCorrectEvalOrderLength()
    {
        var genome = CreateGenomeWithHiddenNode();
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        // EvalOrder contains hidden + output nodes only
        // Hidden node + Output node = 2
        gpuNetwork.EvalOrder.Should().HaveCount(2);
    }

    [Fact]
    public void Build_GenomeWithHiddenNode_EvalOrderContainsCorrectActivationTypes()
    {
        var genome = CreateGenomeWithHiddenNode();
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        foreach (var evalNode in gpuNetwork.EvalOrder)
        {
            evalNode.ActivationType.Should().Be((int)GpuActivationFunction.Sigmoid,
                because: "all nodes default to sigmoid activation");
        }
    }

    [Fact]
    public void Build_GenomeWithHiddenNode_EvalOrderHasCorrectIncomingConnections()
    {
        // 2 inputs -> hidden -> output, plus bias -> hidden
        var genome = CreateGenomeWithHiddenNode();
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        // Hidden node should have 3 incoming connections (2 inputs + 1 bias)
        var hiddenNode = gpuNetwork.EvalOrder[0];
        hiddenNode.IncomingSources.Should().HaveCount(3);
        hiddenNode.IncomingWeights.Should().HaveCount(3);

        // Output node should have 1 incoming connection (from hidden)
        var outputNode = gpuNetwork.EvalOrder[1];
        outputNode.IncomingSources.Should().HaveCount(1);
        outputNode.IncomingWeights.Should().HaveCount(1);
    }

    [Fact]
    public void Build_SimpleGenome_NodeActivationTypesMatchNodeCount()
    {
        var genome = CreateSimpleGenome();
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.NodeActivationTypes.Should().HaveCount(gpuNetwork.NodeCount);
    }

    // --- Degenerate genome (zero connections) ---

    [Fact]
    public void Build_GenomeWithNoEnabledConnections_BuildsWithoutError()
    {
        var genome = CreateDegenerateGenome();

        var act = () => (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        act.Should().NotThrow();
    }

    [Fact]
    public void Build_GenomeWithNoEnabledConnections_HasOutputNodeInEvalOrder()
    {
        var genome = CreateDegenerateGenome();
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        // Output nodes are always in the reachable set and eval order,
        // even with no incoming connections. They evaluate sigmoid(0) = 0.5.
        gpuNetwork.EvalOrder.Should().HaveCount(1);
        gpuNetwork.EvalOrder[0].IncomingSources.Should().BeEmpty();
    }

    [Fact]
    public void Build_GenomeWithNoEnabledConnections_StillHasInputAndOutputIndices()
    {
        var genome = CreateDegenerateGenome();
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        gpuNetwork.InputIndices.Should().HaveCount(2);
        gpuNetwork.OutputIndices.Should().HaveCount(1);
    }

    [Fact]
    public void Build_GenomeWithAllDisabledConnections_HasOutputNodeInEvalOrder()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 2, 1.0, false),
            new(2, 1, 2, 1.0, false),
        };
        var genome = new Genome(nodes, connections);
        var gpuNetwork = (GpuFeedForwardNetwork)GpuBuilder.Build(genome);

        // Output node is always in eval order even with no incoming connections
        gpuNetwork.EvalOrder.Should().HaveCount(1);
        gpuNetwork.EvalOrder[0].IncomingSources.Should().BeEmpty();
    }

    // --- Helpers ---

    /// <summary>
    /// Creates a simple genome: 2 inputs + bias -> output (3 connections).
    /// </summary>
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

    /// <summary>
    /// Creates a genome with a hidden node: 2 inputs + bias -> hidden -> output.
    /// </summary>
    private static Genome CreateGenomeWithHiddenNode()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Bias),
            new(3, NodeType.Output),
            new(4, NodeType.Hidden),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 4, 1.0, true),
            new(2, 1, 4, 1.0, true),
            new(3, 2, 4, -0.5, true),
            new(4, 4, 3, 2.0, true),
        };
        return new Genome(nodes, connections);
    }

    /// <summary>
    /// Creates a degenerate genome with nodes but no connections.
    /// </summary>
    private static Genome CreateDegenerateGenome()
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Output),
        };
        var connections = Array.Empty<ConnectionGene>();
        return new Genome(nodes, connections);
    }
}
