using FluentAssertions;
using NeatSharp.Genetics;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Scheduling;
using NeatSharp.Gpu.Tests.TestDoubles;
using Xunit;

namespace NeatSharp.Gpu.Tests.Scheduling;

public class CostBasedPartitionPolicyTests
{
    private static CostModelOptions DefaultCostModel => new() { NodeWeight = 1.0, ConnectionWeight = 1.0 };

    private static int[] CreateIndices(int count)
    {
        var indices = new int[count];
        for (var i = 0; i < count; i++)
        {
            indices[i] = i;
        }
        return indices;
    }

    // --- Cost formula correctness ---

    [Fact]
    public void Partition_CostFormulaApplied_HighestCostGenomesAssignedToGpu()
    {
        // Arrange: genome costs = 1*nodes + 1*connections
        // G0: 2+3=5, G1: 10+10=20, G2: 1+1=2, G3: 5+5=10
        var genomes = new List<IGenome>
        {
            new StubGenome(nodeCount: 2, connectionCount: 3),   // cost=5
            new StubGenome(nodeCount: 10, connectionCount: 10), // cost=20
            new StubGenome(nodeCount: 1, connectionCount: 1),   // cost=2
            new StubGenome(nodeCount: 5, connectionCount: 5)    // cost=10
        };
        var indices = CreateIndices(4);
        // gpuFraction=0.5 -> cpuCount=(int)(0.5*4)=2, gpuCount=2
        var policy = new CostBasedPartitionPolicy(gpuFraction: 0.5, DefaultCostModel);

        // Act
        var result = policy.Partition(genomes, indices);

        // Assert: top 2 by cost are G1(20) and G3(10) -> GPU
        result.GpuGenomes.Should().HaveCount(2);
        result.GpuGenomes.Should().Contain(genomes[1]); // cost=20
        result.GpuGenomes.Should().Contain(genomes[3]); // cost=10

        // Bottom 2 by cost are G0(5) and G2(2) -> CPU
        result.CpuGenomes.Should().HaveCount(2);
        result.CpuGenomes.Should().Contain(genomes[0]); // cost=5
        result.CpuGenomes.Should().Contain(genomes[2]); // cost=2
    }

    // --- Sorted by cost descending, highest to GPU ---

    [Fact]
    public void Partition_VaryingComplexity_HighestCostGenomesGoToGpu()
    {
        // Arrange: 5 genomes with distinct costs
        var genomes = new List<IGenome>
        {
            new StubGenome(nodeCount: 1, connectionCount: 0),   // cost=1
            new StubGenome(nodeCount: 100, connectionCount: 50), // cost=150
            new StubGenome(nodeCount: 10, connectionCount: 5),   // cost=15
            new StubGenome(nodeCount: 50, connectionCount: 25),  // cost=75
            new StubGenome(nodeCount: 5, connectionCount: 2)     // cost=7
        };
        var indices = CreateIndices(5);
        // gpuFraction=0.4 -> cpuCount=(int)(0.6*5)=3, gpuCount=2
        var policy = new CostBasedPartitionPolicy(gpuFraction: 0.4, DefaultCostModel);

        // Act
        var result = policy.Partition(genomes, indices);

        // Assert: top 2 by cost are G1(150) and G3(75) -> GPU
        result.GpuGenomes.Should().HaveCount(2);
        result.GpuGenomes.Should().Contain(genomes[1]); // cost=150
        result.GpuGenomes.Should().Contain(genomes[3]); // cost=75

        // Bottom 3 -> CPU
        result.CpuGenomes.Should().HaveCount(3);
        result.CpuGenomes.Should().Contain(genomes[0]); // cost=1
        result.CpuGenomes.Should().Contain(genomes[2]); // cost=15
        result.CpuGenomes.Should().Contain(genomes[4]); // cost=7
    }

    // --- Configurable NodeWeight/ConnectionWeight ---

