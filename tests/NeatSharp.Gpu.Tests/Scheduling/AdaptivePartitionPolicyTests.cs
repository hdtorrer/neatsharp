using FluentAssertions;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Scheduling;
using NeatSharp.Gpu.Tests.TestDoubles;
using Xunit;

namespace NeatSharp.Gpu.Tests.Scheduling;

public class AdaptivePartitionPolicyTests
{
    private static AdaptivePidOptions DefaultOptions() => new()
    {
        Kp = 0.5,
        Ki = 0.1,
        Kd = 0.05,
        InitialGpuFraction = 0.5
    };

    private static List<IGenome> CreateGenomes(int count)
    {
        var genomes = new List<IGenome>(count);
        for (var i = 0; i < count; i++)
        {
            genomes.Add(new StubGenome(nodeCount: 5, connectionCount: 3));
        }
        return genomes;
    }

    private static int[] CreateIndices(int count)
    {
        var indices = new int[count];
        for (var i = 0; i < count; i++)
        {
            indices[i] = i;
        }
        return indices;
    }

    private static SchedulingMetrics CreateMetrics(
        TimeSpan cpuLatency,
        TimeSpan gpuLatency,
        int generation = 1,
        int cpuCount = 5,
        int gpuCount = 5) => new()
    {
        Generation = generation,
        CpuGenomeCount = cpuCount,
        GpuGenomeCount = gpuCount,
        CpuThroughput = cpuCount / Math.Max(cpuLatency.TotalSeconds, 0.001),
        GpuThroughput = gpuCount / Math.Max(gpuLatency.TotalSeconds, 0.001),
        CpuLatency = cpuLatency,
        GpuLatency = gpuLatency,
        SplitRatio = 0.5,
        ActivePolicy = SplitPolicyType.Adaptive,
        SchedulerOverhead = TimeSpan.FromMilliseconds(1)
    };

    // --- Initial GPU fraction matches options ---

    [Fact]
    public void Partition_InitialState_UsesDefaultFraction()
    {
        var options = DefaultOptions();
        var policy = new AdaptivePartitionPolicy(options);
        var genomes = CreateGenomes(10);
        var indices = CreateIndices(10);

        var result = policy.Partition(genomes, indices);

        // With initial 0.5 fraction: cpuCount = (int)(0.5 * 10) = 5
        result.CpuGenomes.Count.Should().Be(5);
        result.GpuGenomes.Count.Should().Be(5);
    }

    [Fact]
    public void Partition_CustomInitialFraction_ReflectedInSplit()
    {
        var options = new AdaptivePidOptions { InitialGpuFraction = 0.7 };
        var policy = new AdaptivePartitionPolicy(options);
        var genomes = CreateGenomes(10);
        var indices = CreateIndices(10);

        var result = policy.Partition(genomes, indices);

        // cpuCount = (int)((1.0 - 0.7) * 10) = (int)(3.0) = 3
        result.CpuGenomes.Count.Should().Be(3);
        result.GpuGenomes.Count.Should().Be(7);
    }

    // --- Update feeds PID controller with idle-time error ---

    [Fact]
    public void Update_CpuSlowerThanGpu_IncreasesGpuFraction()
    {
        var options = DefaultOptions();
        var policy = new AdaptivePartitionPolicy(options);

        // CPU took 200ms, GPU took 100ms → error = (200-100)/200 = 0.5 → increase GPU
        var metrics = CreateMetrics(
            cpuLatency: TimeSpan.FromMilliseconds(200),
            gpuLatency: TimeSpan.FromMilliseconds(100));

        policy.Update(metrics);

        var genomes = CreateGenomes(10);
        var indices = CreateIndices(10);
        var result = policy.Partition(genomes, indices);

        // GPU fraction should have increased beyond 0.5
        result.GpuGenomes.Count.Should().BeGreaterThan(5);
    }

    [Fact]
    public void Update_GpuSlowerThanCpu_DecreasesGpuFraction()
    {
        var options = DefaultOptions();
        var policy = new AdaptivePartitionPolicy(options);

        // CPU took 100ms, GPU took 200ms → error = (100-200)/200 = -0.5 → decrease GPU
        var metrics = CreateMetrics(
            cpuLatency: TimeSpan.FromMilliseconds(100),
            gpuLatency: TimeSpan.FromMilliseconds(200));

        policy.Update(metrics);

        var genomes = CreateGenomes(10);
        var indices = CreateIndices(10);
        var result = policy.Partition(genomes, indices);

        // GPU fraction should have decreased below 0.5
        result.GpuGenomes.Count.Should().BeLessThan(5);
    }

