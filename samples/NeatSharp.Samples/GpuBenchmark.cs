using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Detection;
using NeatSharp.Gpu.Evaluation;

namespace NeatSharp.Samples;

/// <summary>
/// Manual benchmark suite for comparing CPU vs GPU evaluation throughput
/// across various population sizes. Run via <see cref="RunBenchmark"/>.
/// </summary>
/// <remarks>
/// <para>
/// This benchmark defines a multi-input function approximation problem (4 inputs, 1 output,
/// 10 test cases) and measures throughput (genomes/second) on both CPU and GPU paths.
/// </para>
/// <para>
/// <strong>GPU hardware required for real performance data.</strong>
/// When run on a machine without a CUDA GPU, both paths use ILGPU CPU accelerator,
/// so the benchmark validates the pipeline but does not measure true GPU speedup.
/// Real GPU benchmarks should be run on hardware with a compatible NVIDIA GPU
/// (compute capability >= 5.0) and results documented separately.
/// </para>
/// <para>
/// The benchmark report generation (T031) requires actual CUDA GPU hardware.
/// This file is marked as a placeholder for T031 since real performance data
/// cannot be collected in CI environments without GPU hardware.
/// The benchmark validation test is gated with [Trait("Category", "GPU")].
/// </para>
/// </remarks>
public static class GpuBenchmark
{
    /// <summary>
    /// Default population sizes to benchmark.
    /// </summary>
    private static readonly int[] DefaultPopulationSizes = [150, 500, 1000, 2000, 5000];

    /// <summary>
    /// Number of warmup evaluations before timing.
    /// </summary>
    private const int DefaultWarmupIterations = 2;

    /// <summary>
    /// Number of timed evaluation iterations per population size.
    /// </summary>
    private const int DefaultTimedIterations = 5;

    /// <summary>
    /// Runs the full benchmark suite and returns structured results.
    /// </summary>
    /// <param name="populationSizes">
    /// Optional override for population sizes to benchmark.
    /// If null, uses the default sizes: 150, 500, 1000, 2000, 5000.
    /// </param>
    /// <param name="warmupIterations">Number of warmup iterations. Default: 2.</param>
    /// <param name="timedIterations">Number of timed iterations. Default: 5.</param>
    /// <returns>A list of benchmark results, one per population size.</returns>
    public static List<BenchmarkResult> RunBenchmark(
        int[]? populationSizes = null,
        int warmupIterations = DefaultWarmupIterations,
        int timedIterations = DefaultTimedIterations)
    {
        populationSizes ??= DefaultPopulationSizes;
        var fitnessFunction = new MultiInputFitnessFunction();
        var results = new List<BenchmarkResult>();

        Console.WriteLine("=== NeatSharp GPU Benchmark ===");
        Console.WriteLine();
        Console.WriteLine($"Problem: Multi-input function approximation (4 inputs, 1 output, {fitnessFunction.CaseCount} test cases)");
        Console.WriteLine($"Warmup: {warmupIterations} iterations, Timed: {timedIterations} iterations");
        Console.WriteLine();

        // Header
        Console.WriteLine($"{"PopSize",-10} {"CPU (g/s)",14} {"GPU (g/s)",14} {"Speedup",10}");
        Console.WriteLine(new string('-', 50));

        foreach (int popSize in populationSizes)
        {
            var population = BuildPopulation(popSize);

            // --- CPU path (GPU disabled, uses CPU fallback) ---
            double cpuThroughput = MeasureThroughput(
                population, fitnessFunction, enableGpu: false,
                warmupIterations, timedIterations);

            // --- GPU path (uses ILGPU CPU accelerator if no real GPU) ---
            double gpuThroughput = MeasureThroughput(
                population, fitnessFunction, enableGpu: true,
                warmupIterations, timedIterations);

            double speedup = cpuThroughput > 0 ? gpuThroughput / cpuThroughput : 0;

            var result = new BenchmarkResult(
                popSize, cpuThroughput, gpuThroughput, speedup);
            results.Add(result);

            Console.WriteLine($"{popSize,-10} {cpuThroughput,14:N0} {gpuThroughput,14:N0} {speedup,10:F2}x");
        }

        Console.WriteLine();
        Console.WriteLine("NOTE: Without a real CUDA GPU, both paths use ILGPU CPU accelerator.");
        Console.WriteLine("      Real speedup data requires NVIDIA GPU hardware.");

        return results;
    }

