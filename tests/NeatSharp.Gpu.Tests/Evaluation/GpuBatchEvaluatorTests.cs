using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Detection;
using NeatSharp.Gpu.Evaluation;
using Xunit;

namespace NeatSharp.Gpu.Tests.Evaluation;

public class GpuBatchEvaluatorTests : IDisposable
{
    private GpuBatchEvaluator? _evaluator;

    public void Dispose()
    {
        _evaluator?.Dispose();
        GC.SuppressFinalize(this);
    }

    // --- Test helpers ---

    private class StubDeviceDetector : IGpuDeviceDetector
    {
        public IGpuDeviceInfo? Detect() =>
            new GpuDeviceInfo("Test CPU", new Version(8, 0), 1024 * 1024 * 1024, true, null);
    }

    private class XorFitnessFunction : IGpuFitnessFunction
    {
        private static readonly float[] Inputs = [0f, 0f, 0f, 1f, 1f, 0f, 1f, 1f]; // 4 cases x 2 inputs
        private static readonly float[] Expected = [0f, 1f, 1f, 0f];

        public int CaseCount => 4;
        public int OutputCount => 1;
        public ReadOnlyMemory<float> InputCases => Inputs;

        public double ComputeFitness(ReadOnlySpan<float> outputs)
        {
            double fitness = 0;
            for (int i = 0; i < CaseCount; i++)
            {
                double error = Math.Abs(Expected[i] - outputs[i]);
                fitness += 1.0 - error;
            }
            return fitness;
        }
    }

    private GpuBatchEvaluator CreateEvaluator(
        IGpuFitnessFunction? fitnessFunction = null,
        IGpuDeviceDetector? detector = null,
        GpuOptions? options = null)
    {
        _evaluator = new GpuBatchEvaluator(
            detector ?? new StubDeviceDetector(),
            fitnessFunction ?? new XorFitnessFunction(),
            Options.Create(options ?? new GpuOptions()),
            NullLogger<GpuBatchEvaluator>.Instance);
        return _evaluator;
    }

