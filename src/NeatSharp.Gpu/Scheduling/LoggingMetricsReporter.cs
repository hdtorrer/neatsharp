using Microsoft.Extensions.Logging;

namespace NeatSharp.Gpu.Scheduling;

/// <summary>
/// Default <see cref="ISchedulingMetricsReporter"/> that logs per-generation scheduling
/// metrics via <see cref="ILogger"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item>Per-generation summary: logged at <see cref="LogLevel.Information"/>.</item>
///   <item>GPU fallback events: logged at <see cref="LogLevel.Warning"/>.</item>
/// </list>
/// Registered as the default reporter by <c>AddNeatSharpHybrid()</c>. Users can
/// override by registering their own <see cref="ISchedulingMetricsReporter"/> before
/// calling <c>AddNeatSharpHybrid()</c>.
/// </remarks>
internal sealed class LoggingMetricsReporter : ISchedulingMetricsReporter
{
    private readonly ILogger<LoggingMetricsReporter> _logger;

    public LoggingMetricsReporter(ILogger<LoggingMetricsReporter> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public void Report(SchedulingMetrics metrics)
    {
        // Log per-generation summary at Information level
        _logger.LogInformation(
            "Generation {Generation}: CPU={CpuCount} ({CpuThroughput:F0} g/s, {CpuLatency}ms), GPU={GpuCount} ({GpuThroughput:F0} g/s, {GpuLatency}ms), Split={SplitRatio:P0}, Policy={Policy}, Overhead={Overhead}ms",
            metrics.Generation,
            metrics.CpuGenomeCount,
            metrics.CpuThroughput,
            metrics.CpuLatency.TotalMilliseconds,
            metrics.GpuGenomeCount,
            metrics.GpuThroughput,
            metrics.GpuLatency.TotalMilliseconds,
            metrics.SplitRatio,
            metrics.ActivePolicy,
            metrics.SchedulerOverhead.TotalMilliseconds);

        // Log fallback events at Warning level
        if (metrics.FallbackEvent is { } fallback)
        {
            _logger.LogWarning(
                "GPU fallback at generation {Generation}: {Reason}, {GenomesRerouted} genomes rerouted at {Timestamp}",
                metrics.Generation,
                fallback.FailureReason,
                fallback.GenomesRerouted,
                fallback.Timestamp);
        }
    }
}