    /// <summary>
    /// Generates a markdown report from benchmark results suitable for repository inclusion.
    /// </summary>
    /// <param name="results">Benchmark results from <see cref="RunBenchmark"/>.</param>
    /// <param name="deviceInfo">GPU device info from detection. If null, reports ILGPU CPU accelerator.</param>
    /// <param name="warmupIterations">Number of warmup iterations used.</param>
    /// <param name="timedIterations">Number of timed iterations used.</param>
    /// <returns>A markdown-formatted benchmark report string.</returns>
    public static string GenerateMarkdownReport(
        List<BenchmarkResult> results,
        IGpuDeviceInfo? deviceInfo = null,
        int warmupIterations = DefaultWarmupIterations,
        int timedIterations = DefaultTimedIterations)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# NeatSharp GPU Benchmark Report");
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
        sb.AppendLine("- **Problem**: Multi-input function approximation (4 inputs, 1 output, 10 test cases)");
        sb.AppendLine("- **Population composition**: 60% simple (direct input-output), 30% medium (1 hidden node), 10% complex (2 hidden nodes)");
        sb.AppendLine($"- **Warmup iterations**: {warmupIterations}");
        sb.AppendLine($"- **Timed iterations**: {timedIterations}");
        sb.AppendLine("- **CPU path**: GPU disabled via `GpuOptions.EnableGpu = false`, uses `IGenome.Activate()` per genome");
        sb.AppendLine("- **GPU path**: GPU enabled, batch forward propagation via ILGPU kernel (1 thread per genome)");
        sb.AppendLine("- **Metric**: Throughput in genomes evaluated per second (higher is better)");
        sb.AppendLine();

        // Results table
        sb.AppendLine("## Results");
        sb.AppendLine();
        sb.AppendLine("| Population Size | CPU (genomes/s) | GPU (genomes/s) | Speedup |");
        sb.AppendLine("|----------------:|----------------:|----------------:|--------:|");
        foreach (var r in results)
        {
            sb.AppendLine($"| {r.PopulationSize,15:N0} | {r.CpuGenomesPerSecond,15:N0} | {r.GpuGenomesPerSecond,15:N0} | {r.Speedup,6:F2}x |");
        }
        sb.AppendLine();

        // Speedup analysis
        sb.AppendLine("## Speedup Analysis");
        sb.AppendLine();
        if (results.Count > 0)
        {
            double minSpeedup = results.Min(r => r.Speedup);
            double maxSpeedup = results.Max(r => r.Speedup);
            double avgSpeedup = results.Average(r => r.Speedup);
            var bestResult = results.OrderByDescending(r => r.Speedup).First();

            sb.AppendLine($"- **Minimum speedup**: {minSpeedup:F2}x");
            sb.AppendLine($"- **Maximum speedup**: {maxSpeedup:F2}x (at population size {bestResult.PopulationSize:N0})");
            sb.AppendLine($"- **Average speedup**: {avgSpeedup:F2}x");
            sb.AppendLine();

            if (deviceInfo is null)
            {
                sb.AppendLine("> **Note**: These results were collected using the ILGPU CPU accelerator (no CUDA GPU available).");
                sb.AppendLine("> Both CPU and GPU paths execute on the CPU, so speedup values reflect pipeline overhead rather than true GPU performance.");
                sb.AppendLine("> Run on hardware with a compatible NVIDIA GPU (compute capability >= 5.0) for representative GPU speedup data.");
            }
            else if (maxSpeedup >= 5.0)
            {
                sb.AppendLine($"> GPU evaluation achieves >= 5x throughput improvement for population sizes >= {bestResult.PopulationSize:N0}, meeting the performance target.");
            }
        }
        sb.AppendLine();

        // Notes
        sb.AppendLine("## Notes");
        sb.AppendLine();
        sb.AppendLine("- GPU forward propagation uses fp32; CPU uses fp64. Per-output tolerance is 1e-4.");
        sb.AppendLine("- Fitness computation (`IGpuFitnessFunction.ComputeFitness`) runs on CPU after GPU outputs are downloaded.");
        sb.AppendLine("- Genome topology extraction and `GpuPopulationData` construction are included in GPU path timing.");
        sb.AppendLine("- The benchmark uses deterministic genome construction (seed 42) for reproducibility.");

