using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeatSharp.Evaluation;
using NeatSharp.Extensions;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Detection;
using NeatSharp.Gpu.Evaluation;
using NeatSharp.Gpu.Extensions;

namespace NeatSharp.Benchmarks;

/// <summary>
/// Hybrid evaluator benchmarks comparing partition policies (Static, Adaptive, CostBased)
/// across various population sizes. Uses DI pipeline via
/// <c>AddNeatSharp</c> + <c>AddNeatSharpGpu</c> + <c>AddNeatSharpHybrid</c>.
/// Requires a CUDA GPU; gracefully skips when none is detected.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class HybridEvaluatorBenchmarks
{
    [Params(150, 500, 1000, 5000)]
    public int PopulationSize { get; set; }

    [Params(SplitPolicyType.Static, SplitPolicyType.Adaptive, SplitPolicyType.CostBased)]
    public SplitPolicyType Policy { get; set; }

    private IReadOnlyList<IGenome> _genomes = null!;
    private ServiceProvider? _serviceProvider;
    private IServiceScope? _scope;
    private IBatchEvaluator? _evaluator;
    private bool _skipGpu;

    [GlobalSetup]
    public void Setup()
    {
        // Detect GPU availability
        var detector = new GpuDeviceDetector(Options.Create(new GpuOptions()));
        var deviceInfo = detector.Detect();
        _skipGpu = deviceInfo is null || !deviceInfo.IsCompatible;

        // Build GPU-compatible population
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
            var services = new ServiceCollection();
            services.AddNeatSharp(opts =>
            {
                opts.InputCount = 4;
                opts.OutputCount = 1;
                opts.PopulationSize = 150;
                opts.Stopping.MaxGenerations = 1;
            });

            var fitnessFunction = new BenchmarkFitnessFunction();
            services.AddSingleton<IGpuFitnessFunction>(fitnessFunction);
            services.AddSingleton<IEvaluationStrategy>(CreateCpuEvaluationStrategy(fitnessFunction));
            services.AddNeatSharpGpu();
            services.AddNeatSharpHybrid(hybrid =>
            {
                hybrid.SplitPolicy = Policy;
                hybrid.MinPopulationForSplit = 2;
                hybrid.StaticGpuFraction = 0.7;
            });
            services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

            _serviceProvider = services.BuildServiceProvider();
            _scope = _serviceProvider.CreateScope();
            _evaluator = _scope.ServiceProvider.GetRequiredService<IBatchEvaluator>();
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
        (_evaluator as IDisposable)?.Dispose();
        _scope?.Dispose();
        _serviceProvider?.Dispose();
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

    /// <summary>
    /// Creates an <see cref="IEvaluationStrategy"/> that computes the same fitness
    /// as the given <see cref="IGpuFitnessFunction"/> using CPU-based genome activation.
    /// </summary>
    private static IEvaluationStrategy CreateCpuEvaluationStrategy(IGpuFitnessFunction gpuFitness)
    {
        var inputCases = gpuFitness.InputCases.ToArray();
        int caseCount = gpuFitness.CaseCount;
        int outputCount = gpuFitness.OutputCount;
        int inputCount = inputCases.Length / caseCount;

        return EvaluationStrategy.FromFunction(genome =>
        {
            var outputs = new float[caseCount * outputCount];
            Span<double> doubleInputs = stackalloc double[inputCount];
            Span<double> doubleOutputs = stackalloc double[outputCount];

            for (int c = 0; c < caseCount; c++)
            {
                for (int j = 0; j < inputCount; j++)
                {
                    doubleInputs[j] = inputCases[c * inputCount + j];
                }

                genome.Activate(doubleInputs, doubleOutputs);

                for (int o = 0; o < outputCount; o++)
                {
                    outputs[c * outputCount + o] = (float)doubleOutputs[o];
                }
            }

            return gpuFitness.ComputeFitness(outputs);
        });
    }
}
