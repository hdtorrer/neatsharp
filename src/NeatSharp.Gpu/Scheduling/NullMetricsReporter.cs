namespace NeatSharp.Gpu.Scheduling;

/// <summary>
/// No-op implementation of <see cref="ISchedulingMetricsReporter"/>.
/// Used as the default when no custom reporter is registered.
/// </summary>
internal sealed class NullMetricsReporter : ISchedulingMetricsReporter
{
    /// <inheritdoc />
    public void Report(SchedulingMetrics metrics)
    {
        // Intentionally empty — metrics are discarded.
    }
}
