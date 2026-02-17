using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeatSharp.Evaluation;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;

namespace NeatSharp.Gpu.Scheduling;

/// <summary>
/// Decorator over CPU and GPU <see cref="IBatchEvaluator"/> backends that partitions
/// genomes via <see cref="IPartitionPolicy"/>, dispatches both concurrently, and
/// merges results through index-remapped <c>setFitness</c> callbacks.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>When <see cref="HybridOptions.EnableHybrid"/> is <c>false</c>, delegates
///         directly to the GPU evaluator with zero overhead.</item>
///   <item>When population size is below <see cref="HybridOptions.MinPopulationForSplit"/>,
///         delegates to the CPU evaluator (default single-backend path).</item>
///   <item>Otherwise, partitions via the configured policy and dispatches both backends
///         concurrently using <see cref="Task.WhenAll"/>.</item>
/// </list>
/// </remarks>
internal sealed class HybridBatchEvaluator : IBatchEvaluator, IDisposable
{
    private readonly IBatchEvaluator _cpuEvaluator;
    private readonly IBatchEvaluator _gpuEvaluator;
    private readonly IPartitionPolicy _partitionPolicy;
    private readonly ISchedulingMetricsReporter _metricsReporter;
    private readonly HybridOptions _options;
    private readonly ILogger<HybridBatchEvaluator> _logger;

    private int _generation;
    private bool _gpuAvailable = true;
    private int _generationsSinceGpuFailure;

