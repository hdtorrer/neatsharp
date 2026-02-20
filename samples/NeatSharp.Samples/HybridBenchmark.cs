using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeatSharp.Evaluation;
using NeatSharp.Extensions;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Detection;
using NeatSharp.Gpu.Evaluation;
using NeatSharp.Gpu.Extensions;
using NeatSharp.Gpu.Scheduling;

namespace NeatSharp.Samples;

/// <summary>
/// Benchmark suite for comparing CPU-only, GPU-only, and hybrid CPU+GPU evaluation
/// throughput across various population sizes, workload profiles, and partition policies.
/// </summary>
/// <remarks>
/// <para>
/// The hybrid evaluator is <c>internal</c> in <c>NeatSharp.Gpu</c>, so this benchmark
/// uses the DI pipeline (<c>AddNeatSharpHybrid()</c>) to construct it. CPU-only and
/// GPU-only baselines are measured directly via <see cref="GpuBatchEvaluator"/>.
/// </para>
/// <para>
/// <strong>GPU hardware required for real performance data.</strong>
/// Without a CUDA GPU, all paths use ILGPU CPU accelerator and speedup values
/// reflect pipeline overhead rather than true GPU performance.
/// </para>
/// </remarks>
public static class HybridBenchmark
{
    private const int DefaultWarmupIterations = 2;
    private const int DefaultTimedIterations = 5;
    private const int AdaptiveWarmupIterations = 10;
    private static readonly int[] DefaultPopulationSizes = [200, 1000, 5000];

    /// <summary>
    /// Runs the hybrid benchmark comparing CPU-only, GPU-only, and hybrid evaluation
    /// across population sizes for all three partition policies.
    /// </summary>
    public static List<HybridBenchmarkResult> RunBenchmark(
        int[]? populationSizes = null,
        IGpuFitnessFunction? fitnessFunction = null,
        int warmupIterations = DefaultWarmupIterations,
        int timedIterations = DefaultTimedIterations)
    {
        populationSizes ??= DefaultPopulationSizes;
        fitnessFunction ??= new MultiInputFitnessFunction();
        var results = new List<HybridBenchmarkResult>();

        Console.WriteLine("=== NeatSharp Hybrid Evaluation Benchmark ===");
        Console.WriteLine();
        Console.WriteLine($"Problem: Multi-input function approximation (4 inputs, 1 output, {fitnessFunction.CaseCount} test cases)");
        Console.WriteLine($"Warmup: {warmupIterations} iterations (adaptive: {AdaptiveWarmupIterations}), Timed: {timedIterations} iterations");
        Console.WriteLine();

        SplitPolicyType[] policies = [SplitPolicyType.Static, SplitPolicyType.Adaptive, SplitPolicyType.CostBased];

        Console.WriteLine($"{"Policy",-12} {"PopSize",-10} {"CPU (g/s)",14} {"GPU (g/s)",14} {"Hybrid (g/s)",14} {"vs CPU",8} {"vs GPU",8}");
        Console.WriteLine(new string('-', 82));

        foreach (int popSize in populationSizes)
        {
            var population = GpuBenchmark.BuildPopulation(popSize);

            // Measure baselines once per population size
            double cpuThroughput = MeasureSingleBackendThroughput(
                population, fitnessFunction, enableGpu: false,
                warmupIterations, timedIterations);

            double gpuThroughput = MeasureSingleBackendThroughput(
                population, fitnessFunction, enableGpu: true,
                warmupIterations, timedIterations);

            foreach (var policy in policies)
            {
                int warmup = policy == SplitPolicyType.Adaptive
                    ? AdaptiveWarmupIterations
                    : warmupIterations;

                var (hybridThroughput, _) = MeasureHybridThroughput(
                    population, fitnessFunction, policy,
                    warmup, timedIterations);

                double vsCpu = cpuThroughput > 0 ? hybridThroughput / cpuThroughput : 0;
                double vsGpu = gpuThroughput > 0 ? hybridThroughput / gpuThroughput : 0;

                var result = new HybridBenchmarkResult(
                    popSize, policy, cpuThroughput, gpuThroughput,
                    hybridThroughput, vsCpu, vsGpu);
                results.Add(result);

                Console.WriteLine(
                    $"{policy,-12} {popSize,-10} {cpuThroughput,14:N0} {gpuThroughput,14:N0} {hybridThroughput,14:N0} {vsCpu,7:F2}x {vsGpu,7:F2}x");
            }
        }

        Console.WriteLine();
        Console.WriteLine("NOTE: Without a real CUDA GPU, all paths use ILGPU CPU accelerator.");

        return results;
    }

