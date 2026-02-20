using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeatSharp.Evaluation;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Scheduling;
using NeatSharp.Gpu.Tests.TestDoubles;
using Xunit;

namespace NeatSharp.Gpu.Tests.Integration;

/// <summary>
/// End-to-end integration tests for the hybrid CPU+GPU evaluation pipeline.
/// Uses mock backends (no real GPU required) to validate the full hybrid
/// evaluation flow including partitioning, concurrent dispatch, result merging,
/// and metrics emission across multiple policies.
/// </summary>
public class HybridTrainingIntegrationTests
{
    // --- Test 1: Static policy end-to-end ---

    [Fact]
    public async Task StaticPolicy_MultiGenerationEvaluation_AllGenomesReceiveFitnessAndMetricsEmitted()
    {
        // Arrange: 100 genomes, 5 generations, static 70/30 GPU/CPU split
        const int populationSize = 100;
        const int generations = 5;
        const double gpuFraction = 0.7;

        var cpuEvaluator = new StubBatchEvaluator(fitnessOffset: 1.0);
        var gpuEvaluator = new StubBatchEvaluator(fitnessOffset: 100.0);
        var policy = new StaticPartitionPolicy(gpuFraction);
        var reporter = new CollectingMetricsReporter();
        var options = Options.Create(new HybridOptions
        {
            EnableHybrid = true,
            SplitPolicy = SplitPolicyType.Static,
            StaticGpuFraction = gpuFraction,
            MinPopulationForSplit = 10
        });

        using var evaluator = new HybridBatchEvaluator(
            cpuEvaluator, gpuEvaluator, policy, reporter, options,
            NullLogger<HybridBatchEvaluator>.Instance);

        var genomes = CreateGenomes(populationSize);

        // Act: run multiple generations
        for (var gen = 0; gen < generations; gen++)
        {
            var fitness = new double[populationSize];
            await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

            // Assert: all genomes received a fitness value (non-default)
            for (var i = 0; i < populationSize; i++)
            {
                fitness[i].Should().NotBe(0.0,
                    $"genome {i} in generation {gen} should have received a fitness value");
            }
        }

        // Assert: metrics emitted for every generation
        reporter.Metrics.Should().HaveCount(generations);

        // Assert: split ratio is consistent across generations (static policy)
        foreach (var metrics in reporter.Metrics)
        {
            metrics.ActivePolicy.Should().Be(SplitPolicyType.Static);
            metrics.SplitRatio.Should().BeApproximately(gpuFraction, 0.02,
                "static split ratio should be approximately the configured GPU fraction");
            metrics.CpuGenomeCount.Should().BeGreaterThan(0);
            metrics.GpuGenomeCount.Should().BeGreaterThan(0);
            (metrics.CpuGenomeCount + metrics.GpuGenomeCount).Should().Be(populationSize);
        }

        // Assert: both backends were invoked
        cpuEvaluator.CallCount.Should().Be(generations);
        gpuEvaluator.CallCount.Should().Be(generations);
    }

    // --- Test 2: Adaptive policy end-to-end ---

    [Fact]
    public async Task AdaptivePolicy_MultiGenerationEvaluation_SplitRatioAdaptsOverGenerations()
    {
        // Arrange: use asymmetric delays to force the PID controller to adapt
        // CPU is slower (50ms) so PID should shift more work to GPU
        const int populationSize = 100;
        const int generations = 15;

        var cpuEvaluator = new StubBatchEvaluator(fitnessOffset: 1.0, delay: TimeSpan.FromMilliseconds(50));
        var gpuEvaluator = new StubBatchEvaluator(fitnessOffset: 100.0, delay: TimeSpan.FromMilliseconds(10));
        var adaptiveOptions = new AdaptivePidOptions
        {
            Kp = 0.5,
            Ki = 0.1,
            Kd = 0.05,
            InitialGpuFraction = 0.5
        };
        var policy = new AdaptivePartitionPolicy(adaptiveOptions);
        var reporter = new CollectingMetricsReporter();
        var options = Options.Create(new HybridOptions
        {
            EnableHybrid = true,
            SplitPolicy = SplitPolicyType.Adaptive,
            MinPopulationForSplit = 10,
            Adaptive = adaptiveOptions
        });

        using var evaluator = new HybridBatchEvaluator(
            cpuEvaluator, gpuEvaluator, policy, reporter, options,
            NullLogger<HybridBatchEvaluator>.Instance);

        var genomes = CreateGenomes(populationSize);

        // Act: run multiple generations
        for (var gen = 0; gen < generations; gen++)
        {
            var fitness = new double[populationSize];
            await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

            // All genomes receive fitness
            fitness.Should().OnlyContain(f => f != 0.0);
        }

        // Assert: metrics emitted for every generation
        reporter.Metrics.Should().HaveCount(generations);

        // Assert: the split ratio changes over time (adaptation is occurring)
        var initialRatio = reporter.Metrics[0].SplitRatio;
        var laterRatios = reporter.Metrics.Skip(5).Select(m => m.SplitRatio).ToList();

        // Since CPU is slower, PID should increase GPU fraction over time
        // Verify adaptation occurred (ratios are not all identical to initial)
        laterRatios.Should().Contain(r => Math.Abs(r - initialRatio) > 0.01,
            "adaptive policy should adjust the split ratio when backend latencies differ");
    }

