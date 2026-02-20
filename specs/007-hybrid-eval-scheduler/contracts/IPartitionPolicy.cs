// CONTRACT — Design-time reference only. Not compiled.
// Actual implementation will be in src/NeatSharp.Gpu/Scheduling/IPartitionPolicy.cs

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
    PartitionResult Partition(IReadOnlyList<IGenome> genomes, int[] originalIndices);

    /// <summary>
    /// Feeds back metrics from a completed generation to inform the next partition.
    /// Called after each generation's evaluation completes.
    /// </summary>
    /// <param name="metrics">The scheduling metrics from the completed generation.</param>
    void Update(SchedulingMetrics metrics);
}

/// <summary>
/// Result of partitioning a population into CPU and GPU evaluation groups.
/// Both partitions are disjoint and their union equals the original population.
/// </summary>
/// <param name="CpuGenomes">Genomes assigned to CPU evaluation.</param>
/// <param name="CpuIndices">Original indices of CPU genomes (for setFitness remapping).</param>
/// <param name="GpuGenomes">Genomes assigned to GPU evaluation.</param>
/// <param name="GpuIndices">Original indices of GPU genomes (for setFitness remapping).</param>
public readonly record struct PartitionResult(
    IReadOnlyList<IGenome> CpuGenomes,
    int[] CpuIndices,
    IReadOnlyList<IGenome> GpuGenomes,
    int[] GpuIndices);
