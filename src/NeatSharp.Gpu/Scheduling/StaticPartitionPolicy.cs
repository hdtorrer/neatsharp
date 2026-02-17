using NeatSharp.Genetics;

namespace NeatSharp.Gpu.Scheduling;

/// <summary>
/// Partition policy that assigns a fixed fraction of genomes to GPU
/// and the remainder to CPU. Deterministic and stateless.
/// </summary>
/// <remarks>
/// The split is computed as:
/// <code>
/// cpuCount = (int)((1.0 - gpuFraction) * count)
/// gpuCount = count - cpuCount
/// </code>
/// The first <c>cpuCount</c> genomes (by position) go to CPU; the rest go to GPU.
/// <see cref="Update"/> is a no-op since the fraction is fixed.
/// </remarks>
internal sealed class StaticPartitionPolicy : IPartitionPolicy
{
    private readonly double _gpuFraction;

    /// <summary>
    /// Initializes a new <see cref="StaticPartitionPolicy"/> with the given GPU fraction.
    /// </summary>
    /// <param name="gpuFraction">
    /// Fraction of genomes to assign to the GPU backend (0.0 = all CPU, 1.0 = all GPU).
    /// </param>
    public StaticPartitionPolicy(double gpuFraction)
    {
        _gpuFraction = gpuFraction;
    }

    /// <inheritdoc />
    public PartitionResult Partition(IReadOnlyList<IGenome> genomes, int[] originalIndices)
    {
        var count = genomes.Count;
        var cpuCount = (int)((1.0 - _gpuFraction) * count);
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
    /// <remarks>No-op for static policy — the fraction is fixed at construction time.</remarks>
    public void Update(SchedulingMetrics metrics)
    {
        // Static policy does not adapt; intentionally empty.
    }
}
