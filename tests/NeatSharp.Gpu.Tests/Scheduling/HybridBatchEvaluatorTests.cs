using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeatSharp.Evaluation;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Scheduling;
using NeatSharp.Gpu.Tests.TestDoubles;
using Xunit;

namespace NeatSharp.Gpu.Tests.Scheduling;

public class HybridBatchEvaluatorTests
{
    private readonly HybridOptions _defaultOptions = new()
    {
        EnableHybrid = true,
        SplitPolicy = SplitPolicyType.Static,
        StaticGpuFraction = 0.7,
        MinPopulationForSplit = 5
    };

    private static List<IGenome> CreateGenomes(int count)
    {
        var genomes = new List<IGenome>(count);
        for (var i = 0; i < count; i++)
        {
            genomes.Add(new StubGenome(nodeCount: 5, connectionCount: 3));
        }
        return genomes;
    }

    private HybridBatchEvaluator CreateEvaluator(
        IBatchEvaluator? cpuEvaluator = null,
        IBatchEvaluator? gpuEvaluator = null,
        IPartitionPolicy? partitionPolicy = null,
        ISchedulingMetricsReporter? metricsReporter = null,
        HybridOptions? options = null,
        ILogger<HybridBatchEvaluator>? logger = null)
    {
        return new HybridBatchEvaluator(
            cpuEvaluator ?? new StubBatchEvaluator(),
            gpuEvaluator ?? new StubBatchEvaluator(),
            partitionPolicy ?? new StaticPartitionPolicy(_defaultOptions.StaticGpuFraction),
            metricsReporter ?? new NullMetricsReporter(),
            Options.Create(options ?? _defaultOptions),
            logger ?? NullLogger<HybridBatchEvaluator>.Instance);
    }

    // --- Concurrent dispatch ---