    [Fact]
    public void Partition_CustomWeights_CostCalculationUsesConfiguredWeights()
    {
        // Arrange: NodeWeight=2, ConnectionWeight=0.5
        // G0: 2*10 + 0.5*100 = 70, G1: 2*100 + 0.5*10 = 205, G2: 2*50 + 0.5*50 = 125
        var costModel = new CostModelOptions { NodeWeight = 2.0, ConnectionWeight = 0.5 };
        var genomes = new List<IGenome>
        {
            new StubGenome(nodeCount: 10, connectionCount: 100), // cost=70
            new StubGenome(nodeCount: 100, connectionCount: 10), // cost=205
            new StubGenome(nodeCount: 50, connectionCount: 50)   // cost=125
        };
        var indices = CreateIndices(3);
        // gpuFraction=0.5 -> cpuCount=(int)(0.5*3)=1, gpuCount=2
        var policy = new CostBasedPartitionPolicy(gpuFraction: 0.5, costModel);

        // Act
        var result = policy.Partition(genomes, indices);

        // Assert: top 2 by cost are G1(205) and G2(125) -> GPU
        result.GpuGenomes.Should().HaveCount(2);
        result.GpuGenomes.Should().Contain(genomes[1]); // cost=205
        result.GpuGenomes.Should().Contain(genomes[2]); // cost=125

        // Bottom 1: G0(70) -> CPU
        result.CpuGenomes.Should().HaveCount(1);
        result.CpuGenomes.Should().Contain(genomes[0]); // cost=70
    }

    [Fact]
    public void Partition_ZeroNodeWeight_OnlyConnectionCountMatters()
    {
        // Arrange: NodeWeight=0, ConnectionWeight=1
        // G0: 0*100 + 1*1 = 1, G1: 0*1 + 1*100 = 100
        var costModel = new CostModelOptions { NodeWeight = 0.0, ConnectionWeight = 1.0 };
        var genomes = new List<IGenome>
        {
            new StubGenome(nodeCount: 100, connectionCount: 1),  // cost=1
            new StubGenome(nodeCount: 1, connectionCount: 100)   // cost=100
        };
        var indices = CreateIndices(2);
        // gpuFraction=0.5 -> cpuCount=(int)(0.5*2)=1, gpuCount=1
        var policy = new CostBasedPartitionPolicy(gpuFraction: 0.5, costModel);

        // Act
        var result = policy.Partition(genomes, indices);

        // Assert: G1(cost=100) -> GPU, G0(cost=1) -> CPU
        result.GpuGenomes.Should().HaveCount(1);
        result.GpuGenomes[0].Should().BeSameAs(genomes[1]);
        result.CpuGenomes.Should().HaveCount(1);
        result.CpuGenomes[0].Should().BeSameAs(genomes[0]);
    }

    // --- Uniform complexity degrades to even split ---

    [Fact]
    public void Partition_UniformComplexity_SplitEquivalentToStaticByCount()
    {
        // Arrange: all genomes have the same cost
        var genomes = new List<IGenome>();
        for (var i = 0; i < 10; i++)
        {
            genomes.Add(new StubGenome(nodeCount: 5, connectionCount: 3)); // cost=8 for all
        }
        var indices = CreateIndices(10);
        const double gpuFraction = 0.3;
        var costPolicy = new CostBasedPartitionPolicy(gpuFraction, DefaultCostModel);
        var staticPolicy = new StaticPartitionPolicy(gpuFraction);

        // Act
        var costResult = costPolicy.Partition(genomes, indices);
        var staticResult = staticPolicy.Partition(genomes, indices);

        // Assert: same split counts
        costResult.CpuGenomes.Count.Should().Be(staticResult.CpuGenomes.Count);
        costResult.GpuGenomes.Count.Should().Be(staticResult.GpuGenomes.Count);
    }

    // --- GPU fraction respected ---

