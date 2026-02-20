using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;

namespace NeatSharp.Gpu.Scheduling;

/// <summary>
/// Partition policy that dynamically adjusts the CPU/GPU split ratio each generation
/// using a PID controller targeting zero idle-time difference between backends.
/// </summary>
/// <remarks>
/// <para>
/// The error signal fed to the PID controller is:
/// <c>error = (cpuLatency - gpuLatency) / max(cpuLatency, gpuLatency)</c>
/// </para>
/// <list type="bullet">
///   <item>Positive error → CPU is slower → increase GPU fraction.</item>
///   <item>Negative error → GPU is slower → decrease GPU fraction.</item>
///   <item>Zero error → balanced → no change.</item>
/// </list>
/// <para>
/// The split algorithm is identical to <see cref="StaticPartitionPolicy"/> but uses
/// the PID-controlled GPU fraction instead of a fixed value. This class is self-contained
/// and does not delegate to <see cref="StaticPartitionPolicy"/>.
/// </para>
/// </remarks>
internal sealed class AdaptivePartitionPolicy : IPartitionPolicy
{
    private readonly PidController _pidController;

    /// <summary>
    /// Initializes a new <see cref="AdaptivePartitionPolicy"/> with the specified PID options.
    /// </summary>
    /// <param name="options">PID controller configuration (gains and initial GPU fraction).</param>
    public AdaptivePartitionPolicy(AdaptivePidOptions options)
    {
        _pidController = new PidController(
            options.Kp,
            options.Ki,
            options.Kd,
            options.InitialGpuFraction);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Performs a count-based split using the PID-controlled GPU fraction.
    /// First <c>cpuCount</c> genomes (by position) go to CPU; the rest go to GPU.
    /// </remarks>
    public PartitionResult Partition(IReadOnlyList<IGenome> genomes, int[] originalIndices)
    {
        var count = genomes.Count;
        var cpuCount = (int)((1.0 - _pidController.GpuFraction) * count);
        var gpuCount = count - cpuCount;

        var cpuGenomes = new IGenome[cpuCount];
        var cpuIndices = new int[cpuCount];
        for (var i = 0; i < cpuCount; i++)
        {
            cpuGenomes[i] = genomes[i];
            cpuIndices[i] = originalIndices[i];
        }

        var gpuGenomes = new IGenome[gpuCount];
        var gpuIndices = new int[gpuCount];
        for (var i = 0; i < gpuCount; i++)
        {
            gpuGenomes[i] = genomes[cpuCount + i];
            gpuIndices[i] = originalIndices[cpuCount + i];
        }

        return new PartitionResult(cpuGenomes, cpuIndices, gpuGenomes, gpuIndices);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Computes the idle-time error from the metrics latencies and feeds it to the PID controller.
    /// Skips the update when both latencies are zero (no meaningful signal).
    /// </remarks>
    public void Update(SchedulingMetrics metrics)
    {
        var maxLatency = Math.Max(metrics.CpuLatency.TotalSeconds, metrics.GpuLatency.TotalSeconds);
        if (maxLatency <= 0)
        {
            return;
        }

        var error = (metrics.CpuLatency.TotalSeconds - metrics.GpuLatency.TotalSeconds) / maxLatency;
        _pidController.Compute(error);
    }
}