    [Fact]
    public async Task EvaluateAsync_WithHybridEnabled_CallsBothBackends()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 1.0);
        var gpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 2.0);
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator);

        var fitness = new double[10];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        cpuEvaluator.CallCount.Should().Be(1);
        gpuEvaluator.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task EvaluateAsync_WithHybridEnabled_CpuAndGpuReceiveCorrectSubsets()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 1.0);
        var gpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 2.0);
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator);

        var fitness = new double[10];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        // With 0.7 GPU fraction: cpuCount = (int)(0.3 * 10) = 3, gpuCount = 7
        cpuEvaluator.LastGenomeCount.Should().Be(3);
        gpuEvaluator.LastGenomeCount.Should().Be(7);
    }

    // --- Index-remapped setFitness merges correctly ---

    [Fact]
    public async Task EvaluateAsync_IndexRemapping_AllGenomesReceiveCorrectFitness()
    {
        // CPU evaluator assigns fitness = localIndex * 10
        var cpuEvaluator = new IndexAwareBatchEvaluator(localIndex => localIndex * 10.0);
        // GPU evaluator assigns fitness = localIndex * 100
        var gpuEvaluator = new IndexAwareBatchEvaluator(localIndex => localIndex * 100.0);
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator);

        var fitness = new double[10];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        // CPU handles indices 0, 1, 2 (local 0, 1, 2): fitness = 0, 10, 20
        fitness[0].Should().Be(0.0);
        fitness[1].Should().Be(10.0);
        fitness[2].Should().Be(20.0);

        // GPU handles indices 3..9 (local 0..6): fitness = 0, 100, 200, 300, 400, 500, 600
        fitness[3].Should().Be(0.0);
        fitness[4].Should().Be(100.0);
        fitness[5].Should().Be(200.0);
        fitness[6].Should().Be(300.0);
        fitness[7].Should().Be(400.0);
        fitness[8].Should().Be(500.0);
        fitness[9].Should().Be(600.0);
    }

    [Fact]
    public async Task EvaluateAsync_IndexRemapping_NoMisalignmentDuplicationOrOmission()
    {
        var cpuEvaluator = new IndexAwareBatchEvaluator(localIndex => 1.0);
        var gpuEvaluator = new IndexAwareBatchEvaluator(localIndex => 2.0);
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator);

        var fitnessSetCount = new int[10];
        await evaluator.EvaluateAsync(
            genomes,
            (i, f) => Interlocked.Increment(ref fitnessSetCount[i]),
            CancellationToken.None);

        // Each genome should have fitness set exactly once
        fitnessSetCount.Should().AllSatisfy(count => count.Should().Be(1));
    }

    // --- Population below MinPopulationForSplit ---

    [Fact]
    public async Task EvaluateAsync_BelowMinPopulation_DelegatesToCpuOnly()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 1.0);
        var gpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 2.0);
        var options = new HybridOptions
        {
            EnableHybrid = true,
            MinPopulationForSplit = 10
        };
        var genomes = CreateGenomes(5); // Below threshold of 10

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator,
            options: options);

        var fitness = new double[5];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        cpuEvaluator.CallCount.Should().Be(1);
        gpuEvaluator.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateAsync_BelowMinPopulation_AllGenomesEvaluatedByCpu()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 42.0);
        var gpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 99.0);
        var options = new HybridOptions
        {
            EnableHybrid = true,
            MinPopulationForSplit = 10
        };
        var genomes = CreateGenomes(5);

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator,
            options: options);

        var fitness = new double[5];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        cpuEvaluator.LastGenomeCount.Should().Be(5);
        fitness.Should().AllSatisfy(f => f.Should().Be(42.0));
    }

    // --- EnableHybrid=false passthrough ---

    [Fact]
    public async Task EvaluateAsync_HybridDisabled_DelegatesToGpuEvaluatorOnly()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 1.0);
        var gpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 2.0);
        var options = new HybridOptions { EnableHybrid = false };
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator,
            options: options);

        var fitness = new double[10];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        cpuEvaluator.CallCount.Should().Be(0);
        gpuEvaluator.CallCount.Should().Be(1);
        gpuEvaluator.LastGenomeCount.Should().Be(10);
        fitness.Should().AllSatisfy(f => f.Should().Be(2.0));
    }

    // --- Metrics emitted via ISchedulingMetricsReporter ---

    [Fact]
    public async Task EvaluateAsync_WithHybridEnabled_EmitsMetrics()
    {
        var metricsReporter = new RecordingMetricsReporter();
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(metricsReporter: metricsReporter);

        var fitness = new double[10];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        metricsReporter.ReportedMetrics.Should().HaveCount(1);
        var metrics = metricsReporter.ReportedMetrics[0];
        metrics.Generation.Should().Be(1);
        metrics.CpuGenomeCount.Should().Be(3);
        metrics.GpuGenomeCount.Should().Be(7);
        metrics.ActivePolicy.Should().Be(SplitPolicyType.Static);
    }

    [Fact]
    public async Task EvaluateAsync_MultipleGenerations_IncrementsGenerationCounter()
    {
        var metricsReporter = new RecordingMetricsReporter();
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(metricsReporter: metricsReporter);

        var fitness = new double[10];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        metricsReporter.ReportedMetrics.Should().HaveCount(3);
        metricsReporter.ReportedMetrics[0].Generation.Should().Be(1);
        metricsReporter.ReportedMetrics[1].Generation.Should().Be(2);
        metricsReporter.ReportedMetrics[2].Generation.Should().Be(3);
    }

    [Fact]
    public async Task EvaluateAsync_WithHybridEnabled_MetricsContainThroughputAndLatency()
    {
        var metricsReporter = new RecordingMetricsReporter();
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(metricsReporter: metricsReporter);

        var fitness = new double[10];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        var metrics = metricsReporter.ReportedMetrics[0];
        metrics.CpuThroughput.Should().BeGreaterThan(0);
        metrics.GpuThroughput.Should().BeGreaterThan(0);
        metrics.CpuLatency.Should().BeGreaterThan(TimeSpan.Zero);
        metrics.GpuLatency.Should().BeGreaterThan(TimeSpan.Zero);
        metrics.SchedulerOverhead.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task EvaluateAsync_BelowMinPopulation_StillEmitsMetrics()
    {
        var metricsReporter = new RecordingMetricsReporter();
        var options = new HybridOptions
        {
            EnableHybrid = true,
            MinPopulationForSplit = 20
        };
        var genomes = CreateGenomes(5);

        using var evaluator = CreateEvaluator(
            metricsReporter: metricsReporter,
            options: options);

        var fitness = new double[5];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        metricsReporter.ReportedMetrics.Should().HaveCount(1);
        var metrics = metricsReporter.ReportedMetrics[0];
        metrics.CpuGenomeCount.Should().Be(5);
        metrics.GpuGenomeCount.Should().Be(0);
    }

    // --- GPU failure basic handling ---

    [Fact]
    public async Task EvaluateAsync_GpuFails_FallsBackToCpu()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 1.0);
        var gpuEvaluator = new FailingBatchEvaluator(new InvalidOperationException("GPU error"));
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator);

        var fitness = new double[10];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        // CPU should have been called for its partition + the GPU fallback
        // All genomes should have fitness set
        fitness.Should().AllSatisfy(f => f.Should().Be(1.0));
    }

    [Fact]
    public async Task EvaluateAsync_OperationCanceledException_IsNotCaughtAsGpuFailure()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 1.0);
        var gpuEvaluator = new FailingBatchEvaluator(new OperationCanceledException("Cancelled"));
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator);

        var fitness = new double[10];
        var act = () => evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- GPU fallback and re-probe (Phase 5) ---

    [Fact]
    public async Task EvaluateAsync_GpuException_ReroutesGpuGenomesToCpuWithCorrectFitness()
    {
        // CPU evaluator assigns a known fitness value
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 5.0);
        var gpuEvaluator = new ConfigurableFailingBatchEvaluator(
            failCount: 1,
            exception: new InvalidOperationException("GPU OOM"),
            successFitnessValue: 99.0);
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator);

        var fitness = new double[10];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        // All genomes should have fitness set by CPU (original CPU partition + rerouted GPU partition)
        fitness.Should().AllSatisfy(f => f.Should().Be(5.0));
    }

    [Fact]
    public async Task EvaluateAsync_GpuFallback_NoGenomesLostOrDuplicated()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 1.0);
        var gpuEvaluator = new ConfigurableFailingBatchEvaluator(
            failCount: 1,
            exception: new InvalidOperationException("GPU error"),
            successFitnessValue: 2.0);
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator);

        var fitnessSetCount = new int[10];
        await evaluator.EvaluateAsync(
            genomes,
            (i, _) => Interlocked.Increment(ref fitnessSetCount[i]),
            CancellationToken.None);

        // Each genome should have fitness set exactly once — no loss, no duplication
        fitnessSetCount.Should().AllSatisfy(count => count.Should().Be(1));
    }

    [Fact]
    public async Task EvaluateAsync_GpuFallback_WarningLoggedWithFailureDetails()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 1.0);
        var gpuEvaluator = new ConfigurableFailingBatchEvaluator(
            failCount: 1,
            exception: new InvalidOperationException("GPU device lost"),
            successFitnessValue: 2.0);
        var genomes = CreateGenomes(10);
        var recordingLogger = new RecordingLogger<HybridBatchEvaluator>();

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator,
            logger: recordingLogger);

        var fitness = new double[10];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        // FR-008: Warning logged with failure reason and rerouted genome count
        recordingLogger.LogEntries.Should().Contain(entry =>
            entry.LogLevel == LogLevel.Warning &&
            entry.Message.Contains("GPU") &&
            entry.Message.Contains("7")); // 7 genomes rerouted (70% of 10)
    }

    [Fact]
    public async Task EvaluateAsync_GpuFailedInPreviousGeneration_SubsequentGenerationUsesCpuOnly()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 1.0);
        // Fail on first call (generation 1), succeed thereafter
        var gpuEvaluator = new ConfigurableFailingBatchEvaluator(
            failCount: 1,
            exception: new InvalidOperationException("GPU error"),
            successFitnessValue: 2.0);
        var genomes = CreateGenomes(10);
        var options = new HybridOptions
        {
            EnableHybrid = true,
            SplitPolicy = SplitPolicyType.Static,
            StaticGpuFraction = 0.7,
            MinPopulationForSplit = 5,
            GpuReprobeInterval = 10 // won't re-probe for 10 generations
        };

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator,
            options: options);

        // Generation 1: GPU fails, falls back to CPU
        var fitness = new double[10];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        // Reset CPU call count for generation 2 observation
        var cpuCallCountAfterGen1 = cpuEvaluator.CallCount;
        var gpuCallCountAfterGen1 = gpuEvaluator.CallCount;

        // Generation 2: Should use CPU-only (GPU marked unavailable)
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        // GPU should NOT have been called again in generation 2
        gpuEvaluator.CallCount.Should().Be(gpuCallCountAfterGen1);
        // CPU should have been called for all genomes in generation 2
        cpuEvaluator.CallCount.Should().BeGreaterThan(cpuCallCountAfterGen1);
        fitness.Should().AllSatisfy(f => f.Should().Be(1.0));
    }

    [Fact]
    public async Task EvaluateAsync_ReprobeAfterInterval_RestoresHybridMode()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 1.0);
        // Fail on first call only, succeed on all subsequent
        var gpuEvaluator = new ConfigurableFailingBatchEvaluator(
            failCount: 1,
            exception: new InvalidOperationException("GPU error"),
            successFitnessValue: 2.0);
        var genomes = CreateGenomes(10);
        var reprobeInterval = 3;
        var options = new HybridOptions
        {
            EnableHybrid = true,
            SplitPolicy = SplitPolicyType.Static,
            StaticGpuFraction = 0.7,
            MinPopulationForSplit = 5,
            GpuReprobeInterval = reprobeInterval
        };
        var recordingLogger = new RecordingLogger<HybridBatchEvaluator>();

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator,
            options: options,
            logger: recordingLogger);

        var fitness = new double[10];

        // Generation 1: GPU fails
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);
        var gpuCallsAfterFailure = gpuEvaluator.CallCount;

        // Generations 2..reprobeInterval: CPU-only, no GPU calls
        for (var gen = 2; gen <= reprobeInterval; gen++)
        {
            await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);
        }
        gpuEvaluator.CallCount.Should().Be(gpuCallsAfterFailure, "GPU should not be called during CPU-only interval");

        // Generation reprobeInterval+1: re-probe should occur and succeed
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        // GPU should have been called for the re-probe
        gpuEvaluator.CallCount.Should().BeGreaterThan(gpuCallsAfterFailure);

        // Should log information about successful re-probe
        recordingLogger.LogEntries.Should().Contain(entry =>
            entry.LogLevel == LogLevel.Information &&
            entry.Message.Contains("re-probe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateAsync_ReprobeFailure_ContinuesCpuOnly()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 1.0);
        // Fail on first 2 calls (initial failure + re-probe failure), succeed on third
        var gpuEvaluator = new ConfigurableFailingBatchEvaluator(
            failCount: 2,
            exception: new InvalidOperationException("GPU still broken"),
            successFitnessValue: 2.0);
        var genomes = CreateGenomes(10);
        var reprobeInterval = 3;
        var options = new HybridOptions
        {
            EnableHybrid = true,
            SplitPolicy = SplitPolicyType.Static,
            StaticGpuFraction = 0.7,
            MinPopulationForSplit = 5,
            GpuReprobeInterval = reprobeInterval
        };
        var recordingLogger = new RecordingLogger<HybridBatchEvaluator>();

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator,
            options: options,
            logger: recordingLogger);

        var fitness = new double[10];

        // Generation 1: GPU fails
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        // Generations 2..reprobeInterval: CPU-only
        for (var gen = 2; gen <= reprobeInterval; gen++)
        {
            await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);
        }

        // Generation reprobeInterval+1: re-probe should fail again
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        // All genomes should still have CPU fitness — no crash
        fitness.Should().AllSatisfy(f => f.Should().Be(1.0));

        // Should log warning about re-probe failure
        recordingLogger.LogEntries.Should().Contain(entry =>
            entry.LogLevel == LogLevel.Warning &&
            entry.Message.Contains("re-probe", StringComparison.OrdinalIgnoreCase));

        // Generation reprobeInterval+2: still CPU-only (counter reset after failed re-probe)
        var gpuCallsBeforeNextCpuOnly = gpuEvaluator.CallCount;
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);
        gpuEvaluator.CallCount.Should().Be(gpuCallsBeforeNextCpuOnly,
            "GPU should not be called in generation after failed re-probe");
    }

    [Fact]
    public async Task EvaluateAsync_GpuFailureOnFirstGeneration_DefaultsToCpuGracefully()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 3.0);
        var gpuEvaluator = new ConfigurableFailingBatchEvaluator(
            failCount: 1,
            exception: new InvalidOperationException("GPU init failed"),
            successFitnessValue: 99.0);
        var genomes = CreateGenomes(10);

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator);

        var fitness = new double[10];

        // First generation: GPU fails, should fallback gracefully even with no history
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        fitness.Should().AllSatisfy(f => f.Should().Be(3.0));

        // Second generation: should run CPU-only without error
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        fitness.Should().AllSatisfy(f => f.Should().Be(3.0));
    }

    [Fact]
    public async Task EvaluateAsync_GpuFallback_FallbackEventInfoPopulatedInMetrics()
    {
        var cpuEvaluator = new RecordingBatchEvaluator(fitnessValue: 1.0);
        var gpuEvaluator = new ConfigurableFailingBatchEvaluator(
            failCount: 1,
            exception: new InvalidOperationException("GPU memory exhausted"),
            successFitnessValue: 2.0);
        var genomes = CreateGenomes(10);
        var metricsReporter = new RecordingMetricsReporter();

        using var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator,
            metricsReporter: metricsReporter);

        var fitness = new double[10];
        await evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, CancellationToken.None);

        metricsReporter.ReportedMetrics.Should().HaveCount(1);
        var metrics = metricsReporter.ReportedMetrics[0];
        metrics.FallbackEvent.Should().NotBeNull();
        metrics.FallbackEvent!.Value.FailureReason.Should().Contain("GPU memory exhausted");
        metrics.FallbackEvent!.Value.GenomesRerouted.Should().Be(7); // 70% of 10
        metrics.FallbackEvent!.Value.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    // --- IDisposable ---

    [Fact]
    public void Dispose_DisposesInnerEvaluators()
    {
        var cpuEvaluator = new DisposableBatchEvaluator();
        var gpuEvaluator = new DisposableBatchEvaluator();

        var evaluator = CreateEvaluator(
            cpuEvaluator: cpuEvaluator,
            gpuEvaluator: gpuEvaluator);

        evaluator.Dispose();

        cpuEvaluator.IsDisposed.Should().BeTrue();
        gpuEvaluator.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void Dispose_NonDisposableEvaluators_DoesNotThrow()
    {
        var evaluator = CreateEvaluator();

        var act = () => evaluator.Dispose();

        act.Should().NotThrow();
    }

    // --- Cancellation ---

    [Fact]
    public async Task EvaluateAsync_CancellationRequested_PropagatesCancellation()
    {
        var genomes = CreateGenomes(10);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        using var evaluator = CreateEvaluator(
            gpuEvaluator: new CancellationAwareBatchEvaluator());

        var fitness = new double[10];
        var act = () => evaluator.EvaluateAsync(genomes, (i, f) => fitness[i] = f, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- Test doubles ---

    private sealed class StubBatchEvaluator : IBatchEvaluator
    {
        public Task EvaluateAsync(IReadOnlyList<IGenome> genomes, Action<int, double> setFitness, CancellationToken cancellationToken)
        {
            for (var i = 0; i < genomes.Count; i++)
            {
                setFitness(i, 0.0);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingBatchEvaluator(double fitnessValue) : IBatchEvaluator
    {
        public int CallCount { get; private set; }
        public int LastGenomeCount { get; private set; }

        public Task EvaluateAsync(IReadOnlyList<IGenome> genomes, Action<int, double> setFitness, CancellationToken cancellationToken)
        {
            CallCount++;
            LastGenomeCount = genomes.Count;
            for (var i = 0; i < genomes.Count; i++)
            {
                setFitness(i, fitnessValue);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class IndexAwareBatchEvaluator(Func<int, double> fitnessFunc) : IBatchEvaluator
    {
        public Task EvaluateAsync(IReadOnlyList<IGenome> genomes, Action<int, double> setFitness, CancellationToken cancellationToken)
        {
            for (var i = 0; i < genomes.Count; i++)
            {
                setFitness(i, fitnessFunc(i));
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FailingBatchEvaluator(Exception exception) : IBatchEvaluator
    {
        public Task EvaluateAsync(IReadOnlyList<IGenome> genomes, Action<int, double> setFitness, CancellationToken cancellationToken)
        {
            throw exception;
        }
    }

    private sealed class DisposableBatchEvaluator : IBatchEvaluator, IDisposable
    {
        public bool IsDisposed { get; private set; }

        public Task EvaluateAsync(IReadOnlyList<IGenome> genomes, Action<int, double> setFitness, CancellationToken cancellationToken)
        {
            for (var i = 0; i < genomes.Count; i++)
            {
                setFitness(i, 0.0);
            }
            return Task.CompletedTask;
        }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class CancellationAwareBatchEvaluator : IBatchEvaluator
    {
        public Task EvaluateAsync(IReadOnlyList<IGenome> genomes, Action<int, double> setFitness, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class NullMetricsReporter : ISchedulingMetricsReporter
    {
        public void Report(SchedulingMetrics metrics) { }
    }

    private sealed class RecordingMetricsReporter : ISchedulingMetricsReporter
    {
        public List<SchedulingMetrics> ReportedMetrics { get; } = [];

        public void Report(SchedulingMetrics metrics) => ReportedMetrics.Add(metrics);
    }

    /// <summary>
    /// Evaluator that fails on the first <paramref name="failCount"/> calls, then succeeds.
    /// Used for testing GPU failure + re-probe scenarios.
    /// </summary>
    private sealed class ConfigurableFailingBatchEvaluator(
        int failCount,
        Exception exception,
        double successFitnessValue) : IBatchEvaluator
    {
        private int _callCount;

        public int CallCount => _callCount;

        public Task EvaluateAsync(IReadOnlyList<IGenome> genomes, Action<int, double> setFitness, CancellationToken cancellationToken)
        {
            var currentCall = Interlocked.Increment(ref _callCount);
            if (currentCall <= failCount)
            {
                throw exception;
            }

            for (var i = 0; i < genomes.Count; i++)
            {
                setFitness(i, successFitnessValue);
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Logger that records all log entries for assertion purposes.
    /// </summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> LogEntries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogEntries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }

        public record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);
    }
}
