using NeatSharp.Genetics;

namespace NeatSharp.Gpu.Scheduling;

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
