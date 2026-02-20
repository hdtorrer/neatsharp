using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Detection;
using NeatSharp.Gpu.Evaluation;

namespace NeatSharp.Benchmarks;

/// <summary>
/// GPU evaluator benchmarks measuring batch evaluation throughput
/// via <see cref="GpuBatchEvaluator"/> across various population sizes.
/// Gracefully skips when no CUDA GPU is detected.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class GpuEvaluatorBenchmarks
{
    [Params(150, 500, 1000, 5000)]
    public int PopulationSize { get; set; }

    private IReadOnlyList<IGenome> _genomes = null!;
    private GpuBatchEvaluator? _evaluator;
    private bool _skipGpu;

    [GlobalSetup]
    public void Setup()
    {
        // Detect GPU availability
        var detector = new GpuDeviceDetector(Options.Create(new GpuOptions()));
        var deviceInfo = detector.Detect();
        _skipGpu = deviceInfo is null || !deviceInfo.IsCompatible;

        // Build population with GPU-compatible genomes
        var random = new Random(42);
        var registry = new ActivationFunctionRegistry();
        var cpuBuilder = new FeedForwardNetworkBuilder(registry);
        var gpuBuilder = new GpuNetworkBuilder(cpuBuilder);

        var genomes = new List<IGenome>(PopulationSize);
        for (int i = 0; i < PopulationSize; i++)
        {
            genomes.Add(gpuBuilder.Build(CreateGenome(random)));
        }

        _genomes = genomes;

        if (!_skipGpu)
        {
            var fitnessFunction = new BenchmarkFitnessFunction();
            _evaluator = new GpuBatchEvaluator(
                detector,
                fitnessFunction,
                Options.Create(new GpuOptions()),
                NullLogger<GpuBatchEvaluator>.Instance);
        }
    }

    [Benchmark]
    [BenchmarkCategory("GPU")]
    public void EvaluateBatch()
    {
        if (_skipGpu)
        {
            return;
        }

        _evaluator!.EvaluateAsync(
            _genomes,
            static (_, _) => { },
            CancellationToken.None).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _evaluator?.Dispose();
    }

    private static Genome CreateGenome(Random random)
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input), new(1, NodeType.Input),
            new(2, NodeType.Input), new(3, NodeType.Input),
            new(4, NodeType.Bias), new(5, NodeType.Output)
        };
        var connections = new ConnectionGene[]
        {
            new(1, 0, 5, random.NextDouble() * 4.0 - 2.0, true),
            new(2, 1, 5, random.NextDouble() * 4.0 - 2.0, true),
            new(3, 2, 5, random.NextDouble() * 4.0 - 2.0, true),
            new(4, 3, 5, random.NextDouble() * 4.0 - 2.0, true),
            new(5, 4, 5, random.NextDouble() * 4.0 - 2.0, true),
        };
        return new Genome(nodes, connections);
    }
}

/// <summary>
/// Simple fitness function for GPU benchmarking: 4 inputs, 1 output, 4 test cases.
/// </summary>
internal sealed class BenchmarkFitnessFunction : IGpuFitnessFunction
{
    private static readonly float[] TestInputs =
    [
        0.5f, 0.3f, 0.7f, 0.1f,
        0.1f, 0.9f, 0.2f, 0.8f,
        0.8f, 0.2f, 0.5f, 0.5f,
        0.3f, 0.7f, 0.4f, 0.6f
    ];

    public int CaseCount => 4;

    public int OutputCount => 1;

    public ReadOnlyMemory<float> InputCases => TestInputs;

    public double ComputeFitness(ReadOnlySpan<float> outputs)
    {
        double sum = 0;
        for (int i = 0; i < outputs.Length; i++)
        {
            sum += outputs[i];
        }

        return sum / outputs.Length;
    }
}