    private static (Genome genome, GpuFeedForwardNetwork network) BuildXorGenome(
        double biasToOutputWeight = -1.5,
        double in0ToOutputWeight = 3.0,
        double in1ToOutputWeight = 3.0)
    {
        var registry = new ActivationFunctionRegistry();
        var innerBuilder = new FeedForwardNetworkBuilder(registry);
        var gpuBuilder = new GpuNetworkBuilder(innerBuilder);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),    // input 0
            new(1, NodeType.Input),    // input 1
            new(2, NodeType.Bias),     // bias
            new(3, NodeType.Output),   // output
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 3, in0ToOutputWeight, true),
            new(2, 1, 3, in1ToOutputWeight, true),
            new(3, 2, 3, biasToOutputWeight, true),
        };
        var genome = new Genome(nodes, connections);
        var network = (GpuFeedForwardNetwork)gpuBuilder.Build(genome);
        return (genome, network);
    }

    private static GpuFeedForwardNetwork BuildGenomeWithHidden(
        double weight1 = 1.0,
        double weight2 = 1.0,
        double hiddenToOutputWeight = 2.0,
        double biasWeight = -0.5)
    {
        var registry = new ActivationFunctionRegistry();
        var innerBuilder = new FeedForwardNetworkBuilder(registry);
        var gpuBuilder = new GpuNetworkBuilder(innerBuilder);

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
            new(1, 0, 4, weight1, true),
            new(2, 1, 4, weight2, true),
            new(3, 2, 4, biasWeight, true),
            new(4, 4, 3, hiddenToOutputWeight, true),
        };
        var genome = new Genome(nodes, connections);
        return (GpuFeedForwardNetwork)gpuBuilder.Build(genome);
    }

    /// <summary>
    /// Manually computes the expected output for a simple perceptron
    /// (2 inputs + bias -> output with sigmoid activation) using the GPU's fp32 sigmoid.
    /// </summary>
    private static float ExpectedSigmoid(float in0, float in1, float w0, float w1, float biasW)
    {
        float sum = in0 * w0 + in1 * w1 + 1.0f * biasW;
        return 1.0f / (1.0f + MathF.Exp(-4.9f * sum));
    }

    // --- Single genome evaluation ---

    [Fact]
    public async Task EvaluateAsync_SingleGenome_CallsSetFitnessWithCorrectIndex()
    {
        var evaluator = CreateEvaluator();
        var (_, network) = BuildXorGenome();

        int calledIndex = -1;
        double calledFitness = -1;

        await evaluator.EvaluateAsync(
            [network],
            (index, fitness) =>
            {
                calledIndex = index;
                calledFitness = fitness;
            },
            CancellationToken.None);

        calledIndex.Should().Be(0);
        calledFitness.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EvaluateAsync_SingleGenome_FitnessIsReasonable()
    {
        var evaluator = CreateEvaluator();
        var (_, network) = BuildXorGenome();

        double fitness = 0;
        await evaluator.EvaluateAsync(
            [network],
            (_, f) => fitness = f,
            CancellationToken.None);

        // For XOR with a single-layer perceptron, fitness should be > 0 and <= 4.0
        // (max possible fitness = 4.0 if all outputs perfectly match)
        fitness.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(4.0);
    }

    [Fact]
    public async Task EvaluateAsync_SingleGenome_ProducesExpectedOutputValues()
    {
        // Verify GPU outputs match manually computed sigmoid values within fp32 tolerance
        var evaluator = CreateEvaluator();
        var (_, network) = BuildXorGenome(biasToOutputWeight: -1.5, in0ToOutputWeight: 3.0, in1ToOutputWeight: 3.0);

        // XOR inputs: (0,0), (0,1), (1,0), (1,1)
        // Expected sigmoid outputs:
        // (0,0): sigmoid(0*3 + 0*3 + 1*(-1.5)) = sigmoid(-1.5)
        // (0,1): sigmoid(0*3 + 1*3 + 1*(-1.5)) = sigmoid(1.5)
        // (1,0): sigmoid(1*3 + 0*3 + 1*(-1.5)) = sigmoid(1.5)
        // (1,1): sigmoid(1*3 + 1*3 + 1*(-1.5)) = sigmoid(4.5)
        float exp00 = ExpectedSigmoid(0, 0, 3.0f, 3.0f, -1.5f);
        float exp01 = ExpectedSigmoid(0, 1, 3.0f, 3.0f, -1.5f);
        float exp10 = ExpectedSigmoid(1, 0, 3.0f, 3.0f, -1.5f);
        float exp11 = ExpectedSigmoid(1, 1, 3.0f, 3.0f, -1.5f);

        // Manually compute expected fitness
        double expectedFitness =
            (1.0 - Math.Abs(0f - exp00)) +
            (1.0 - Math.Abs(1f - exp01)) +
            (1.0 - Math.Abs(1f - exp10)) +
            (1.0 - Math.Abs(0f - exp11));

        double actualFitness = 0;
        await evaluator.EvaluateAsync(
            [network],
            (_, f) => actualFitness = f,
            CancellationToken.None);

        actualFitness.Should().BeApproximately(expectedFitness, 1e-4,
            because: "GPU fp32 evaluation should match manual fp32 computation");
    }

    // --- Batch evaluation (multiple genomes) ---

    [Fact]
    public async Task EvaluateAsync_MultipleGenomes_CallsSetFitnessForEach()
    {
        var evaluator = CreateEvaluator();
        var (_, network1) = BuildXorGenome(biasToOutputWeight: -1.5, in0ToOutputWeight: 3.0, in1ToOutputWeight: 3.0);
        var (_, network2) = BuildXorGenome(biasToOutputWeight: -0.5, in0ToOutputWeight: 1.0, in1ToOutputWeight: 1.0);
        var (_, network3) = BuildXorGenome(biasToOutputWeight: -2.0, in0ToOutputWeight: 5.0, in1ToOutputWeight: 5.0);

        var fitnessResults = new Dictionary<int, double>();
        await evaluator.EvaluateAsync(
            [network1, network2, network3],
            (index, fitness) => fitnessResults[index] = fitness,
            CancellationToken.None);

        fitnessResults.Should().HaveCount(3);
        fitnessResults.Should().ContainKeys(0, 1, 2);
    }

    [Fact]
    public async Task EvaluateAsync_MultipleGenomesWithDifferentWeights_ProduceDifferentFitnesses()
    {
        var evaluator = CreateEvaluator();
        var (_, network1) = BuildXorGenome(biasToOutputWeight: -1.5, in0ToOutputWeight: 3.0, in1ToOutputWeight: 3.0);
        var (_, network2) = BuildXorGenome(biasToOutputWeight: -0.5, in0ToOutputWeight: 1.0, in1ToOutputWeight: 1.0);
        var (_, network3) = BuildXorGenome(biasToOutputWeight: -2.0, in0ToOutputWeight: 5.0, in1ToOutputWeight: 5.0);

        var fitnessResults = new Dictionary<int, double>();
        await evaluator.EvaluateAsync(
            [network1, network2, network3],
            (index, fitness) => fitnessResults[index] = fitness,
            CancellationToken.None);

        // Different weights should produce different fitness values
        var uniqueFitnesses = fitnessResults.Values.Distinct().ToList();
        uniqueFitnesses.Should().HaveCountGreaterThanOrEqualTo(2,
            because: "genomes with different weights should produce different fitness scores");
    }

    [Fact]
    public async Task EvaluateAsync_IdenticalGenomes_ProduceSameFitness()
    {
        var evaluator = CreateEvaluator();
        var (_, network1) = BuildXorGenome(biasToOutputWeight: -1.5, in0ToOutputWeight: 3.0, in1ToOutputWeight: 3.0);
        var (_, network2) = BuildXorGenome(biasToOutputWeight: -1.5, in0ToOutputWeight: 3.0, in1ToOutputWeight: 3.0);

        var fitnessResults = new Dictionary<int, double>();
        await evaluator.EvaluateAsync(
            [network1, network2],
            (index, fitness) => fitnessResults[index] = fitness,
            CancellationToken.None);

        fitnessResults[0].Should().BeApproximately(fitnessResults[1], 1e-6,
            because: "identical genomes should produce identical fitness");
    }

    // --- Heterogeneous topologies ---

    [Fact]
    public async Task EvaluateAsync_HeterogeneousTopologies_AllEvaluateCorrectly()
    {
        var evaluator = CreateEvaluator();

        // Simple perceptron (no hidden nodes)
        var (_, simpleNetwork) = BuildXorGenome();

        // Network with hidden node
        var hiddenNetwork = BuildGenomeWithHidden();

        var fitnessResults = new Dictionary<int, double>();
        await evaluator.EvaluateAsync(
            [simpleNetwork, hiddenNetwork],
            (index, fitness) => fitnessResults[index] = fitness,
            CancellationToken.None);

        fitnessResults.Should().HaveCount(2);
        fitnessResults[0].Should().BeGreaterThan(0);
        fitnessResults[1].Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EvaluateAsync_MixOfSimpleAndComplexGenomes_AllReceiveFitness()
    {
        var evaluator = CreateEvaluator();

        var (_, simple1) = BuildXorGenome(in0ToOutputWeight: 1.0, in1ToOutputWeight: 1.0, biasToOutputWeight: -0.5);
        var (_, simple2) = BuildXorGenome(in0ToOutputWeight: 2.0, in1ToOutputWeight: 2.0, biasToOutputWeight: -1.0);
        var complex1 = BuildGenomeWithHidden(weight1: 1.0, weight2: 1.0, hiddenToOutputWeight: 2.0, biasWeight: -0.5);
        var complex2 = BuildGenomeWithHidden(weight1: 3.0, weight2: 3.0, hiddenToOutputWeight: 4.0, biasWeight: -2.0);

        var fitnessResults = new Dictionary<int, double>();
        await evaluator.EvaluateAsync(
            [simple1, complex1, simple2, complex2],
            (index, fitness) => fitnessResults[index] = fitness,
            CancellationToken.None);

        fitnessResults.Should().HaveCount(4);
        fitnessResults.Values.Should().AllSatisfy(f => f.Should().BeGreaterThan(0));
    }

    // --- Degenerate genomes ---

    [Fact]
    public async Task EvaluateAsync_DegenerateGenomeNoConnections_DoesNotCrash()
    {
        var evaluator = CreateEvaluator();

        var registry = new ActivationFunctionRegistry();
        var innerBuilder = new FeedForwardNetworkBuilder(registry);
        var gpuBuilder = new GpuNetworkBuilder(innerBuilder);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Output),
        };
        var genome = new Genome(nodes, Array.Empty<ConnectionGene>());
        var network = (GpuFeedForwardNetwork)gpuBuilder.Build(genome);

        var fitnessResults = new Dictionary<int, double>();
        await evaluator.EvaluateAsync(
            [network],
            (index, fitness) => fitnessResults[index] = fitness,
            CancellationToken.None);

        fitnessResults.Should().ContainKey(0);
        // Output should be sigmoid(0) = 0.5 for all cases since no connections
        // Fitness should be computed from those default outputs
        fitnessResults[0].Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task EvaluateAsync_DegenerateGenome_OutputIsSigmoidOfZero()
    {
        // With no connections, the output node receives sum=0, so output = sigmoid(0)
        // sigmoid(0) = 1/(1+exp(0)) = 0.5
        float sigmoidZero = 1.0f / (1.0f + MathF.Exp(-4.9f * 0.0f));

        // Build a fitness function that captures raw outputs
        var capturedOutputs = new List<float>();
        var captureFitnessFunction = new CapturingFitnessFunction(capturedOutputs);

        var evaluator = CreateEvaluator(fitnessFunction: captureFitnessFunction);

        var registry = new ActivationFunctionRegistry();
        var innerBuilder = new FeedForwardNetworkBuilder(registry);
        var gpuBuilder = new GpuNetworkBuilder(innerBuilder);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Output),
        };
        var genome = new Genome(nodes, Array.Empty<ConnectionGene>());
        var network = (GpuFeedForwardNetwork)gpuBuilder.Build(genome);

        await evaluator.EvaluateAsync(
            [network],
            (_, _) => { },
            CancellationToken.None);

        // Degenerate genome: output node has no incoming connections, sum=0,
        // sigmoid(0) with steepened factor 4.9: 1/(1+exp(-4.9*0)) = 1/(1+1) = 0.5
        capturedOutputs.Should().HaveCount(4); // 4 test cases x 1 output
        capturedOutputs.Should().AllSatisfy(o =>
            o.Should().BeApproximately(0.5f, 1e-4f));
    }

    // --- CPU fallback on non-GPU genomes ---

    [Fact]
    public async Task EvaluateAsync_PlainIGenome_FallsBackToCpu()
    {
        var evaluator = CreateEvaluator();

        // Build CPU-only networks (not GpuFeedForwardNetwork)
        var registry = new ActivationFunctionRegistry();
        var cpuBuilder = new FeedForwardNetworkBuilder(registry);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Bias),
            new(3, NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 3, 3.0, true),
            new(2, 1, 3, 3.0, true),
            new(3, 2, 3, -1.5, true),
        };
        var genome = new Genome(nodes, connections);
        var cpuNetwork = cpuBuilder.Build(genome);

        var fitnessResults = new Dictionary<int, double>();
        await evaluator.EvaluateAsync(
            [cpuNetwork],
            (index, fitness) => fitnessResults[index] = fitness,
            CancellationToken.None);

        fitnessResults.Should().ContainKey(0);
        fitnessResults[0].Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EvaluateAsync_PlainIGenome_ProducesCorrectFitness()
    {
        var evaluator = CreateEvaluator();

        var registry = new ActivationFunctionRegistry();
        var cpuBuilder = new FeedForwardNetworkBuilder(registry);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Bias),
            new(3, NodeType.Output),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 3, 3.0, true),
            new(2, 1, 3, 3.0, true),
            new(3, 2, 3, -1.5, true),
        };
        var genome = new Genome(nodes, connections);
        var cpuNetwork = cpuBuilder.Build(genome);

        // Also evaluate same genome on GPU path for comparison
        var gpuBuilder = new GpuNetworkBuilder(new FeedForwardNetworkBuilder(new ActivationFunctionRegistry()));
        var gpuNetwork = gpuBuilder.Build(genome);

        double cpuFitness = 0;
        double gpuFitness = 0;

        // CPU fallback path
        await evaluator.EvaluateAsync(
            [cpuNetwork],
            (_, f) => cpuFitness = f,
            CancellationToken.None);

        // GPU path - create a separate evaluator
        using var gpuEvaluator = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            new XorFitnessFunction(),
            Options.Create(new GpuOptions()),
            NullLogger<GpuBatchEvaluator>.Instance);
        await gpuEvaluator.EvaluateAsync(
            [gpuNetwork],
            (_, f) => gpuFitness = f,
            CancellationToken.None);

        // CPU uses fp64, GPU uses fp32 — tolerance of 1e-4
        cpuFitness.Should().BeApproximately(gpuFitness, 1e-4,
            because: "CPU fallback and GPU paths should produce similar fitness for the same genome");
    }

    // --- IDisposable cleanup ---

    [Fact]
    public async Task EvaluateAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var evaluator = CreateEvaluator();
        var (_, network) = BuildXorGenome();

        // Trigger GPU initialization by evaluating once
        await evaluator.EvaluateAsync(
            [network],
            (_, _) => { },
            CancellationToken.None);

        evaluator.Dispose();

        var act = () => evaluator.EvaluateAsync(
            [network],
            (_, _) => { },
            CancellationToken.None);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var evaluator = CreateEvaluator();

        var act = () =>
        {
            evaluator.Dispose();
            evaluator.Dispose();
            evaluator.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_WithoutEvaluation_DoesNotThrow()
    {
        var evaluator = CreateEvaluator();

        var act = () => evaluator.Dispose();

        act.Should().NotThrow();
    }

    // --- Buffer reuse ---

    [Fact]
    public async Task EvaluateAsync_CalledTwice_SecondCallProducesCorrectResults()
    {
        var evaluator = CreateEvaluator();
        var (_, network) = BuildXorGenome();

        double fitness1 = 0;
        double fitness2 = 0;

        await evaluator.EvaluateAsync(
            [network],
            (_, f) => fitness1 = f,
            CancellationToken.None);

        await evaluator.EvaluateAsync(
            [network],
            (_, f) => fitness2 = f,
            CancellationToken.None);

        fitness1.Should().BeGreaterThan(0);
        fitness2.Should().Be(fitness1,
            because: "evaluating the same genome twice should produce identical results");
    }

    [Fact]
    public async Task EvaluateAsync_DifferentPopulationSizes_BufferReuseWorks()
    {
        var evaluator = CreateEvaluator();
        var (_, network1) = BuildXorGenome(in0ToOutputWeight: 1.0, in1ToOutputWeight: 1.0, biasToOutputWeight: -0.5);
        var (_, network2) = BuildXorGenome(in0ToOutputWeight: 2.0, in1ToOutputWeight: 2.0, biasToOutputWeight: -1.0);
        var (_, network3) = BuildXorGenome(in0ToOutputWeight: 3.0, in1ToOutputWeight: 3.0, biasToOutputWeight: -1.5);

        // First call with 1 genome
        var results1 = new Dictionary<int, double>();
        await evaluator.EvaluateAsync(
            [network1],
            (index, fitness) => results1[index] = fitness,
            CancellationToken.None);

        // Second call with 3 genomes (larger population triggers buffer growth)
        var results2 = new Dictionary<int, double>();
        await evaluator.EvaluateAsync(
            [network1, network2, network3],
            (index, fitness) => results2[index] = fitness,
            CancellationToken.None);

        // Third call with 2 genomes (smaller population reuses existing buffers)
        var results3 = new Dictionary<int, double>();
        await evaluator.EvaluateAsync(
            [network1, network2],
            (index, fitness) => results3[index] = fitness,
            CancellationToken.None);

        results1.Should().HaveCount(1);
        results2.Should().HaveCount(3);
        results3.Should().HaveCount(2);

        // The same genome should produce the same fitness across all calls
        results1[0].Should().BeApproximately(results2[0], 1e-6);
        results1[0].Should().BeApproximately(results3[0], 1e-6);
    }

    // --- Empty genome list ---

    [Fact]
    public async Task EvaluateAsync_EmptyGenomeList_CompletesWithoutError()
    {
        var evaluator = CreateEvaluator();

        var act = () => evaluator.EvaluateAsync(
            Array.Empty<IGenome>(),
            (_, _) => { },
            CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    // --- Null argument validation ---

    [Fact]
    public async Task EvaluateAsync_NullGenomes_ThrowsArgumentNullException()
    {
        var evaluator = CreateEvaluator();

        var act = () => evaluator.EvaluateAsync(
            null!,
            (_, _) => { },
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EvaluateAsync_NullSetFitness_ThrowsArgumentNullException()
    {
        var evaluator = CreateEvaluator();
        var (_, network) = BuildXorGenome();

        var act = () => evaluator.EvaluateAsync(
            [network],
            null!,
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // --- Cancellation ---

    [Fact]
    public async Task EvaluateAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var evaluator = CreateEvaluator();
        var (_, network) = BuildXorGenome();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => evaluator.EvaluateAsync(
            [network],
            (_, _) => { },
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- GPU disabled (CPU fallback via options) ---

    [Fact]
    public async Task EvaluateAsync_GpuDisabledInOptions_UsesGpuGenomesViaCpuFallback()
    {
        var options = new GpuOptions { EnableGpu = false };
        var evaluator = CreateEvaluator(options: options);
        var (_, network) = BuildXorGenome();

        var fitnessResults = new Dictionary<int, double>();
        await evaluator.EvaluateAsync(
            [network],
            (index, fitness) => fitnessResults[index] = fitness,
            CancellationToken.None);

        // Even with GPU disabled, GpuFeedForwardNetwork genomes still check the GPU path
        // but fall back to CPU. They should still produce reasonable fitness.
        fitnessResults.Should().ContainKey(0);
        fitnessResults[0].Should().BeGreaterThan(0);
    }

    // --- Known output verification ---

    [Fact]
    public async Task EvaluateAsync_KnownWeights_OutputsMatchManualComputation()
    {
        // Use specific weights where we can manually verify the sigmoid output
        var evaluator = CreateEvaluator();
        var (_, network) = BuildXorGenome(biasToOutputWeight: 0.0, in0ToOutputWeight: 0.0, in1ToOutputWeight: 0.0);

        // With all zero weights, sum is always 0, sigmoid(0) = 1/(1+exp(0)) = 0.5
        // For XOR expected [0, 1, 1, 0]:
        // fitness = (1-|0-0.5|) + (1-|1-0.5|) + (1-|1-0.5|) + (1-|0-0.5|) = 0.5+0.5+0.5+0.5 = 2.0
        double fitness = 0;
        await evaluator.EvaluateAsync(
            [network],
            (_, f) => fitness = f,
            CancellationToken.None);

        fitness.Should().BeApproximately(2.0, 1e-4,
            because: "all-zero weights produce sigmoid(0)=0.5 output for each case");
    }

    // --- Capturing fitness function helper ---

    private class CapturingFitnessFunction : IGpuFitnessFunction
    {
        private static readonly float[] Inputs = [0f, 0f, 0f, 1f, 1f, 0f, 1f, 1f];
        private readonly List<float> _capturedOutputs;

        public CapturingFitnessFunction(List<float> capturedOutputs)
        {
            _capturedOutputs = capturedOutputs;
        }

        public int CaseCount => 4;
        public int OutputCount => 1;
        public ReadOnlyMemory<float> InputCases => Inputs;

        public double ComputeFitness(ReadOnlySpan<float> outputs)
        {
            for (int i = 0; i < outputs.Length; i++)
            {
                _capturedOutputs.Add(outputs[i]);
            }
            return 1.0;
        }
    }

    // ==========================================================================
    // Phase 5 — US3: CPU/GPU Result Consistency Tests (T025)
    // ==========================================================================

    /// <summary>
    /// Builds a genome with a specified activation function for consistency testing.
    /// </summary>
    private static (GpuFeedForwardNetwork gpuNetwork, IGenome cpuNetwork) BuildGenomeWithActivation(
        string activationFunction,
        double w0 = 3.0,
        double w1 = 3.0,
        double biasW = -1.5)
    {
        var registry = new ActivationFunctionRegistry();
        var cpuBuilder = new FeedForwardNetworkBuilder(registry);
        var gpuBuilder = new GpuNetworkBuilder(cpuBuilder);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Bias),
            new(3, NodeType.Output, activationFunction),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 3, w0, true),
            new(2, 1, 3, w1, true),
            new(3, 2, 3, biasW, true),
        };
        var genome = new Genome(nodes, connections);

        // Build CPU network separately (fresh builder to get independent IGenome)
        var cpuRegistry = new ActivationFunctionRegistry();
        var cpuOnlyBuilder = new FeedForwardNetworkBuilder(cpuRegistry);
        var cpuNetwork = cpuOnlyBuilder.Build(genome);

        // Build GPU network (wraps a CPU network + GPU topology)
        var gpuNetwork = (GpuFeedForwardNetwork)gpuBuilder.Build(genome);

        return (gpuNetwork, cpuNetwork);
    }

    /// <summary>
    /// Builds a medium-complexity genome with one hidden layer for consistency testing.
    /// </summary>
    private static (GpuFeedForwardNetwork gpuNetwork, IGenome cpuNetwork) BuildMediumGenome(
        string activationFunction = ActivationFunctions.Sigmoid)
    {
        var registry = new ActivationFunctionRegistry();
        var cpuBuilder = new FeedForwardNetworkBuilder(registry);
        var gpuBuilder = new GpuNetworkBuilder(cpuBuilder);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Bias),
            new(3, NodeType.Output, activationFunction),
            new(4, NodeType.Hidden, activationFunction),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 4, 2.0, true),
            new(2, 1, 4, 1.5, true),
            new(3, 2, 4, -0.5, true),
            new(4, 4, 3, 3.0, true),
            new(5, 2, 3, -1.0, true),
        };
        var genome = new Genome(nodes, connections);

        var cpuRegistry = new ActivationFunctionRegistry();
        var cpuOnlyBuilder = new FeedForwardNetworkBuilder(cpuRegistry);
        var cpuNetwork = cpuOnlyBuilder.Build(genome);
        var gpuNetwork = (GpuFeedForwardNetwork)gpuBuilder.Build(genome);

        return (gpuNetwork, cpuNetwork);
    }

    /// <summary>
    /// Builds a complex genome with two hidden layers for consistency testing.
    /// </summary>
    private static (GpuFeedForwardNetwork gpuNetwork, IGenome cpuNetwork) BuildComplexGenome(
        string activationFunction = ActivationFunctions.Sigmoid)
    {
        var registry = new ActivationFunctionRegistry();
        var cpuBuilder = new FeedForwardNetworkBuilder(registry);
        var gpuBuilder = new GpuNetworkBuilder(cpuBuilder);

        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Bias),
            new(3, NodeType.Output, activationFunction),
            new(4, NodeType.Hidden, activationFunction),
            new(5, NodeType.Hidden, activationFunction),
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 4, 1.0, true),
            new(2, 1, 4, 1.0, true),
            new(3, 2, 4, -0.3, true),
            new(4, 0, 5, 0.5, true),
            new(5, 1, 5, -0.5, true),
            new(6, 2, 5, 0.2, true),
            new(7, 4, 3, 2.0, true),
            new(8, 5, 3, -1.0, true),
            new(9, 2, 3, -0.7, true),
        };
        var genome = new Genome(nodes, connections);

        var cpuRegistry = new ActivationFunctionRegistry();
        var cpuOnlyBuilder = new FeedForwardNetworkBuilder(cpuRegistry);
        var cpuNetwork = cpuOnlyBuilder.Build(genome);
        var gpuNetwork = (GpuFeedForwardNetwork)gpuBuilder.Build(genome);

        return (gpuNetwork, cpuNetwork);
    }

    /// <summary>
    /// Helper: evaluate a single genome on GPU and capture raw outputs.
    /// </summary>
    private float[] EvaluateOnGpuAndCaptureOutputs(GpuFeedForwardNetwork gpuNetwork)
    {
        var capturedOutputs = new List<float>();
        var captureFitness = new CapturingFitnessFunction(capturedOutputs);
        using var evaluator = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            captureFitness,
            Options.Create(new GpuOptions()),
            NullLogger<GpuBatchEvaluator>.Instance);

        evaluator.EvaluateAsync(
            [gpuNetwork],
            (_, _) => { },
            CancellationToken.None).Wait();

        return capturedOutputs.ToArray();
    }

    /// <summary>
    /// Helper: evaluate a single genome on CPU and return outputs as float[].
    /// XOR inputs: (0,0), (0,1), (1,0), (1,1) — 4 cases, 2 inputs, 1 output.
    /// </summary>
    private static float[] EvaluateOnCpu(IGenome cpuNetwork)
    {
        float[][] xorInputs = [[0f, 0f], [0f, 1f], [1f, 0f], [1f, 1f]];
        var outputs = new float[xorInputs.Length];

        Span<double> cpuInputs = stackalloc double[2];
        Span<double> cpuOutputs = stackalloc double[1];

        for (int i = 0; i < xorInputs.Length; i++)
        {
            cpuInputs[0] = xorInputs[i][0];
            cpuInputs[1] = xorInputs[i][1];
            cpuNetwork.Activate(cpuInputs, cpuOutputs);
            outputs[i] = (float)cpuOutputs[0];
        }

        return outputs;
    }

    [Fact]
    public void EvaluateAsync_SimpleTopology_CpuGpuOutputsWithinEpsilon()
    {
        // T025: Simple topology — 2 inputs + bias -> output (sigmoid)
        var (gpuNetwork, cpuNetwork) = BuildGenomeWithActivation(ActivationFunctions.Sigmoid);

        var gpuOutputs = EvaluateOnGpuAndCaptureOutputs(gpuNetwork);
        var cpuOutputs = EvaluateOnCpu(cpuNetwork);

        gpuOutputs.Should().HaveCount(cpuOutputs.Length);
        for (int i = 0; i < gpuOutputs.Length; i++)
        {
            ((double)gpuOutputs[i]).Should().BeApproximately(cpuOutputs[i], 1e-4,
                because: $"GPU fp32 and CPU fp64 output for case {i} should agree within 1e-4 epsilon");
        }
    }

    [Fact]
    public void EvaluateAsync_MediumTopology_CpuGpuOutputsWithinEpsilon()
    {
        // T025: Medium topology — 2 inputs + bias -> hidden -> output (sigmoid)
        var (gpuNetwork, cpuNetwork) = BuildMediumGenome();

        var gpuOutputs = EvaluateOnGpuAndCaptureOutputs(gpuNetwork);
        var cpuOutputs = EvaluateOnCpu(cpuNetwork);

        gpuOutputs.Should().HaveCount(cpuOutputs.Length);
        for (int i = 0; i < gpuOutputs.Length; i++)
        {
            ((double)gpuOutputs[i]).Should().BeApproximately(cpuOutputs[i], 1e-4,
                because: $"GPU fp32 and CPU fp64 output for case {i} should agree within 1e-4 (medium topology)");
        }
    }

    [Fact]
    public void EvaluateAsync_ComplexTopology_CpuGpuOutputsWithinEpsilon()
    {
        // T025: Complex topology — 2 inputs + bias -> 2 hidden -> output (sigmoid)
        var (gpuNetwork, cpuNetwork) = BuildComplexGenome();

        var gpuOutputs = EvaluateOnGpuAndCaptureOutputs(gpuNetwork);
        var cpuOutputs = EvaluateOnCpu(cpuNetwork);

        gpuOutputs.Should().HaveCount(cpuOutputs.Length);
        for (int i = 0; i < gpuOutputs.Length; i++)
        {
            ((double)gpuOutputs[i]).Should().BeApproximately(cpuOutputs[i], 1e-4,
                because: $"GPU fp32 and CPU fp64 output for case {i} should agree within 1e-4 (complex topology)");
        }
    }

    [Fact]
    public async Task EvaluateAsync_SimpleTopology_CpuGpuFitnessWithinEpsilon()
    {
        // T025: Per-genome fitness comparison — simple topology
        var (gpuNetwork, cpuNetwork) = BuildGenomeWithActivation(ActivationFunctions.Sigmoid);

        // GPU fitness
        double gpuFitness = 0;
        using var gpuEvaluator = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            new XorFitnessFunction(),
            Options.Create(new GpuOptions()),
            NullLogger<GpuBatchEvaluator>.Instance);
        await gpuEvaluator.EvaluateAsync(
            [gpuNetwork],
            (_, f) => gpuFitness = f,
            CancellationToken.None);

        // CPU fitness via CPU fallback path
        double cpuFitness = 0;
        using var cpuEvaluator = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            new XorFitnessFunction(),
            Options.Create(new GpuOptions()),
            NullLogger<GpuBatchEvaluator>.Instance);
        await cpuEvaluator.EvaluateAsync(
            [cpuNetwork],
            (_, f) => cpuFitness = f,
            CancellationToken.None);

        gpuFitness.Should().BeApproximately(cpuFitness, 1e-4,
            because: "GPU fp32 and CPU fp64 fitness should agree within 1e-4 for simple topology");
    }

    [Fact]
    public async Task EvaluateAsync_MediumTopology_CpuGpuFitnessWithinEpsilon()
    {
        // T025: Per-genome fitness comparison — medium topology
        var (gpuNetwork, cpuNetwork) = BuildMediumGenome();

        double gpuFitness = 0;
        using var gpuEvaluator = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            new XorFitnessFunction(),
            Options.Create(new GpuOptions()),
            NullLogger<GpuBatchEvaluator>.Instance);
        await gpuEvaluator.EvaluateAsync(
            [gpuNetwork],
            (_, f) => gpuFitness = f,
            CancellationToken.None);

        double cpuFitness = 0;
        using var cpuEvaluator = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            new XorFitnessFunction(),
            Options.Create(new GpuOptions()),
            NullLogger<GpuBatchEvaluator>.Instance);
        await cpuEvaluator.EvaluateAsync(
            [cpuNetwork],
            (_, f) => cpuFitness = f,
            CancellationToken.None);

        gpuFitness.Should().BeApproximately(cpuFitness, 1e-4,
            because: "GPU fp32 and CPU fp64 fitness should agree within 1e-4 for medium topology");
    }

    [Fact]
    public async Task EvaluateAsync_ComplexTopology_CpuGpuFitnessWithinEpsilon()
    {
        // T025: Per-genome fitness comparison — complex topology
        var (gpuNetwork, cpuNetwork) = BuildComplexGenome();

        double gpuFitness = 0;
        using var gpuEvaluator = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            new XorFitnessFunction(),
            Options.Create(new GpuOptions()),
            NullLogger<GpuBatchEvaluator>.Instance);
        await gpuEvaluator.EvaluateAsync(
            [gpuNetwork],
            (_, f) => gpuFitness = f,
            CancellationToken.None);

        double cpuFitness = 0;
        using var cpuEvaluator = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            new XorFitnessFunction(),
            Options.Create(new GpuOptions()),
            NullLogger<GpuBatchEvaluator>.Instance);
        await cpuEvaluator.EvaluateAsync(
            [cpuNetwork],
            (_, f) => cpuFitness = f,
            CancellationToken.None);

        gpuFitness.Should().BeApproximately(cpuFitness, 1e-4,
            because: "GPU fp32 and CPU fp64 fitness should agree within 1e-4 for complex topology");
    }

    [Fact]
    public void EvaluateAsync_BatchConsistency_AllGenomesCpuGpuOutputsWithinEpsilon()
    {
        // T025: Evaluate a batch of mixed-topology genomes and compare each genome's
        // GPU vs CPU outputs within epsilon
        var (simpleGpu, simpleCpu) = BuildGenomeWithActivation(ActivationFunctions.Sigmoid);
        var (mediumGpu, mediumCpu) = BuildMediumGenome();
        var (complexGpu, complexCpu) = BuildComplexGenome();

        var gpuGenomes = new[] { simpleGpu, mediumGpu, complexGpu };
        var cpuGenomes = new IGenome[] { simpleCpu, mediumCpu, complexCpu };

        // Evaluate each on GPU individually (batch evaluator captures outputs per genome)
        for (int g = 0; g < gpuGenomes.Length; g++)
        {
            var gpuOutputs = EvaluateOnGpuAndCaptureOutputs(gpuGenomes[g]);
            var cpuOutputs = EvaluateOnCpu(cpuGenomes[g]);

            gpuOutputs.Should().HaveCount(cpuOutputs.Length);
            for (int i = 0; i < gpuOutputs.Length; i++)
            {
                ((double)gpuOutputs[i]).Should().BeApproximately(cpuOutputs[i], 1e-4,
                    because: $"genome {g}, case {i}: GPU and CPU outputs should agree within 1e-4");
            }
        }
    }

    // ==========================================================================
    // Phase 5 — US3: Canonical XOR Consistency Tests (T026)
    // ==========================================================================

    [Theory]
    [InlineData(ActivationFunctions.Sigmoid)]
    [InlineData(ActivationFunctions.Tanh)]
    [InlineData(ActivationFunctions.ReLU)]
    [InlineData(ActivationFunctions.Step)]
    [InlineData(ActivationFunctions.Identity)]
    public async Task EvaluateAsync_XorWithActivationFunction_CpuGpuFitnessConsistent(
        string activationFunction)
    {
        // T026: XOR consistency test with each activation function.
        // Build a genome using the specified activation function and evaluate
        // on both CPU and GPU, comparing fitness within tolerance.
        var (gpuNetwork, cpuNetwork) = BuildGenomeWithActivation(
            activationFunction, w0: 2.0, w1: 2.0, biasW: -1.0);

        // GPU evaluation
        double gpuFitness = 0;
        using var gpuEvaluator = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            new XorFitnessFunction(),
            Options.Create(new GpuOptions()),
            NullLogger<GpuBatchEvaluator>.Instance);
        await gpuEvaluator.EvaluateAsync(
            [gpuNetwork],
            (_, f) => gpuFitness = f,
            CancellationToken.None);

        // CPU evaluation (via CPU fallback path — non-GPU genomes)
        double cpuFitness = 0;
        using var cpuEvaluator = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            new XorFitnessFunction(),
            Options.Create(new GpuOptions()),
            NullLogger<GpuBatchEvaluator>.Instance);
        await cpuEvaluator.EvaluateAsync(
            [cpuNetwork],
            (_, f) => cpuFitness = f,
            CancellationToken.None);

        gpuFitness.Should().BeApproximately(cpuFitness, 1e-4,
            because: $"XOR fitness with {activationFunction} activation should be consistent between CPU (fp64) and GPU (fp32) within 1e-4");
    }

    [Theory]
    [InlineData(ActivationFunctions.Sigmoid)]
    [InlineData(ActivationFunctions.Tanh)]
    [InlineData(ActivationFunctions.ReLU)]
    [InlineData(ActivationFunctions.Step)]
    [InlineData(ActivationFunctions.Identity)]
    public void EvaluateAsync_XorWithActivationFunction_CpuGpuOutputsConsistent(
        string activationFunction)
    {
        // T026: XOR output-level consistency with each activation function.
        var (gpuNetwork, cpuNetwork) = BuildGenomeWithActivation(
            activationFunction, w0: 2.0, w1: 2.0, biasW: -1.0);

        var gpuOutputs = EvaluateOnGpuAndCaptureOutputs(gpuNetwork);
        var cpuOutputs = EvaluateOnCpu(cpuNetwork);

        gpuOutputs.Should().HaveCount(cpuOutputs.Length);
        for (int i = 0; i < gpuOutputs.Length; i++)
        {
            ((double)gpuOutputs[i]).Should().BeApproximately(cpuOutputs[i], 1e-4,
                because: $"XOR output case {i} with {activationFunction} should match CPU within 1e-4");
        }
    }

    [Fact]
    public async Task EvaluateAsync_XorMediumTopologyAllActivations_ConsistentFitness()
    {
        // T026: XOR consistency with medium topology across all 5 activation functions
        var activationFunctions = new[]
        {
            ActivationFunctions.Sigmoid,
            ActivationFunctions.Tanh,
            ActivationFunctions.ReLU,
            ActivationFunctions.Step,
            ActivationFunctions.Identity
        };

        foreach (var af in activationFunctions)
        {
            var (gpuNetwork, cpuNetwork) = BuildMediumGenome(af);

            double gpuFitness = 0;
            using var gpuEvaluator = new GpuBatchEvaluator(
                new StubDeviceDetector(),
                new XorFitnessFunction(),
                Options.Create(new GpuOptions()),
                NullLogger<GpuBatchEvaluator>.Instance);
            await gpuEvaluator.EvaluateAsync(
                [gpuNetwork],
                (_, f) => gpuFitness = f,
                CancellationToken.None);

            double cpuFitness = 0;
            using var cpuEvaluator = new GpuBatchEvaluator(
                new StubDeviceDetector(),
                new XorFitnessFunction(),
                Options.Create(new GpuOptions()),
                NullLogger<GpuBatchEvaluator>.Instance);
            await cpuEvaluator.EvaluateAsync(
                [cpuNetwork],
                (_, f) => cpuFitness = f,
                CancellationToken.None);

            gpuFitness.Should().BeApproximately(cpuFitness, 1e-4,
                because: $"medium-topology XOR fitness with {af} should be consistent between CPU and GPU");
        }
    }

    // ==========================================================================
    // Phase 6 — US4: Determinism Validation Tests (T029)
    // ==========================================================================

    [Fact]
    public async Task EvaluateAsync_IdenticalWorkloadTwice_ProducesBitwiseIdenticalOutputs()
    {
        // T029: Run identical workload twice with same population, assert bitwise identical
        // GPU outputs. Using ILGPU CPU accelerator, which is fully deterministic.
        //
        // Non-determinism sources documented (not applicable to ILGPU CPU accelerator):
        // - CUDA driver variations: different GPU drivers may produce ULP-level floating-point
        //   differences due to hardware FMA implementation.
        // - FMA (Fused Multiply-Add) instructions: CUDA GPUs may use FMA where the CPU uses
        //   separate multiply + add, producing slightly different rounding.
        // - Cross-machine differences: different GPU architectures (e.g., Ampere vs Hopper)
        //   may have different microarchitectural floating-point behavior.
        // - Thread scheduling: not applicable here because the kernel uses 1 thread per genome
        //   with sequential accumulation (deterministic by design per R-008).

        var (_, network1) = BuildXorGenome(biasToOutputWeight: -1.5, in0ToOutputWeight: 3.0, in1ToOutputWeight: 3.0);
        var (_, network2) = BuildXorGenome(biasToOutputWeight: -0.5, in0ToOutputWeight: 1.0, in1ToOutputWeight: 1.0);
        var hiddenNetwork = BuildGenomeWithHidden(weight1: 2.0, weight2: 1.5, hiddenToOutputWeight: 3.0, biasWeight: -1.0);

        var population = new IGenome[] { network1, network2, hiddenNetwork };

        // First run — capture raw outputs
        var outputs1 = new List<float>();
        var capture1 = new CapturingFitnessFunction(outputs1);
        using var evaluator1 = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            capture1,
            Options.Create(new GpuOptions { BestEffortDeterministic = true }),
            NullLogger<GpuBatchEvaluator>.Instance);
        await evaluator1.EvaluateAsync(
            population,
            (_, _) => { },
            CancellationToken.None);

        // Second run — capture raw outputs with fresh evaluator
        var outputs2 = new List<float>();
        var capture2 = new CapturingFitnessFunction(outputs2);
        using var evaluator2 = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            capture2,
            Options.Create(new GpuOptions { BestEffortDeterministic = true }),
            NullLogger<GpuBatchEvaluator>.Instance);
        await evaluator2.EvaluateAsync(
            population,
            (_, _) => { },
            CancellationToken.None);

        // Assert bitwise identical outputs
        outputs1.Should().HaveCount(outputs2.Count,
            because: "both runs should produce the same number of outputs");
        for (int i = 0; i < outputs1.Count; i++)
        {
            // Bitwise comparison via BitConverter for exact fp32 equality
            BitConverter.SingleToInt32Bits(outputs1[i]).Should().Be(
                BitConverter.SingleToInt32Bits(outputs2[i]),
                because: $"output index {i} should be bitwise identical across deterministic runs");
        }
    }

    [Fact]
    public async Task EvaluateAsync_SameEvaluatorTwice_ProducesBitwiseIdenticalOutputs()
    {
        // T029: Run identical workload twice on the SAME evaluator instance,
        // validating that buffer reuse does not introduce non-determinism.

        var (_, network) = BuildXorGenome(biasToOutputWeight: -1.5, in0ToOutputWeight: 3.0, in1ToOutputWeight: 3.0);
        var hiddenNetwork = BuildGenomeWithHidden();
        var population = new IGenome[] { network, hiddenNetwork };

        var outputs1 = new List<float>();
        var capture1 = new CapturingFitnessFunction(outputs1);
        using var evaluator = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            capture1,
            Options.Create(new GpuOptions { BestEffortDeterministic = true }),
            NullLogger<GpuBatchEvaluator>.Instance);

        // First run
        await evaluator.EvaluateAsync(
            population,
            (_, _) => { },
            CancellationToken.None);
        var firstRunOutputs = outputs1.ToArray();

        // Clear captured outputs and run again
        outputs1.Clear();
        await evaluator.EvaluateAsync(
            population,
            (_, _) => { },
            CancellationToken.None);

        // Assert bitwise identical outputs
        outputs1.Should().HaveCount(firstRunOutputs.Length);
        for (int i = 0; i < outputs1.Count; i++)
        {
            BitConverter.SingleToInt32Bits(outputs1[i]).Should().Be(
                BitConverter.SingleToInt32Bits(firstRunOutputs[i]),
                because: $"output index {i} should be bitwise identical across runs on the same evaluator");
        }
    }

    [Fact]
    public async Task EvaluateAsync_DeterministicMode_FitnessScoresBitwiseIdentical()
    {
        // T029: Verify that fitness scores (not just raw outputs) are bitwise identical
        // across deterministic runs.

        var (_, network1) = BuildXorGenome(biasToOutputWeight: -1.5, in0ToOutputWeight: 3.0, in1ToOutputWeight: 3.0);
        var (_, network2) = BuildXorGenome(biasToOutputWeight: -0.5, in0ToOutputWeight: 1.0, in1ToOutputWeight: 1.0);
        var hiddenNetwork = BuildGenomeWithHidden(weight1: 2.0, weight2: 1.5, hiddenToOutputWeight: 3.0, biasWeight: -1.0);

        var population = new IGenome[] { network1, network2, hiddenNetwork };

        // First run
        var fitness1 = new Dictionary<int, double>();
        using var evaluator1 = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            new XorFitnessFunction(),
            Options.Create(new GpuOptions { BestEffortDeterministic = true }),
            NullLogger<GpuBatchEvaluator>.Instance);
        await evaluator1.EvaluateAsync(
            population,
            (idx, f) => fitness1[idx] = f,
            CancellationToken.None);

        // Second run
        var fitness2 = new Dictionary<int, double>();
        using var evaluator2 = new GpuBatchEvaluator(
            new StubDeviceDetector(),
            new XorFitnessFunction(),
            Options.Create(new GpuOptions { BestEffortDeterministic = true }),
            NullLogger<GpuBatchEvaluator>.Instance);
        await evaluator2.EvaluateAsync(
            population,
            (idx, f) => fitness2[idx] = f,
            CancellationToken.None);

        fitness1.Should().HaveCount(fitness2.Count);
        foreach (var (idx, f1) in fitness1)
        {
            // Use bitwise comparison on the double fitness values for exact match
            BitConverter.DoubleToInt64Bits(f1).Should().Be(
                BitConverter.DoubleToInt64Bits(fitness2[idx]),
                because: $"fitness for genome {idx} should be bitwise identical across deterministic runs");
        }
    }
}