    // --- Test 3: Cost-based policy end-to-end ---

    [Fact]
    public async Task CostBasedPolicy_BimodalPopulation_ComplexGenomesRouteToGpu()
    {
        // Arrange: bimodal population - half simple (5 nodes), half complex (50 nodes)
        const int populationSize = 100;
        const double gpuFraction = 0.5;

        var simpleGenomes = Enumerable.Range(0, 50)
            .Select(_ => new StubGenome(nodeCount: 5, connectionCount: 3))
            .Cast<IGenome>().ToList();
        var complexGenomes = Enumerable.Range(0, 50)
            .Select(_ => new StubGenome(nodeCount: 50, connectionCount: 40))
            .Cast<IGenome>().ToList();

        // Interleave simple and complex genomes
        var genomes = new List<IGenome>(populationSize);
        for (var i = 0; i < 50; i++)
        {
            genomes.Add(simpleGenomes[i]);
            genomes.Add(complexGenomes[i]);
        }

        // CPU evaluator assigns fitness = localIndex + 1
        var cpuEvaluator = new TrackingBatchEvaluator();
        // GPU evaluator assigns fitness = (localIndex + 1) * 1000
        var gpuEvaluator = new TrackingBatchEvaluator();

        var costModel = new CostModelOptions { NodeWeight = 1.0, ConnectionWeight = 1.0 };
        var policy = new CostBasedPartitionPolicy(gpuFraction, costModel);
        var reporter = new CollectingMetricsReporter();
        var options = Options.Create(new HybridOptions
        {
            EnableHybrid = true,
            SplitPolicy = SplitPolicyType.CostBased,
            StaticGpuFraction = gpuFraction,
            MinPopulationForSplit = 10,
            CostModel = costModel
        });

        using var evaluator = new HybridBatchEvaluator(
            cpuEvaluator, gpuEvaluator, policy, reporter, options,
            NullLogger<HybridBatchEvaluator>.Instance);

        var fitness = new double[populationSize];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        // Assert: all genomes received fitness
        fitness.Should().OnlyContain(f => f != 0.0);

        // Assert: GPU received the complex genomes (higher cost)
        // CostBasedPartitionPolicy sorts by cost descending, routes top gpuFraction to GPU
        gpuEvaluator.LastReceivedGenomes.Should().NotBeNull();
        var gpuGenomes = gpuEvaluator.LastReceivedGenomes!;
        gpuGenomes.Should().NotBeEmpty();

        // The genomes sent to GPU should have higher average node count than those sent to CPU
        var gpuAvgNodes = gpuGenomes.Average(g => g.NodeCount);
        cpuEvaluator.LastReceivedGenomes.Should().NotBeNull();
        var cpuGenomes = cpuEvaluator.LastReceivedGenomes!;
        var cpuAvgNodes = cpuGenomes.Average(g => g.NodeCount);

        gpuAvgNodes.Should().BeGreaterThan(cpuAvgNodes,
            "cost-based policy should route complex genomes (higher node count) to GPU");

        // Assert: metrics reflect cost-based policy
        reporter.Metrics.Should().HaveCount(1);
        reporter.Metrics[0].ActivePolicy.Should().Be(SplitPolicyType.CostBased);
    }

    // --- Test 4: CPU determinism preserved when hybrid disabled (SC-003) ---

    [Fact]
    public async Task HybridDisabled_SamePopulationTwice_ProducesIdenticalFitness()
    {
        // Arrange: deterministic evaluator (fitness = index * 1.5 + 0.1)
        const int populationSize = 50;

        var genomes = CreateGenomes(populationSize);

        // First run
        var cpuEval1 = new DeterministicBatchEvaluator();
        var gpuEval1 = new DeterministicBatchEvaluator();
        var options = Options.Create(new HybridOptions { EnableHybrid = false });

        using var evaluator1 = new HybridBatchEvaluator(
            cpuEval1, gpuEval1, new StaticPartitionPolicy(0.5),
            new CollectingMetricsReporter(), options,
            NullLogger<HybridBatchEvaluator>.Instance);

        var fitness1 = new double[populationSize];
        await evaluator1.EvaluateAsync(genomes, (i, f) => fitness1[i] = f, CancellationToken.None);

        // Second run
        var cpuEval2 = new DeterministicBatchEvaluator();
        var gpuEval2 = new DeterministicBatchEvaluator();

        using var evaluator2 = new HybridBatchEvaluator(
            cpuEval2, gpuEval2, new StaticPartitionPolicy(0.5),
            new CollectingMetricsReporter(), options,
            NullLogger<HybridBatchEvaluator>.Instance);

        var fitness2 = new double[populationSize];
        await evaluator2.EvaluateAsync(genomes, (i, f) => fitness2[i] = f, CancellationToken.None);

        // Assert: identical results
        fitness1.Should().Equal(fitness2,
            "when hybrid is disabled, same population should produce identical fitness values");
    }