    /// <summary>
    /// Initializes a new <see cref="HybridBatchEvaluator"/>.
    /// </summary>
    public HybridBatchEvaluator(
        IBatchEvaluator cpuEvaluator,
        IBatchEvaluator gpuEvaluator,
        IPartitionPolicy partitionPolicy,
        ISchedulingMetricsReporter metricsReporter,
        IOptions<HybridOptions> options,
        ILogger<HybridBatchEvaluator> logger)
    {
        ArgumentNullException.ThrowIfNull(cpuEvaluator);
        ArgumentNullException.ThrowIfNull(gpuEvaluator);
        ArgumentNullException.ThrowIfNull(partitionPolicy);
        ArgumentNullException.ThrowIfNull(metricsReporter);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _cpuEvaluator = cpuEvaluator;
        _gpuEvaluator = gpuEvaluator;
        _partitionPolicy = partitionPolicy;
        _metricsReporter = metricsReporter;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task EvaluateAsync(
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        CancellationToken cancellationToken)
    {
        _generation++;

        // FR-012: When hybrid is disabled, passthrough to GPU evaluator directly.
        if (!_options.EnableHybrid)
        {
            await _gpuEvaluator.EvaluateAsync(genomes, setFitness, cancellationToken);
            return;
        }

        // FR-014: Below MinPopulationForSplit, delegate to CPU evaluator.
        if (genomes.Count < _options.MinPopulationForSplit)
        {
            await EvaluateCpuOnlyAsync(genomes, setFitness, cancellationToken);
            return;
        }

        // FR-009: GPU unavailable — check for re-probe or run CPU-only.
        if (!_gpuAvailable)
        {
            _generationsSinceGpuFailure++;

            if (_generationsSinceGpuFailure >= _options.GpuReprobeInterval)
            {
                // Attempt GPU re-probe via normal hybrid dispatch.
                // If GPU succeeds, hybrid mode is restored.
                // If GPU fails again, stay CPU-only and reset counter.
                await EvaluateWithReprobeAsync(genomes, setFitness, cancellationToken);
                return;
            }

            // GPU still unavailable, not yet time to re-probe — CPU-only.
            await EvaluateCpuOnlyAsync(genomes, setFitness, cancellationToken);
            return;
        }

        await EvaluateHybridAsync(genomes, setFitness, cancellationToken);
    }

    /// <summary>
    /// Evaluates all genomes on CPU only, emitting metrics with zero GPU activity.
    /// Used when GPU is unavailable or population is below MinPopulationForSplit.
    /// </summary>
    private async Task EvaluateCpuOnlyAsync(
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var cpuSw = Stopwatch.StartNew();
        await _cpuEvaluator.EvaluateAsync(genomes, setFitness, cancellationToken);
        cpuSw.Stop();
        sw.Stop();

        var singleBackendMetrics = new SchedulingMetrics
        {
            Generation = _generation,
            CpuGenomeCount = genomes.Count,
            GpuGenomeCount = 0,
            CpuThroughput = genomes.Count / Math.Max(cpuSw.Elapsed.TotalSeconds, double.Epsilon),
            GpuThroughput = 0,
            CpuLatency = cpuSw.Elapsed,
            GpuLatency = TimeSpan.Zero,
            SplitRatio = 0,
            ActivePolicy = _options.SplitPolicy,
            SchedulerOverhead = sw.Elapsed - cpuSw.Elapsed
        };

        _metricsReporter.Report(singleBackendMetrics);
        _partitionPolicy.Update(singleBackendMetrics);
    }

    /// <summary>
    /// Attempts a GPU re-probe by running normal hybrid dispatch. If GPU succeeds,
    /// hybrid mode is restored. If GPU fails again, stays CPU-only and resets the counter.
    /// </summary>
    private async Task EvaluateWithReprobeAsync(
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        CancellationToken cancellationToken)
    {
        _gpuAvailable = true; // Tentatively restore GPU for the hybrid dispatch attempt

        try
        {
            await EvaluateHybridAsync(genomes, setFitness, cancellationToken);

            // If we get here without _gpuAvailable being set to false by the catch block
            // in EvaluateHybridAsync, the re-probe succeeded.
            if (_gpuAvailable)
            {
                _logger.LogInformation(
                    "GPU re-probe succeeded at generation {Generation}. Restoring hybrid evaluation mode.",
                    _generation);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // EvaluateHybridAsync already handled the GPU failure (rerouted to CPU,
            // set _gpuAvailable = false, reset counter). The re-probe warning is
            // logged below. No need to re-throw since genomes were evaluated.
        }

        if (!_gpuAvailable)
        {
            _logger.LogWarning(
                "GPU re-probe failed at generation {Generation}. Continuing CPU-only evaluation.",
                _generation);
        }
    }

    /// <summary>
    /// Core hybrid evaluation: partitions genomes, dispatches CPU and GPU concurrently,
    /// handles GPU failures with CPU fallback.
    /// </summary>
    private async Task EvaluateHybridAsync(
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        CancellationToken cancellationToken)
    {
        // Build original indices array [0, 1, 2, ..., count-1]
        var overheadSw = Stopwatch.StartNew();
        var count = genomes.Count;
        var originalIndices = new int[count];
        for (var i = 0; i < count; i++)
        {
            originalIndices[i] = i;
        }

        // Partition genomes
        var partition = _partitionPolicy.Partition(genomes, originalIndices);
        overheadSw.Stop();

        // R-004: Index-remapped setFitness callbacks
        Action<int, double> cpuSetFitness = (localIndex, fitness) =>
            setFitness(partition.CpuIndices[localIndex], fitness);
        Action<int, double> gpuSetFitness = (localIndex, fitness) =>
            setFitness(partition.GpuIndices[localIndex], fitness);

        // Dispatch concurrently
        var cpuStopwatch = new Stopwatch();
        var gpuStopwatch = new Stopwatch();

        FallbackEventInfo? fallbackEvent = null;

        var cpuTask = RunBackendAsync(_cpuEvaluator, partition.CpuGenomes, cpuSetFitness, cpuStopwatch, cancellationToken);

        Task gpuTask;
        if (partition.GpuGenomes.Count > 0)
        {
            gpuTask = RunBackendAsync(_gpuEvaluator, partition.GpuGenomes, gpuSetFitness, gpuStopwatch, cancellationToken);
        }
        else
        {
            gpuTask = Task.CompletedTask;
        }

        try
        {
            await Task.WhenAll(cpuTask, gpuTask);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // R-003: GPU failure handling — catch all exceptions except OperationCanceledException.
            if (gpuTask.IsFaulted)
            {
                _logger.LogWarning(
                    ex,
                    "GPU evaluation failed for generation {Generation}. Rerouting {GenomeCount} genomes to CPU.",
                    _generation,
                    partition.GpuGenomes.Count);

                // FR-009: Mark GPU unavailable and reset re-probe counter.
                _gpuAvailable = false;
                _generationsSinceGpuFailure = 0;

                fallbackEvent = new FallbackEventInfo(
                    DateTimeOffset.UtcNow,
                    ex.Message,
                    partition.GpuGenomes.Count);

                // FR-007: Reroute GPU genomes to CPU for current generation.
                await _cpuEvaluator.EvaluateAsync(
                    partition.GpuGenomes,
                    gpuSetFitness,
                    cancellationToken);

                // Wait for CPU task if it hasn't finished
                await cpuTask;
            }
            else
            {
                // CPU task failed — propagate
                throw;
            }
        }

        // Resume overhead timing for metrics creation
        overheadSw.Start();

        var cpuLatency = cpuStopwatch.Elapsed;
        var gpuLatency = gpuStopwatch.Elapsed;

        var metrics = new SchedulingMetrics
        {
            Generation = _generation,
            CpuGenomeCount = partition.CpuGenomes.Count,
            GpuGenomeCount = partition.GpuGenomes.Count,
            CpuThroughput = partition.CpuGenomes.Count / Math.Max(cpuLatency.TotalSeconds, double.Epsilon),
            GpuThroughput = partition.GpuGenomes.Count > 0
                ? partition.GpuGenomes.Count / Math.Max(gpuLatency.TotalSeconds, double.Epsilon)
                : 0,
            CpuLatency = cpuLatency,
            GpuLatency = gpuLatency,
            SplitRatio = (double)partition.GpuGenomes.Count / count,
            ActivePolicy = _options.SplitPolicy,
            FallbackEvent = fallbackEvent,
            SchedulerOverhead = overheadSw.Elapsed
        };

        overheadSw.Stop();

        _metricsReporter.Report(metrics);
        _partitionPolicy.Update(metrics);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        (_cpuEvaluator as IDisposable)?.Dispose();
        (_gpuEvaluator as IDisposable)?.Dispose();
    }

    private static async Task RunBackendAsync(
        IBatchEvaluator evaluator,
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        if (genomes.Count == 0)
        {
            return;
        }

        stopwatch.Start();
        try
        {
            await evaluator.EvaluateAsync(genomes, setFitness, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
        }
    }
}