    /// <summary>
    /// Runs the full scaling benchmark with two workload profiles:
    /// transfer-dominated (10 test cases) and compute-dominated (100 test cases).
    /// </summary>
    public static (List<HybridBenchmarkResult> TransferResults, List<HybridBenchmarkResult> ComputeResults)
        RunScalingBenchmark(
            int[]? populationSizes = null,
            int warmupIterations = DefaultWarmupIterations,
            int timedIterations = DefaultTimedIterations)
    {
        populationSizes ??= DefaultPopulationSizes;

        Console.WriteLine("=== Transfer-Dominated Workload (10 test cases) ===");
        Console.WriteLine();
        var transferResults = RunBenchmark(
            populationSizes,
            new MultiInputFitnessFunction(),
            warmupIterations, timedIterations);

        Console.WriteLine();
        Console.WriteLine("=== Compute-Dominated Workload (100 test cases) ===");
        Console.WriteLine();
        var computeResults = RunBenchmark(
            populationSizes,
            new ParametricFitnessFunction(caseCount: 100, inputCount: 4),
            warmupIterations, timedIterations);

        return (transferResults, computeResults);
    }

    /// <summary>
    /// Runs the bimodal population comparison (SC-006): cost-based vs uniform (static)
    /// partitioning on a population with 50% simple and 50% complex genomes.
    /// </summary>
    public static List<BimodalComparisonResult> RunBimodalComparison(
        int[]? populationSizes = null,
        int warmupIterations = DefaultWarmupIterations,
        int timedIterations = DefaultTimedIterations)
    {
        populationSizes ??= DefaultPopulationSizes;
        var fitnessFunction = new MultiInputFitnessFunction();
        var results = new List<BimodalComparisonResult>();

        Console.WriteLine("=== Bimodal Population Comparison (SC-006) ===");
        Console.WriteLine();
        Console.WriteLine("Population: 50% simple (<10 nodes) + 50% complex (>100 nodes)");
        Console.WriteLine();

        Console.WriteLine($"{"PopSize",-10} {"Uniform (g/s)",14} {"CostBased (g/s)",16} {"Improvement",12}");
        Console.WriteLine(new string('-', 54));

        foreach (int popSize in populationSizes)
        {
            var population = BuildBimodalPopulation(popSize);

            var (uniformThroughput, _) = MeasureHybridThroughput(
                population, fitnessFunction, SplitPolicyType.Static,
                warmupIterations, timedIterations);

            var (costBasedThroughput, _) = MeasureHybridThroughput(
                population, fitnessFunction, SplitPolicyType.CostBased,
                warmupIterations, timedIterations);

            double improvement = uniformThroughput > 0
                ? (costBasedThroughput - uniformThroughput) / uniformThroughput * 100
                : 0;

            var result = new BimodalComparisonResult(
                popSize, uniformThroughput, costBasedThroughput, improvement);
            results.Add(result);

            Console.WriteLine(
                $"{popSize,-10} {uniformThroughput,14:N0} {costBasedThroughput,16:N0} {improvement,10:F1}%");
        }

        Console.WriteLine();
        Console.WriteLine("Target: Cost-based should achieve >= 10% improvement on bimodal populations (SC-006).");

        return results;
    }