    [Fact]
    public void Partition_ThirtyPercentGpuOnTenGenomes_ThreeHighestCostToGpu()
    {
        // Arrange: 10 genomes with increasing cost
        var genomes = new List<IGenome>();
        for (var i = 0; i < 10; i++)
        {
            genomes.Add(new StubGenome(nodeCount: (i + 1) * 10, connectionCount: 0));
            // costs: 10, 20, 30, 40, 50, 60, 70, 80, 90, 100
        }
        var indices = CreateIndices(10);
        // gpuFraction=0.3 -> cpuCount=(int)(0.7*10)=7, gpuCount=3
        var policy = new CostBasedPartitionPolicy(gpuFraction: 0.3, DefaultCostModel);

        // Act
        var result = policy.Partition(genomes, indices);

        // Assert: 3 highest cost (100, 90, 80) go to GPU
        result.GpuGenomes.Should().HaveCount(3);
        result.GpuGenomes.Should().Contain(genomes[9]); // cost=100
        result.GpuGenomes.Should().Contain(genomes[8]); // cost=90
        result.GpuGenomes.Should().Contain(genomes[7]); // cost=80

        // 7 lowest cost go to CPU
        result.CpuGenomes.Should().HaveCount(7);
        for (var i = 0; i < 7; i++)
        {
            result.CpuGenomes.Should().Contain(genomes[i]);
        }
    }

    // --- Update is no-op ---

    [Fact]
    public void Update_CalledMultipleTimes_DoesNotChangePartitionResults()
    {
        var genomes = new List<IGenome>
        {
            new StubGenome(nodeCount: 10, connectionCount: 5),
            new StubGenome(nodeCount: 1, connectionCount: 1),
            new StubGenome(nodeCount: 50, connectionCount: 25),
            new StubGenome(nodeCount: 5, connectionCount: 3)
        };
        var indices = CreateIndices(4);
        var policy = new CostBasedPartitionPolicy(gpuFraction: 0.5, DefaultCostModel);

        var resultBefore = policy.Partition(genomes, indices);

        // Call Update with arbitrary metrics
        var metrics = new SchedulingMetrics
        {
            Generation = 1,
            CpuGenomeCount = 2,
            GpuGenomeCount = 2,
            CpuThroughput = 100.0,
            GpuThroughput = 500.0,
            CpuLatency = TimeSpan.FromMilliseconds(30),
            GpuLatency = TimeSpan.FromMilliseconds(14),
            SplitRatio = 0.5,
            ActivePolicy = SplitPolicyType.CostBased,
            SchedulerOverhead = TimeSpan.FromMilliseconds(1)
        };
        policy.Update(metrics);
        policy.Update(metrics);
        policy.Update(metrics);

        var resultAfter = policy.Partition(genomes, indices);

        resultAfter.CpuGenomes.Count.Should().Be(resultBefore.CpuGenomes.Count);
        resultAfter.GpuGenomes.Count.Should().Be(resultBefore.GpuGenomes.Count);
        resultAfter.CpuIndices.Should().BeEquivalentTo(resultBefore.CpuIndices);
        resultAfter.GpuIndices.Should().BeEquivalentTo(resultBefore.GpuIndices);
    }

    // --- Index mapping correctness ---