    // --- Test 5: Scheduler overhead < 5% of slower backend (FR-018) ---

    [Fact]
    public async Task SchedulerOverhead_WithKnownBackendDelay_IsLessThanFivePercentOfSlowerBackend()
    {
        // Arrange: backends with known delays
        const int populationSize = 100;
        var backendDelay = TimeSpan.FromMilliseconds(100);

        var cpuEvaluator = new StubBatchEvaluator(fitnessOffset: 1.0, delay: backendDelay);
        var gpuEvaluator = new StubBatchEvaluator(fitnessOffset: 100.0, delay: backendDelay);
        var policy = new StaticPartitionPolicy(0.5);
        var reporter = new CollectingMetricsReporter();
        var options = Options.Create(new HybridOptions
        {
            EnableHybrid = true,
            SplitPolicy = SplitPolicyType.Static,
            StaticGpuFraction = 0.5,
            MinPopulationForSplit = 10
        });

        using var evaluator = new HybridBatchEvaluator(
            cpuEvaluator, gpuEvaluator, policy, reporter, options,
            NullLogger<HybridBatchEvaluator>.Instance);

        var genomes = CreateGenomes(populationSize);

        // Act: run a few generations to get stable measurement
        for (var i = 0; i < 3; i++)
        {
            var fitness = new double[populationSize];
            await evaluator.EvaluateAsync(genomes, (i2, f) => fitness[i2] = f, CancellationToken.None);
        }

        // Assert: scheduler overhead < 5% of the slower backend for each generation
        foreach (var metrics in reporter.Metrics)
        {
            var slowerBackendLatency = TimeSpan.FromTicks(
                Math.Max(metrics.CpuLatency.Ticks, metrics.GpuLatency.Ticks));

            // Allow threshold: overhead should be less than 5% of the slower backend
            var overheadPercent = metrics.SchedulerOverhead.TotalMilliseconds /
                                 Math.Max(slowerBackendLatency.TotalMilliseconds, 0.001) * 100.0;

            overheadPercent.Should().BeLessThan(5.0,
                $"scheduler overhead ({metrics.SchedulerOverhead.TotalMilliseconds:F2}ms) should be " +
                $"< 5% of slower backend ({slowerBackendLatency.TotalMilliseconds:F2}ms) in generation {metrics.Generation}");
        }
    }

    // --- Helpers ---

    private static List<IGenome> CreateGenomes(int count)
    {
        var genomes = new List<IGenome>(count);
        for (var i = 0; i < count; i++)
        {
            genomes.Add(new StubGenome(nodeCount: 5, connectionCount: 3));
        }
        return genomes;
    }

    // --- Test doubles ---

    /// <summary>
    /// Batch evaluator stub that assigns predictable fitness and supports optional delay.
    /// </summary>
    private sealed class StubBatchEvaluator(double fitnessOffset = 0.5, TimeSpan? delay = null)
        : IBatchEvaluator
    {
        public int CallCount { get; private set; }

        public async Task EvaluateAsync(
            IReadOnlyList<IGenome> genomes,
            Action<int, double> setFitness,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (delay is { } d && d > TimeSpan.Zero)
            {
                await Task.Delay(d, cancellationToken);
            }

            for (var i = 0; i < genomes.Count; i++)
            {
                setFitness(i, i * 1.0 + fitnessOffset);
            }
        }
    }

    /// <summary>
    /// Batch evaluator that tracks genomes received for partition verification.
    /// </summary>
    private sealed class TrackingBatchEvaluator : IBatchEvaluator
    {
        public IReadOnlyList<IGenome>? LastReceivedGenomes { get; private set; }

        public Task EvaluateAsync(
            IReadOnlyList<IGenome> genomes,
            Action<int, double> setFitness,
            CancellationToken cancellationToken)
        {
            LastReceivedGenomes = genomes.ToList();
            for (var i = 0; i < genomes.Count; i++)
            {
                setFitness(i, (i + 1) * 1.0);
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Deterministic batch evaluator that always assigns the same fitness for the same index.
    /// </summary>
    private sealed class DeterministicBatchEvaluator : IBatchEvaluator
    {
        public Task EvaluateAsync(
            IReadOnlyList<IGenome> genomes,
            Action<int, double> setFitness,
            CancellationToken cancellationToken)
        {
            for (var i = 0; i < genomes.Count; i++)
            {
                setFitness(i, i * 1.5 + 0.1);
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Metrics reporter that collects all reported metrics for verification.
    /// </summary>
    private sealed class CollectingMetricsReporter : ISchedulingMetricsReporter
    {
        public List<SchedulingMetrics> Metrics { get; } = [];

        public void Report(SchedulingMetrics metrics) => Metrics.Add(metrics);
    }
}