        return sb.ToString();
    }

    /// <summary>
    /// Builds a population of GpuFeedForwardNetwork genomes with varying complexity.
    /// Uses deterministic genome construction to create a realistic benchmark population.
    /// </summary>
    /// <param name="populationSize">Number of genomes to create.</param>
    /// <returns>A list of GpuFeedForwardNetwork genomes.</returns>
    public static List<IGenome> BuildPopulation(int populationSize)
    {
        var registry = new ActivationFunctionRegistry();
        var cpuBuilder = new FeedForwardNetworkBuilder(registry);
        var gpuBuilder = new GpuNetworkBuilder(cpuBuilder);

        var population = new List<IGenome>(populationSize);
        var random = new Random(42);

        for (int i = 0; i < populationSize; i++)
        {
            var genome = CreateRandomGenome(random, i);
            population.Add(gpuBuilder.Build(genome));
        }

        return population;
    }

    /// <summary>
    /// Creates a genome with varying complexity based on the index.
    /// Roughly 60% simple (no hidden), 30% medium (1 hidden), 10% complex (2 hidden).
    /// </summary>
    private static Genome CreateRandomGenome(Random random, int index)
    {
        int complexity = index % 10;

        if (complexity < 6)
        {
            return CreateSimpleGenome(random);
        }
        else if (complexity < 9)
        {
            return CreateMediumGenome(random);
        }
        else
        {
            return CreateComplexGenome(random);
        }
    }

    /// <summary>
    /// Simple genome: 4 inputs + bias -> 1 output (no hidden nodes).
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

    /// <summary>
    /// Medium genome: 4 inputs + bias -> 1 hidden -> 1 output.
    /// </summary>
    private static Genome CreateMediumGenome(Random random)
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Input),
            new(3, NodeType.Input),
            new(4, NodeType.Bias),
            new(5, NodeType.Output),
            new(6, NodeType.Hidden),
        };

        var connections = new ConnectionGene[]
        {
            new(1, 0, 6, RandomWeight(random), true),
            new(2, 1, 6, RandomWeight(random), true),
            new(3, 2, 6, RandomWeight(random), true),
            new(4, 3, 6, RandomWeight(random), true),
            new(5, 4, 6, RandomWeight(random), true),
            new(6, 6, 5, RandomWeight(random), true),
            new(7, 4, 5, RandomWeight(random), true),
        };

        return new Genome(nodes, connections);
    }

    /// <summary>
    /// Complex genome: 4 inputs + bias -> 2 hidden nodes -> 1 output.
    /// </summary>
    private static Genome CreateComplexGenome(Random random)
    {
        var nodes = new NodeGene[]
        {
            new(0, NodeType.Input),
            new(1, NodeType.Input),
            new(2, NodeType.Input),
            new(3, NodeType.Input),
            new(4, NodeType.Bias),
            new(5, NodeType.Output),
            new(6, NodeType.Hidden),
            new(7, NodeType.Hidden),
        };

        var connections = new ConnectionGene[]
        {
            new(1, 0, 6, RandomWeight(random), true),
            new(2, 1, 6, RandomWeight(random), true),
            new(3, 2, 7, RandomWeight(random), true),
            new(4, 3, 7, RandomWeight(random), true),
            new(5, 4, 6, RandomWeight(random), true),
            new(6, 4, 7, RandomWeight(random), true),
            new(7, 6, 5, RandomWeight(random), true),
            new(8, 7, 5, RandomWeight(random), true),
            new(9, 4, 5, RandomWeight(random), true),
        };

        return new Genome(nodes, connections);
    }

    /// <summary>
    /// Creates a large genome with the specified number of hidden layers,
    /// each with the specified number of nodes per layer. Fully connected between layers.
    /// Representative of evolved NEAT networks.
    /// </summary>
    /// <param name="random">Random number generator for weights.</param>
    /// <param name="hiddenLayers">Number of hidden layers.</param>
    /// <param name="nodesPerLayer">Number of nodes per hidden layer.</param>
    /// <returns>A genome with the specified topology.</returns>
    public static Genome CreateLargeGenome(Random random, int hiddenLayers, int nodesPerLayer)
    {
        const int inputCount = 4;
        int nodeId = 0;
        int innovationId = 1;

        var nodes = new List<NodeGene>();
        var connections = new List<ConnectionGene>();

        // Input nodes
        int[] inputIds = new int[inputCount];
        for (int i = 0; i < inputCount; i++)
        {
            inputIds[i] = nodeId;
            nodes.Add(new NodeGene(nodeId++, NodeType.Input));
        }

        // Bias node
        int biasId = nodeId;
        nodes.Add(new NodeGene(nodeId++, NodeType.Bias));

        // Output node
        int outputId = nodeId;
        nodes.Add(new NodeGene(nodeId++, NodeType.Output));

        // Hidden layers
        int[] prevLayerIds = [..inputIds, biasId];
        for (int layer = 0; layer < hiddenLayers; layer++)
        {
            int[] layerIds = new int[nodesPerLayer];
            for (int n = 0; n < nodesPerLayer; n++)
            {
                layerIds[n] = nodeId;
                nodes.Add(new NodeGene(nodeId++, NodeType.Hidden));

                // Connect from all nodes in previous layer
                foreach (int srcId in prevLayerIds)
                {
                    connections.Add(new ConnectionGene(innovationId++, srcId, layerIds[n], RandomWeight(random), true));
                }
            }
            prevLayerIds = layerIds;
        }

        // Connect last hidden layer (or inputs if no hidden) to output
        foreach (int srcId in prevLayerIds)
        {
            connections.Add(new ConnectionGene(innovationId++, srcId, outputId, RandomWeight(random), true));
        }
        // Bias to output
        connections.Add(new ConnectionGene(innovationId++, biasId, outputId, RandomWeight(random), true));

        return new Genome(nodes.ToArray(), connections.ToArray());
    }

    /// <summary>
    /// Runs a scaling benchmark that measures GPU vs CPU throughput across
    /// both population sizes and genome complexities (number of hidden nodes).
    /// </summary>
    /// <param name="populationSizes">Population sizes to test.</param>
    /// <param name="hiddenNodeCounts">Hidden node counts to test (nodes per single layer).</param>
    /// <param name="warmupIterations">Number of warmup iterations. Default: 2.</param>
    /// <param name="timedIterations">Number of timed iterations. Default: 5.</param>
    /// <returns>A list of benchmark results with complexity annotation.</returns>
    public static List<ScalingBenchmarkResult> RunScalingBenchmark(
        int[]? populationSizes = null,
        int[]? hiddenNodeCounts = null,
        int warmupIterations = DefaultWarmupIterations,
        int timedIterations = DefaultTimedIterations,
        IGpuFitnessFunction? fitnessFunction = null)
    {
        populationSizes ??= [500, 1000, 2000, 5000];
        hiddenNodeCounts ??= [0, 5, 10, 20, 50];
        fitnessFunction ??= new MultiInputFitnessFunction();
        var results = new List<ScalingBenchmarkResult>();

        Console.WriteLine("=== NeatSharp GPU Scaling Benchmark ===");
        Console.WriteLine();
        Console.WriteLine($"Problem: Multi-input function approximation (4 inputs, 1 output, {fitnessFunction.CaseCount} test cases)");
        Console.WriteLine($"Warmup: {warmupIterations} iterations, Timed: {timedIterations} iterations");
        Console.WriteLine();

        Console.WriteLine($"{"Hidden",-8} {"PopSize",-10} {"CPU (g/s)",14} {"GPU (g/s)",14} {"Speedup",10}");
        Console.WriteLine(new string('-', 58));

        foreach (int hiddenNodes in hiddenNodeCounts)
        {
            foreach (int popSize in populationSizes)
            {
                var population = BuildPopulationWithComplexity(popSize, hiddenNodes);

                double cpuThroughput = MeasureThroughput(
                    population, fitnessFunction, enableGpu: false,
                    warmupIterations, timedIterations);

                double gpuThroughput = MeasureThroughput(
                    population, fitnessFunction, enableGpu: true,
                    warmupIterations, timedIterations);

                double speedup = cpuThroughput > 0 ? gpuThroughput / cpuThroughput : 0;

                var result = new ScalingBenchmarkResult(
                    popSize, hiddenNodes, cpuThroughput, gpuThroughput, speedup);
                results.Add(result);

                Console.WriteLine($"{hiddenNodes,-8} {popSize,-10} {cpuThroughput,14:N0} {gpuThroughput,14:N0} {speedup,10:F2}x");
            }
        }

        return results;
    }

    /// <summary>
    /// Builds a population where all genomes have the same complexity:
    /// a single hidden layer with the specified number of hidden nodes.
    /// </summary>
    public static List<IGenome> BuildPopulationWithComplexity(int populationSize, int hiddenNodes)
    {
        var registry = new ActivationFunctionRegistry();
        var cpuBuilder = new FeedForwardNetworkBuilder(registry);
        var gpuBuilder = new GpuNetworkBuilder(cpuBuilder);

        var population = new List<IGenome>(populationSize);
        var random = new Random(42);

        for (int i = 0; i < populationSize; i++)
        {
            Genome genome;
            if (hiddenNodes == 0)
            {
                genome = CreateSimpleGenome(random);
            }
            else
            {
                genome = CreateLargeGenome(random, hiddenLayers: 1, nodesPerLayer: hiddenNodes);
            }
            population.Add(gpuBuilder.Build(genome));
        }

        return population;
    }

    /// <summary>
    /// Generates a markdown report from scaling benchmark results.
    /// </summary>
    public static string GenerateScalingMarkdownReport(
        List<ScalingBenchmarkResult> results,
        IGpuDeviceInfo? deviceInfo = null,
        int warmupIterations = DefaultWarmupIterations,
        int timedIterations = DefaultTimedIterations,
        List<ScalingBenchmarkResult>? heavyResults = null,
        int heavyCaseCount = 0)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# NeatSharp GPU Scaling Benchmark Report");
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
        sb.AppendLine("- **Problem**: Multi-input function approximation (4 inputs, 1 output, 10 test cases)");
        sb.AppendLine("- **Population composition**: Uniform genome complexity per data point (all genomes same hidden node count)");
        sb.AppendLine($"- **Warmup iterations**: {warmupIterations}");
        sb.AppendLine($"- **Timed iterations**: {timedIterations}");
        sb.AppendLine("- **CPU path**: GPU disabled via `GpuOptions.EnableGpu = false`, uses `IGenome.Activate()` per genome");
        sb.AppendLine("- **GPU path**: GPU enabled, batch forward propagation via ILGPU CUDA kernel (1 thread per genome)");
        sb.AppendLine("- **Metric**: Throughput in genomes evaluated per second (higher is better)");
        sb.AppendLine();

        // Results table
        sb.AppendLine("## Results");
        sb.AppendLine();
        sb.AppendLine("| Hidden Nodes | Population Size | CPU (genomes/s) | GPU (genomes/s) | Speedup |");
        sb.AppendLine("|-------------:|----------------:|----------------:|----------------:|--------:|");
        foreach (var r in results)
        {
            sb.AppendLine($"| {r.HiddenNodes,12} | {r.PopulationSize,15:N0} | {r.CpuGenomesPerSecond,15:N0} | {r.GpuGenomesPerSecond,15:N0} | {r.Speedup,6:F2}x |");
        }
        sb.AppendLine();

        // Speedup analysis
        sb.AppendLine("## Speedup Analysis");
        sb.AppendLine();
        if (results.Count > 0)
        {
            var grouped = results.GroupBy(r => r.HiddenNodes).OrderBy(g => g.Key);
            foreach (var group in grouped)
            {
                double avgSpeedup = group.Average(r => r.Speedup);
                double maxSpeedup = group.Max(r => r.Speedup);
                var bestResult = group.OrderByDescending(r => r.Speedup).First();
                sb.AppendLine($"- **{group.Key} hidden nodes**: avg {avgSpeedup:F2}x, max {maxSpeedup:F2}x (pop {bestResult.PopulationSize:N0})");
            }
            sb.AppendLine();

            var bestOverall = results.OrderByDescending(r => r.Speedup).First();
            sb.AppendLine($"- **Best overall**: {bestOverall.Speedup:F2}x at {bestOverall.HiddenNodes} hidden nodes, population {bestOverall.PopulationSize:N0}");
        }
        sb.AppendLine();

        // Heavy workload results
        if (heavyResults is { Count: > 0 })
        {
            sb.AppendLine($"## High Test-Case Count Results ({heavyCaseCount} test cases)");
            sb.AppendLine();
            sb.AppendLine("| Hidden Nodes | Population Size | CPU (genomes/s) | GPU (genomes/s) | Speedup |");
            sb.AppendLine("|-------------:|----------------:|----------------:|----------------:|--------:|");
            foreach (var r in heavyResults)
            {
                sb.AppendLine($"| {r.HiddenNodes,12} | {r.PopulationSize,15:N0} | {r.CpuGenomesPerSecond,15:N0} | {r.GpuGenomesPerSecond,15:N0} | {r.Speedup,6:F2}x |");
            }
            sb.AppendLine();

            var heavyGrouped = heavyResults.GroupBy(r => r.HiddenNodes).OrderBy(g => g.Key);
            foreach (var group in heavyGrouped)
            {
                double avgSpeedup = group.Average(r => r.Speedup);
                double maxSpeedup = group.Max(r => r.Speedup);
                var bestResult = group.OrderByDescending(r => r.Speedup).First();
                sb.AppendLine($"- **{group.Key} hidden nodes**: avg {avgSpeedup:F2}x, max {maxSpeedup:F2}x (pop {bestResult.PopulationSize:N0})");
            }
            sb.AppendLine();
        }

        // Notes
        sb.AppendLine("## Notes");
        sb.AppendLine();
        sb.AppendLine("- GPU forward propagation uses fp32; CPU uses fp64. Per-output tolerance is 1e-4.");
        sb.AppendLine("- For small genomes (0-2 hidden nodes), CPU evaluation is faster due to GPU kernel launch and data transfer overhead.");
        sb.AppendLine("- GPU advantage increases with genome complexity (more nodes/connections per genome), larger populations, and more test cases.");
        sb.AppendLine("- In practice, NEAT populations start simple and grow complexity through evolution, so GPU speedup increases over training time.");
        sb.AppendLine("- The benchmark uses deterministic genome construction (seed 42) for reproducibility.");
        sb.AppendLine("- Fitness computation (`IGpuFitnessFunction.ComputeFitness`) runs on CPU after GPU outputs are downloaded.");
        sb.AppendLine("- Genome topology extraction and `GpuPopulationData` construction are included in GPU path timing.");

        return sb.ToString();
    }

    private static double RandomWeight(Random random) =>
        random.NextDouble() * 4.0 - 2.0; // Range [-2, 2]

    /// <summary>
    /// Measures evaluation throughput in genomes/second.
    /// </summary>
    private static double MeasureThroughput(
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
                population,
                (_, _) => { },
                CancellationToken.None).GetAwaiter().GetResult();
        }

        // Timed runs
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < timed; i++)
        {
            evaluator.EvaluateAsync(
                population,
                (_, _) => { },
                CancellationToken.None).GetAwaiter().GetResult();
        }
        sw.Stop();

        double totalGenomes = (double)population.Count * timed;
        double seconds = sw.Elapsed.TotalSeconds;
        return seconds > 0 ? totalGenomes / seconds : 0;
    }

    /// <summary>
    /// Stub detector that returns a compatible device (forces GPU path via ILGPU CPU accelerator).
    /// </summary>
    private sealed class BenchmarkDeviceDetector : IGpuDeviceDetector
    {
        public IGpuDeviceInfo? Detect() =>
            new GpuDeviceInfo("Benchmark CPU Accelerator", new Version(8, 0), 1024L * 1024 * 1024, true, null);
    }
}

