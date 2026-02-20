using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;

namespace NeatSharp.Gpu.Scheduling;

/// <summary>
/// Partition policy that routes complex genomes (high node/connection count) to GPU
/// and simple genomes to CPU using a weighted linear cost model.
/// </summary>
/// <remarks>
/// <para>
/// The cost formula is:
/// <code>
/// cost = NodeWeight * genome.NodeCount + ConnectionWeight * genome.ConnectionCount
/// </code>
/// </para>
/// <para>
/// Genomes are sorted by cost descending, and the top <c>gpuCount</c> (highest cost)
/// are assigned to GPU while the remainder go to CPU. The split count uses the same
/// formula as <see cref="StaticPartitionPolicy"/>:
/// <code>
/// cpuCount = (int)((1.0 - gpuFraction) * count)
/// gpuCount = count - cpuCount
/// </code>
/// </para>
/// <para>
/// This policy is stateless — <see cref="Update"/> is a no-op since partitioning
/// is based solely on genome structure, not runtime performance.
/// </para>
/// </remarks>
internal sealed class CostBasedPartitionPolicy : IPartitionPolicy
{
    private readonly double _gpuFraction;
    private readonly CostModelOptions _costModel;

    /// <summary>
    /// Initializes a new <see cref="CostBasedPartitionPolicy"/> with the given GPU fraction
    /// and cost model configuration.
    /// </summary>
    /// <param name="gpuFraction">
    /// Fraction of genomes to assign to the GPU backend (0.0 = all CPU, 1.0 = all GPU).
    /// </param>
    /// <param name="costModel">
    /// Cost model weights for computing genome complexity cost.
    /// </param>
    public CostBasedPartitionPolicy(double gpuFraction, CostModelOptions costModel)
    {
        _gpuFraction = gpuFraction;
        _costModel = costModel;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Computes cost per genome, sorts by cost descending, assigns highest-cost genomes
    /// to GPU and lowest-cost to CPU. Original indices are remapped through the sort.
    /// </remarks>
    public PartitionResult Partition(IReadOnlyList<IGenome> genomes, int[] originalIndices)
    {
        var count = genomes.Count;
        if (count == 0)
        {
            return new PartitionResult(Array.Empty<IGenome>(), [], Array.Empty<IGenome>(), []);
        }

        var cpuCount = (int)((1.0 - _gpuFraction) * count);
        var gpuCount = count - cpuCount;

        // Build cost-index pairs and sort by cost descending
        var indexed = new (double Cost, int Index)[count];
        for (var i = 0; i < count; i++)
        {
            var genome = genomes[i];
            indexed[i] = (
                _costModel.NodeWeight * genome.NodeCount + _costModel.ConnectionWeight * genome.ConnectionCount,
                i
            );
        }

        // Sort descending by cost — highest cost first (goes to GPU)
        Array.Sort(indexed, (a, b) => b.Cost.CompareTo(a.Cost));

        // First gpuCount entries (highest cost) go to GPU
        var gpuGenomes = new IGenome[gpuCount];
        var gpuIndices = new int[gpuCount];
        for (var i = 0; i < gpuCount; i++)
        {
            gpuGenomes[i] = genomes[indexed[i].Index];
            gpuIndices[i] = originalIndices[indexed[i].Index];
        }

        // Remaining go to CPU
        var cpuGenomes = new IGenome[cpuCount];
        var cpuIndices = new int[cpuCount];
        for (var i = 0; i < cpuCount; i++)
        {
            cpuGenomes[i] = genomes[indexed[gpuCount + i].Index];
            cpuIndices[i] = originalIndices[indexed[gpuCount + i].Index];
        }

        return new PartitionResult(cpuGenomes, cpuIndices, gpuGenomes, gpuIndices);
    }

    /// <inheritdoc />
    /// <remarks>No-op for cost-based policy — partitioning is based solely on genome structure.</remarks>
    public void Update(SchedulingMetrics metrics)
    {
        // Cost-based policy is stateless — no adaptation needed.
    }
}
