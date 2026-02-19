using NeatSharp.Genetics;

namespace NeatSharp.Gpu.Scheduling;

/// <summary>
/// Defines the contract for partitioning a genome population across
/// CPU and GPU evaluation backends.
/// </summary>
public interface IPartitionPolicy
{
    /// <summary>
    /// Partitions a population of genomes into CPU and GPU evaluation groups.
    /// </summary>
    /// <param name="genomes">The full population to partition.</param>
    /// <param name="originalIndices">
    /// The original indices of each genome in the caller's list.
    /// Used to map partition-local indices back to global indices for setFitness callbacks.
    /// When called from HybridBatchEvaluator, this is [0, 1, 2, ..., genomes.Count - 1].
    /// </param>
    /// <returns>A <see cref="PartitionResult"/> with disjoint CPU and GPU genome sets.</returns>
    public PartitionResult Partition(IReadOnlyList<IGenome> genomes, int[] originalIndices);

    /// <summary>
    /// Feeds back metrics from a completed generation to inform the next partition.
    /// Called after each generation's evaluation completes.
    /// </summary>
    /// <param name="metrics">The scheduling metrics from the completed generation.</param>
    public void Update(SchedulingMetrics metrics);
}