/// <summary>
/// Structured benchmark result for a single population size.
/// </summary>
/// <param name="PopulationSize">Number of genomes in the population.</param>
/// <param name="CpuGenomesPerSecond">CPU evaluation throughput.</param>
/// <param name="GpuGenomesPerSecond">GPU evaluation throughput.</param>
/// <param name="Speedup">GPU throughput / CPU throughput.</param>
public record BenchmarkResult(
    int PopulationSize,
    double CpuGenomesPerSecond,
    double GpuGenomesPerSecond,
    double Speedup);

/// <summary>
/// Structured benchmark result for a scaling benchmark data point.
/// </summary>
/// <param name="PopulationSize">Number of genomes in the population.</param>
/// <param name="HiddenNodes">Number of hidden nodes per genome.</param>
/// <param name="CpuGenomesPerSecond">CPU evaluation throughput.</param>
/// <param name="GpuGenomesPerSecond">GPU evaluation throughput.</param>
/// <param name="Speedup">GPU throughput / CPU throughput.</param>
public record ScalingBenchmarkResult(
    int PopulationSize,
    int HiddenNodes,
    double CpuGenomesPerSecond,
    double GpuGenomesPerSecond,
    double Speedup);

/// <summary>
/// Multi-input function approximation fitness function for benchmarking.
/// Defines 10 test cases with 4 inputs and 1 output, computing fitness
/// based on mean squared error against target values.
/// </summary>
public sealed class MultiInputFitnessFunction : IGpuFitnessFunction
{
    private static readonly float[] TestInputs;
    private static readonly float[] TargetOutputs;