    [Fact]
    public void Update_EqualLatencies_NoChange()
    {
        var options = DefaultOptions();
        var policy = new AdaptivePartitionPolicy(options);

        // CPU and GPU took the same time → error = 0 → no change
        var metrics = CreateMetrics(
            cpuLatency: TimeSpan.FromMilliseconds(100),
            gpuLatency: TimeSpan.FromMilliseconds(100));

        policy.Update(metrics);

        var genomes = CreateGenomes(10);
        var indices = CreateIndices(10);
        var result = policy.Partition(genomes, indices);

        result.CpuGenomes.Count.Should().Be(5);
        result.GpuGenomes.Count.Should().Be(5);
    }

    [Fact]
    public void Update_ZeroMaxLatency_NoChange()
    {
        var options = DefaultOptions();
        var policy = new AdaptivePartitionPolicy(options);

        // Both latencies are zero → maxLatency = 0 → should not change
        var metrics = CreateMetrics(
            cpuLatency: TimeSpan.Zero,
            gpuLatency: TimeSpan.Zero);

        policy.Update(metrics);

        var genomes = CreateGenomes(10);
        var indices = CreateIndices(10);
        var result = policy.Partition(genomes, indices);

        result.CpuGenomes.Count.Should().Be(5);
        result.GpuGenomes.Count.Should().Be(5);
    }

    // --- Partition uses current PID-controlled fraction ---

    [Fact]
    public void Partition_AfterUpdate_UsesPidControlledFraction()
    {
        var options = DefaultOptions();
        var policy = new AdaptivePartitionPolicy(options);

        // Push GPU fraction up significantly
        for (var i = 0; i < 5; i++)
        {
            policy.Update(CreateMetrics(
                cpuLatency: TimeSpan.FromMilliseconds(300),
                gpuLatency: TimeSpan.FromMilliseconds(100)));
        }

        var genomes = CreateGenomes(20);
        var indices = CreateIndices(20);
        var result = policy.Partition(genomes, indices);

        // GPU fraction should be well above 0.5 after consistent CPU-slower signals
        result.GpuGenomes.Count.Should().BeGreaterThan(10);
    }

    // --- Convergence behavior over multiple updates ---

    [Fact]
    public void Update_ThirtyConsistentMetrics_FractionStabilizes()
    {
        // Use lower gains appropriate for this closed-loop simulation.
        // The default gains (Kp=0.5) are tuned for real workloads where error
        // magnitudes are smaller; in this simulation with sharp error signals,
        // lower gains are needed to avoid oscillation.
        var options = new AdaptivePidOptions
        {
            Kp = 0.15,
            Ki = 0.02,
            Kd = 0.03,
            InitialGpuFraction = 0.5
        };
        var policy = new AdaptivePartitionPolicy(options);
        var fractions = new double[30];

        const int populationSize = 1000;

        for (var step = 0; step < 30; step++)
        {
            var genomes = CreateGenomes(populationSize);
            var indices = CreateIndices(populationSize);
            var result = policy.Partition(genomes, indices);

            var gpuCount = result.GpuGenomes.Count;
            var cpuCount = result.CpuGenomes.Count;

            // Simulate latencies proportional to genome count / backend speed
            // CPU processes at 100 genomes/sec, GPU processes at 150 genomes/sec
            // Equilibrium: gpuFraction ≈ 0.6 (400 CPU @ 4s, 600 GPU @ 4s)
            var cpuLatency = cpuCount > 0 ? TimeSpan.FromSeconds(cpuCount / 100.0) : TimeSpan.Zero;
            var gpuLatency = gpuCount > 0 ? TimeSpan.FromSeconds(gpuCount / 150.0) : TimeSpan.Zero;

            policy.Update(CreateMetrics(cpuLatency, gpuLatency, generation: step + 1, cpuCount, gpuCount));

            fractions[step] = (double)gpuCount / populationSize;
        }

        // Last 5 fractions should have < 5 percentage points variance
        var last5 = fractions[25..30];
        var maxVariance = last5.Max() - last5.Min();
        maxVariance.Should().BeLessThan(0.05,
            "GPU fraction should stabilize over the last 5 steps of 30 consistent updates");
    }