    [Fact]
    public void Partition_IndicesCoverAllOriginalIndices_NoDuplicatesOrOmissions()
    {
        var genomes = new List<IGenome>
        {
            new StubGenome(nodeCount: 1, connectionCount: 0),
            new StubGenome(nodeCount: 100, connectionCount: 50),
            new StubGenome(nodeCount: 10, connectionCount: 5),
            new StubGenome(nodeCount: 50, connectionCount: 25),
            new StubGenome(nodeCount: 5, connectionCount: 2),
            new StubGenome(nodeCount: 75, connectionCount: 30)
        };
        var indices = CreateIndices(6);
        var policy = new CostBasedPartitionPolicy(gpuFraction: 0.5, DefaultCostModel);

        var result = policy.Partition(genomes, indices);

        var allIndices = result.CpuIndices.Concat(result.GpuIndices).OrderBy(i => i).ToArray();
        allIndices.Should().BeEquivalentTo(Enumerable.Range(0, 6));
        allIndices.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Partition_GenomesMatchTheirIndices_AfterSorting()
    {
        // Arrange: verify genomes and indices stay paired after sorting
        var genomes = new List<IGenome>
        {
            new StubGenome(nodeCount: 1, connectionCount: 0),    // cost=1
            new StubGenome(nodeCount: 100, connectionCount: 50), // cost=150
            new StubGenome(nodeCount: 10, connectionCount: 5),   // cost=15
            new StubGenome(nodeCount: 50, connectionCount: 25)   // cost=75
        };
        var indices = CreateIndices(4);
        // gpuFraction=0.5 -> cpuCount=2, gpuCount=2
        var policy = new CostBasedPartitionPolicy(gpuFraction: 0.5, DefaultCostModel);

        // Act
        var result = policy.Partition(genomes, indices);

        // Assert: each genome's index maps back to the correct original genome
        for (var i = 0; i < result.GpuGenomes.Count; i++)
        {
            result.GpuGenomes[i].Should().BeSameAs(genomes[result.GpuIndices[i]]);
        }
        for (var i = 0; i < result.CpuGenomes.Count; i++)
        {
            result.CpuGenomes[i].Should().BeSameAs(genomes[result.CpuIndices[i]]);
        }
    }

    // --- Empty population ---

    [Fact]
    public void Partition_EmptyPopulation_ReturnsBothEmpty()
    {
        var policy = new CostBasedPartitionPolicy(gpuFraction: 0.5, DefaultCostModel);
        var genomes = new List<IGenome>();
        var indices = Array.Empty<int>();

        var result = policy.Partition(genomes, indices);

        result.CpuGenomes.Count.Should().Be(0);
        result.GpuGenomes.Count.Should().Be(0);
        result.CpuIndices.Should().BeEmpty();
        result.GpuIndices.Should().BeEmpty();
    }

    // --- Single genome ---

    [Fact]
    public void Partition_SingleGenomeWithHighFraction_AssignsToGpu()
    {
        // gpuFraction=0.7 -> cpuCount=(int)(0.3*1)=0, gpuCount=1
        var policy = new CostBasedPartitionPolicy(gpuFraction: 0.7, DefaultCostModel);
        var genomes = new List<IGenome> { new StubGenome(nodeCount: 5, connectionCount: 3) };
        var indices = CreateIndices(1);

        var result = policy.Partition(genomes, indices);

        result.GpuGenomes.Should().HaveCount(1);
        result.CpuGenomes.Should().BeEmpty();
    }

    [Fact]
    public void Partition_SingleGenomeWithZeroFraction_AssignsToCpu()
    {
        // gpuFraction=0.0 -> cpuCount=(int)(1.0*1)=1, gpuCount=0
        var policy = new CostBasedPartitionPolicy(gpuFraction: 0.0, DefaultCostModel);
        var genomes = new List<IGenome> { new StubGenome(nodeCount: 5, connectionCount: 3) };
        var indices = CreateIndices(1);

        var result = policy.Partition(genomes, indices);

        result.CpuGenomes.Should().HaveCount(1);
        result.GpuGenomes.Should().BeEmpty();
    }

    // --- Custom original indices ---

    [Fact]
    public void Partition_WithCustomOriginalIndices_MapsBackCorrectlyThroughSorting()
    {
        // Arrange: non-sequential original indices simulating a subset of a larger population
        var genomes = new List<IGenome>
        {
            new StubGenome(nodeCount: 1, connectionCount: 0),   // cost=1, originalIndex=100
            new StubGenome(nodeCount: 50, connectionCount: 25),  // cost=75, originalIndex=200
            new StubGenome(nodeCount: 10, connectionCount: 5),   // cost=15, originalIndex=300
            new StubGenome(nodeCount: 100, connectionCount: 50)  // cost=150, originalIndex=400
        };
        var indices = new[] { 100, 200, 300, 400 };
        // gpuFraction=0.5 -> cpuCount=(int)(0.5*4)=2, gpuCount=2
        var policy = new CostBasedPartitionPolicy(gpuFraction: 0.5, DefaultCostModel);

        // Act
        var result = policy.Partition(genomes, indices);

        // Assert: highest cost genomes (G3=150, G1=75) go to GPU with their original indices
        result.GpuIndices.Should().Contain(400); // G3 original index
        result.GpuIndices.Should().Contain(200); // G1 original index

        // Lowest cost genomes (G2=15, G0=1) go to CPU with their original indices
        result.CpuIndices.Should().Contain(300); // G2 original index
        result.CpuIndices.Should().Contain(100); // G0 original index

        // All original indices are preserved
        var allIndices = result.CpuIndices.Concat(result.GpuIndices).OrderBy(i => i).ToArray();
        allIndices.Should().BeEquivalentTo(new[] { 100, 200, 300, 400 });
    }
}