    /// <summary>
    /// Measures hybrid evaluation throughput using the DI pipeline with the specified policy.
    /// </summary>
    public static (double Throughput, List<SchedulingMetrics> Metrics) MeasureHybridThroughput(
        List<IGenome> population,
        IGpuFitnessFunction fitnessFunction,
        SplitPolicyType policy,
        int warmup,
        int timed)
    {
        var services = new ServiceCollection();
        services.AddNeatSharp(opts =>
        {
            opts.InputCount = 4;
            opts.OutputCount = 1;
            opts.PopulationSize = 150;
            opts.Stopping.MaxGenerations = 1;
        });

        services.AddSingleton<IGpuFitnessFunction>(fitnessFunction);
        services.AddSingleton<IEvaluationStrategy>(CreateCpuFitnessFunction(fitnessFunction));
        services.AddNeatSharpGpu();

        var collector = new BenchmarkMetricsCollector();
        services.AddSingleton<ISchedulingMetricsReporter>(collector);

        services.AddNeatSharpHybrid(hybrid =>
        {
            hybrid.SplitPolicy = policy;
            hybrid.MinPopulationForSplit = 2;
            hybrid.StaticGpuFraction = 0.7;
        });
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var evaluator = scope.ServiceProvider.GetRequiredService<IBatchEvaluator>();

        // Warmup
        for (int i = 0; i < warmup; i++)
        {
            evaluator.EvaluateAsync(
                population, (_, _) => { }, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        // Timed runs
        collector.History.Clear();
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < timed; i++)
        {
            evaluator.EvaluateAsync(
                population, (_, _) => { }, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        sw.Stop();

        // Dispose the evaluator if it implements IDisposable
        (evaluator as IDisposable)?.Dispose();

        double totalGenomes = (double)population.Count * timed;
        double seconds = sw.Elapsed.TotalSeconds;
        double throughput = seconds > 0 ? totalGenomes / seconds : 0;

        return (throughput, collector.History.ToList());
    }

    /// <summary>
    /// Measures throughput for a single backend (CPU-only or GPU-only) via direct
    /// <see cref="GpuBatchEvaluator"/> construction.
    /// </summary>
    private static double MeasureSingleBackendThroughput(
        List<IGenome> population,
        IGpuFitnessFunction fitnessFunction,
        bool enableGpu,
        int warmup,
        int timed)
    {
        var options = new GpuOptions { EnableGpu = enableGpu };

        using var evaluator = new GpuBatchEvaluator(
            new BenchmarkDeviceDetector(),
            fitnessFunction,
            Options.Create(options),
            NullLogger<GpuBatchEvaluator>.Instance);

        // Warmup
        for (int i = 0; i < warmup; i++)
        {
            evaluator.EvaluateAsync(
                population, (_, _) => { }, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        // Timed runs
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < timed; i++)
        {
            evaluator.EvaluateAsync(
                population, (_, _) => { }, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
        sw.Stop();

        double totalGenomes = (double)population.Count * timed;
        double seconds = sw.Elapsed.TotalSeconds;
        return seconds > 0 ? totalGenomes / seconds : 0;
    }

    /// <summary>
    /// Builds a bimodal population: 50% simple genomes (&lt;10 nodes) and
    /// 50% complex genomes (&gt;100 nodes) for cost-based partitioning comparison.
    /// </summary>
    public static List<IGenome> BuildBimodalPopulation(int populationSize)
    {
        var registry = new ActivationFunctionRegistry();
        var cpuBuilder = new FeedForwardNetworkBuilder(registry);
        var gpuBuilder = new GpuNetworkBuilder(cpuBuilder);

        var population = new List<IGenome>(populationSize);
        var random = new Random(42);

        int halfSize = populationSize / 2;

        // First half: simple genomes (<10 nodes — 4 inputs + 1 bias + 1 output = 6 nodes)
        for (int i = 0; i < halfSize; i++)
        {
            var genome = CreateSimpleGenome(random);
            population.Add(gpuBuilder.Build(genome));
        }

        // Second half: complex genomes (>100 nodes — 4 inputs + 1 bias + 1 output + 100 hidden = 106 nodes)
        for (int i = halfSize; i < populationSize; i++)
        {
            var genome = GpuBenchmark.CreateLargeGenome(random, hiddenLayers: 1, nodesPerLayer: 100);
            population.Add(gpuBuilder.Build(genome));
        }

        return population;
    }

    /// <summary>
    /// Creates an <see cref="IEvaluationStrategy"/> that computes the same fitness
    /// as the given <see cref="IGpuFitnessFunction"/> using CPU-based genome activation.
    /// </summary>
    private static IEvaluationStrategy CreateCpuFitnessFunction(IGpuFitnessFunction gpuFitness)
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
                // Convert float inputs to double
                for (int j = 0; j < inputCount; j++)
                {
                    doubleInputs[j] = inputCases[c * inputCount + j];
                }

                genome.Activate(doubleInputs, doubleOutputs);

                // Convert double outputs to float
                for (int o = 0; o < outputCount; o++)
                {
                    outputs[c * outputCount + o] = (float)doubleOutputs[o];
                }
            }

            return gpuFitness.ComputeFitness(outputs);
        });
    }

    /// <summary>
    /// Generates a markdown report from benchmark results.
    /// </summary>
    public static string GenerateMarkdownReport(
        List<HybridBenchmarkResult> transferResults,
        List<HybridBenchmarkResult> computeResults,
        List<BimodalComparisonResult> bimodalResults,
        IGpuDeviceInfo? deviceInfo = null,
        int warmupIterations = DefaultWarmupIterations,
        int timedIterations = DefaultTimedIterations)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# NeatSharp Hybrid Evaluation Benchmark Report");
        sb.AppendLine();
        sb.AppendLine($"**Date**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Hardware configuration
        sb.AppendLine("## Hardware Configuration");
        sb.AppendLine();
        if (deviceInfo is not null)
        {
            sb.AppendLine($"- **GPU**: {deviceInfo.DeviceName}");
            sb.AppendLine($"- **Compute Capability**: {deviceInfo.ComputeCapability}");
            sb.AppendLine($"- **GPU Memory**: {deviceInfo.MemoryBytes / (1024.0 * 1024.0 * 1024.0):F1} GB");
        }
        else
        {
            sb.AppendLine("- **GPU**: ILGPU CPU Accelerator (no CUDA GPU detected)");
        }
        sb.AppendLine($"- **Runtime**: .NET {Environment.Version}");
        sb.AppendLine($"- **OS**: {Environment.OSVersion}");
        sb.AppendLine($"- **Processor Count**: {Environment.ProcessorCount}");
        sb.AppendLine();

        // Methodology
        sb.AppendLine("## Methodology");
        sb.AppendLine();
        sb.AppendLine("- **Problem**: Multi-input function approximation (4 inputs, 1 output)");
        sb.AppendLine("- **Workload profiles**:");
        sb.AppendLine("  - Transfer-dominated: 10 test cases (favors CPU, GPU transfer overhead dominates)");
        sb.AppendLine("  - Compute-dominated: 100 test cases (favors GPU, amortizes transfer cost)");
        sb.AppendLine("- **Population composition**: 60% simple, 30% medium, 10% complex (per GpuBenchmark.BuildPopulation)");
        sb.AppendLine("- **Population sizes**: 200, 1,000, 5,000");
        sb.AppendLine("- **Split policies tested**: Static (70/30), Adaptive (PID), Cost-Based (complexity-driven)");
        sb.AppendLine($"- **Warmup iterations**: {warmupIterations} (adaptive: {AdaptiveWarmupIterations})");
        sb.AppendLine($"- **Timed iterations**: {timedIterations}");
        sb.AppendLine("- **CPU path**: `IGenome.Activate()` per genome via `IEvaluationStrategy`");
        sb.AppendLine("- **GPU path**: Batch forward propagation via ILGPU kernel");
        sb.AppendLine("- **Hybrid path**: DI-based `HybridBatchEvaluator` with concurrent CPU+GPU dispatch");
        sb.AppendLine("- **Metric**: Throughput in genomes evaluated per second (higher is better)");
        sb.AppendLine();

        // Transfer-dominated results
        AppendResultsTable(sb, "Transfer-Dominated Workload Results (10 test cases)", transferResults);

        // Compute-dominated results
        AppendResultsTable(sb, "Compute-Dominated Workload Results (100 test cases)", computeResults);

        // Bimodal comparison
        sb.AppendLine("## Bimodal Population Comparison (SC-006)");
        sb.AppendLine();
        sb.AppendLine("Population: 50% simple (<10 nodes) + 50% complex (>100 nodes)");
        sb.AppendLine();
        sb.AppendLine("| Population Size | Uniform/Static (g/s) | Cost-Based (g/s) | Improvement |");
        sb.AppendLine("|----------------:|---------------------:|-----------------:|------------:|");
        foreach (var r in bimodalResults)
        {
            sb.AppendLine(
                $"| {r.PopulationSize,15:N0} | {r.UniformGenomesPerSecond,20:N0} | {r.CostBasedGenomesPerSecond,16:N0} | {r.ImprovementPercent,10:F1}% |");
        }
        sb.AppendLine();
        sb.AppendLine("> **SC-006 Target**: Cost-based partitioning should achieve >= 10% higher throughput");
        sb.AppendLine("> than uniform partitioning on bimodal-complexity populations.");
        sb.AppendLine();

        // Analysis
        sb.AppendLine("## Analysis");
        sb.AppendLine();
        AppendAnalysis(sb, transferResults, computeResults, bimodalResults);

        // Notes
        sb.AppendLine("## Notes");
        sb.AppendLine();
        sb.AppendLine("- GPU forward propagation uses fp32; CPU uses fp64. Per-output tolerance is 1e-4.");
        sb.AppendLine("- Hybrid CPU backend uses `IEvaluationStrategy` (per-genome `IGenome.Activate()`).");
        sb.AppendLine("- Hybrid GPU backend uses `GpuBatchEvaluator` (ILGPU kernel batch evaluation).");
        sb.AppendLine("- Hybrid overhead includes DI resolution, partition policy computation, and `Task.WhenAll` dispatch.");
        sb.AppendLine("- `MinPopulationForSplit` is set to 2 for benchmarks to ensure hybrid mode at all sizes.");
        sb.AppendLine("- Adaptive policy uses 10 warmup iterations for PID convergence before timed runs.");
        sb.AppendLine("- The benchmark uses deterministic genome construction (seed 42) for reproducibility.");

        if (deviceInfo is null)
        {
            sb.AppendLine();
            sb.AppendLine("> **Note**: These results were collected using the ILGPU CPU accelerator (no CUDA GPU available).");
            sb.AppendLine("> Both CPU and GPU paths execute on the CPU, so speedup values reflect pipeline overhead rather than true GPU performance.");
            sb.AppendLine("> Run on hardware with a compatible NVIDIA GPU (compute capability >= 5.0) for representative hybrid speedup data.");
        }

        return sb.ToString();
    }

    private static void AppendResultsTable(
        StringBuilder sb, string title, List<HybridBenchmarkResult> results)
    {
        sb.AppendLine($"## {title}");
        sb.AppendLine();
        sb.AppendLine("| Policy | Pop Size | CPU (g/s) | GPU (g/s) | Hybrid (g/s) | vs CPU | vs GPU |");
        sb.AppendLine("|--------|--------:|---------:|---------:|------------:|-------:|-------:|");
        foreach (var r in results)
        {
            sb.AppendLine(
                $"| {r.Policy,-10} | {r.PopulationSize,7:N0} | {r.CpuGenomesPerSecond,8:N0} | {r.GpuGenomesPerSecond,8:N0} | {r.HybridGenomesPerSecond,11:N0} | {r.HybridVsCpuSpeedup,5:F2}x | {r.HybridVsGpuSpeedup,5:F2}x |");
        }
        sb.AppendLine();

        // Per-policy summary
        var grouped = results.GroupBy(r => r.Policy);
        foreach (var group in grouped)
        {
            double avgVsCpu = group.Average(r => r.HybridVsCpuSpeedup);
            double avgVsGpu = group.Average(r => r.HybridVsGpuSpeedup);
            sb.AppendLine($"- **{group.Key}**: avg {avgVsCpu:F2}x vs CPU, {avgVsGpu:F2}x vs GPU");
        }
        sb.AppendLine();
    }

    private static void AppendAnalysis(
        StringBuilder sb,
        List<HybridBenchmarkResult> transferResults,
        List<HybridBenchmarkResult> computeResults,
        List<BimodalComparisonResult> bimodalResults)
    {
        if (transferResults.Count > 0)
        {
            var bestTransfer = transferResults.OrderByDescending(r => r.HybridVsCpuSpeedup).First();
            sb.AppendLine($"- **Best transfer-dominated**: {bestTransfer.Policy} at pop {bestTransfer.PopulationSize:N0} — {bestTransfer.HybridVsCpuSpeedup:F2}x vs CPU");
        }

        if (computeResults.Count > 0)
        {
            var bestCompute = computeResults.OrderByDescending(r => r.HybridVsCpuSpeedup).First();
            sb.AppendLine($"- **Best compute-dominated**: {bestCompute.Policy} at pop {bestCompute.PopulationSize:N0} — {bestCompute.HybridVsCpuSpeedup:F2}x vs CPU");
        }

        if (bimodalResults.Count > 0)
        {
            double avgImprovement = bimodalResults.Average(r => r.ImprovementPercent);
            bool meetsTarget = bimodalResults.All(r => r.ImprovementPercent >= 10.0);
            sb.AppendLine($"- **Bimodal cost-based improvement**: avg {avgImprovement:F1}%, target met: {(meetsTarget ? "YES" : "NO")}");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Simple genome: 4 inputs + bias -> 1 output (no hidden nodes).
    /// Used for the simple half of bimodal populations.
    /// </summary>
    private static Genome CreateSimpleGenome(Random random)
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Input),
            new(3, NodeType.Input),
            new(4, NodeType.Bias),
            new(5, NodeType.Output),
        };

        var connections = new ConnectionGene[]
        {
            new(1, 0, 5, RandomWeight(random), true),
            new(2, 1, 5, RandomWeight(random), true),
            new(3, 2, 5, RandomWeight(random), true),
            new(4, 3, 5, RandomWeight(random), true),
            new(5, 4, 5, RandomWeight(random), true),
        };

        return new Genome(nodes, connections);
    }

    private static double RandomWeight(Random random) =>
        random.NextDouble() * 4.0 - 2.0;

    /// <summary>
    /// Stub detector that returns a compatible device (forces GPU path via ILGPU CPU accelerator).
    /// </summary>
    private sealed class BenchmarkDeviceDetector : IGpuDeviceDetector
    {
        public IGpuDeviceInfo? Detect() =>
            new GpuDeviceInfo("Benchmark CPU Accelerator", new Version(8, 0), 1024L * 1024 * 1024, true, null);
    }

    /// <summary>
    /// Collects scheduling metrics emitted by the hybrid evaluator during benchmark runs.
    /// </summary>
    private sealed class BenchmarkMetricsCollector : ISchedulingMetricsReporter
    {
        public List<SchedulingMetrics> History { get; } = [];

        public void Report(SchedulingMetrics metrics) => History.Add(metrics);
    }
}

/// <summary>
/// Structured result for a hybrid benchmark data point.
/// </summary>
public record HybridBenchmarkResult(
    int PopulationSize,
    SplitPolicyType Policy,
    double CpuGenomesPerSecond,
    double GpuGenomesPerSecond,
    double HybridGenomesPerSecond,
    double HybridVsCpuSpeedup,
    double HybridVsGpuSpeedup);

/// <summary>
/// Structured result for a bimodal population comparison data point.
/// </summary>
public record BimodalComparisonResult(
    int PopulationSize,
    double UniformGenomesPerSecond,
    double CostBasedGenomesPerSecond,
    double ImprovementPercent);