    // --- Index correctness after adaptive changes ---

    [Fact]
    public void Partition_AfterAdaptiveChange_IndicesCoverAllGenomes()
    {
        var options = DefaultOptions();
        var policy = new AdaptivePartitionPolicy(options);

        // Push fraction away from 0.5
        policy.Update(CreateMetrics(
            cpuLatency: TimeSpan.FromMilliseconds(200),
            gpuLatency: TimeSpan.FromMilliseconds(100)));

        var genomes = CreateGenomes(10);
        var indices = CreateIndices(10);
        var result = policy.Partition(genomes, indices);

        var allIndices = result.CpuIndices.Concat(result.GpuIndices).OrderBy(i => i).ToArray();
        allIndices.Should().BeEquivalentTo(Enumerable.Range(0, 10));
    }

    [Fact]
    public void Partition_AfterAdaptiveChange_NoDuplicateIndices()
    {
        var options = DefaultOptions();
        var policy = new AdaptivePartitionPolicy(options);

        policy.Update(CreateMetrics(
            cpuLatency: TimeSpan.FromMilliseconds(150),
            gpuLatency: TimeSpan.FromMilliseconds(100)));

        var genomes = CreateGenomes(10);
        var indices = CreateIndices(10);
        var result = policy.Partition(genomes, indices);

        var allIndices = result.CpuIndices.Concat(result.GpuIndices).ToArray();
        allIndices.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Partition_AfterAdaptiveChange_GenomesMatchIndices()
    {
        var options = DefaultOptions();
        var policy = new AdaptivePartitionPolicy(options);

        policy.Update(CreateMetrics(
            cpuLatency: TimeSpan.FromMilliseconds(200),
            gpuLatency: TimeSpan.FromMilliseconds(50)));

        var genomes = CreateGenomes(10);
        var indices = CreateIndices(10);
        var result = policy.Partition(genomes, indices);

        for (var i = 0; i < result.CpuGenomes.Count; i++)
        {
            result.CpuGenomes[i].Should().BeSameAs(genomes[result.CpuIndices[i]]);
        }

        for (var i = 0; i < result.GpuGenomes.Count; i++)
        {
            result.GpuGenomes[i].Should().BeSameAs(genomes[result.GpuIndices[i]]);
        }
    }

    [Fact]
    public void Partition_CpuIndicesAreFirstPortion_GpuIndicesAreRemainder()
    {
        var options = DefaultOptions();
        var policy = new AdaptivePartitionPolicy(options);

        var genomes = CreateGenomes(10);
        var indices = CreateIndices(10);
        var result = policy.Partition(genomes, indices);

        // CPU gets first cpuCount indices, GPU gets the rest (same as static)
        var cpuCount = result.CpuGenomes.Count;
        result.CpuIndices.Should().BeEquivalentTo(Enumerable.Range(0, cpuCount));
        result.GpuIndices.Should().BeEquivalentTo(Enumerable.Range(cpuCount, 10 - cpuCount));
    }

    [Fact]
    public void Partition_WithCustomOriginalIndices_MapsBackCorrectly()
    {
        var options = DefaultOptions();
        var policy = new AdaptivePartitionPolicy(options);

        var genomes = CreateGenomes(4);
        var indices = new[] { 10, 20, 30, 40 };

        var result = policy.Partition(genomes, indices);

        // With 0.5 initial: cpuCount = (int)(0.5*4) = 2
        result.CpuIndices.Should().BeEquivalentTo([10, 20]);
        result.GpuIndices.Should().BeEquivalentTo([30, 40]);
    }

    // --- Empty population ---

    [Fact]
    public void Partition_EmptyPopulation_ReturnsBothEmpty()
    {
        var options = DefaultOptions();
        var policy = new AdaptivePartitionPolicy(options);
        var genomes = CreateGenomes(0);
        var indices = CreateIndices(0);

        var result = policy.Partition(genomes, indices);

        result.CpuGenomes.Count.Should().Be(0);
        result.GpuGenomes.Count.Should().Be(0);
        result.CpuIndices.Should().BeEmpty();
        result.GpuIndices.Should().BeEmpty();
    }
}
