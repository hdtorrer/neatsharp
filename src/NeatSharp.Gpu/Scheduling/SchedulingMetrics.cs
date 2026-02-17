using NeatSharp.Gpu.Configuration;

namespace NeatSharp.Gpu.Scheduling;

/// <summary>
/// Per-generation metrics capturing hybrid scheduler performance.
/// Emitted after each generation's evaluation completes.
/// </summary>
/// <remarks>
/// Used by:
/// <list type="bullet">
///   <item>Adaptive partition policy (PID controller reads throughput/latency)</item>
///   <item>Operators (observability, diagnosis, convergence verification)</item>
///   <item>Logging (via <see cref="ISchedulingMetricsReporter"/>)</item>
/// </list>
/// </remarks>
public sealed record SchedulingMetrics
{
    /// <summary>Generation number (1-based).</summary>
    public required int Generation { get; init; }

    /// <summary>Number of genomes evaluated by the CPU backend.</summary>
    public required int CpuGenomeCount { get; init; }

    /// <summary>Number of genomes evaluated by the GPU backend.</summary>
    public required int GpuGenomeCount { get; init; }

    /// <summary>CPU evaluation throughput in genomes per second.</summary>
    public required double CpuThroughput { get; init; }

    /// <summary>GPU evaluation throughput in genomes per second.</summary>
    public required double GpuThroughput { get; init; }

    /// <summary>Wall-clock time for CPU evaluation.</summary>
    public required TimeSpan CpuLatency { get; init; }

    /// <summary>Wall-clock time for GPU evaluation.</summary>
    public required TimeSpan GpuLatency { get; init; }

    /// <summary>
    /// Current GPU fraction (0.0 = all CPU, 1.0 = all GPU).
    /// For adaptive policy, this is the PID-controlled value.
    /// For static policy, this is the configured fraction.
    /// </summary>
    public required double SplitRatio { get; init; }

    /// <summary>Which partitioning policy was active this generation.</summary>
    public required SplitPolicyType ActivePolicy { get; init; }

    /// <summary>
    /// GPU fallback event details, if a fallback occurred this generation.
    /// Null when no fallback occurred.
    /// </summary>
    public FallbackEventInfo? FallbackEvent { get; init; }

    /// <summary>
    /// Time spent in scheduler overhead (partitioning, dispatch setup, result merge).
    /// Excludes actual backend evaluation time.
    /// </summary>
    public required TimeSpan SchedulerOverhead { get; init; }
}
