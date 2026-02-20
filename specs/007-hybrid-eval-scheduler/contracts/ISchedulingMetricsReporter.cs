// CONTRACT — Design-time reference only. Not compiled.
// Actual implementation will be in src/NeatSharp.Gpu/Scheduling/ISchedulingMetricsReporter.cs

namespace NeatSharp.Gpu.Scheduling;

/// <summary>
/// Interface for receiving per-generation scheduling metrics from the hybrid evaluator.
/// </summary>
/// <remarks>
/// The default implementation (<c>LoggingMetricsReporter</c>) writes metrics to
/// <see cref="Microsoft.Extensions.Logging.ILogger"/> at Information level, with
/// fallback events at Warning level.
///
/// Users can provide custom implementations for monitoring, dashboards, or
/// programmatic analysis of scheduling behavior.
///
/// Implementations MUST be thread-safe (may be called from different threads
/// across generations, though not concurrently for the same generation).
/// </remarks>
public interface ISchedulingMetricsReporter
{
    /// <summary>
    /// Reports scheduling metrics for a completed generation.
    /// Called exactly once per generation after evaluation completes.
    /// </summary>
    /// <param name="metrics">Immutable metrics for the completed generation.</param>
    void Report(SchedulingMetrics metrics);
}