    static MultiInputFitnessFunction()
    {
        // 10 test cases, 4 inputs each = 40 floats
        const int caseCount = 10;
        const int inputCount = 4;
        TestInputs = new float[caseCount * inputCount];
        TargetOutputs = new float[caseCount];

        // Generate deterministic test cases
        for (int c = 0; c < caseCount; c++)
        {
            float x = c / (float)(caseCount - 1); // 0..1
            TestInputs[c * inputCount + 0] = x;
            TestInputs[c * inputCount + 1] = 1.0f - x;
            TestInputs[c * inputCount + 2] = x * x;
            TestInputs[c * inputCount + 3] = MathF.Sin(x * MathF.PI);

            // Target: weighted combination
            TargetOutputs[c] = 0.5f * (x + MathF.Sin(x * MathF.PI));
        }
    }

    /// <inheritdoc />
    public int CaseCount => 10;

    /// <inheritdoc />
    public int OutputCount => 1;

    /// <inheritdoc />
    public ReadOnlyMemory<float> InputCases => TestInputs;

    /// <inheritdoc />
    public double ComputeFitness(ReadOnlySpan<float> outputs)
    {
        double mse = 0;
        for (int i = 0; i < CaseCount; i++)
        {
            double error = TargetOutputs[i] - outputs[i];
            mse += error * error;
        }
        mse /= CaseCount;
        return 1.0 / (1.0 + mse); // Fitness in (0, 1]
    }
}

/// <summary>
/// Parameterized fitness function with configurable test case count and input count.
/// Generates deterministic test cases for benchmarking with varying workload sizes.
/// </summary>
public sealed class ParametricFitnessFunction : IGpuFitnessFunction
{
    private readonly float[] _testInputs;
    private readonly float[] _targetOutputs;

    public ParametricFitnessFunction(int caseCount, int inputCount)
    {
        CaseCount = caseCount;
        _testInputs = new float[caseCount * inputCount];
        _targetOutputs = new float[caseCount];

        for (int c = 0; c < caseCount; c++)
        {
            float x = c / (float)Math.Max(1, caseCount - 1);
            for (int j = 0; j < inputCount; j++)
            {
                _testInputs[c * inputCount + j] = j switch
                {
                    0 => x,
                    1 => 1.0f - x,
                    2 => x * x,
                    _ => MathF.Sin(x * MathF.PI * (j - 2))
                };
            }
            _targetOutputs[c] = 0.5f * (x + MathF.Sin(x * MathF.PI));
        }
    }

    public int CaseCount { get; }

    public int OutputCount => 1;

    public ReadOnlyMemory<float> InputCases => _testInputs;

    public double ComputeFitness(ReadOnlySpan<float> outputs)
    {
        double mse = 0;
        for (int i = 0; i < CaseCount; i++)
        {
            double error = _targetOutputs[i] - outputs[i];
            mse += error * error;
        }
        mse /= CaseCount;
        return 1.0 / (1.0 + mse);
    }
}
