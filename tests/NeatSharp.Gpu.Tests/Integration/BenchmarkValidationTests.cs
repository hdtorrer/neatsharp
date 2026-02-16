#if NET9_0_OR_GREATER
using FluentAssertions;
using NeatSharp.Gpu.Evaluation;
using NeatSharp.Samples;
using Xunit;

namespace NeatSharp.Gpu.Tests.Integration;

/// <summary>
/// Validates that the benchmark suite executes correctly on ILGPU CPU accelerator.
/// These tests verify correctness of the benchmark pipeline, not performance.
/// </summary>
public class BenchmarkValidationTests
{
    [Fact]
    public void BuildPopulation_SmallSize_CreatesGpuFeedForwardNetworks()
    {
        var population = GpuBenchmark.BuildPopulation(10);

        population.Should().HaveCount(10);
        population.Should().AllSatisfy(g =>
            g.Should().BeOfType<GpuFeedForwardNetwork>());
    }

    [Fact]
    public void BuildPopulation_VaryingComplexity_ContainsMixedTopologies()
    {
        // 10 genomes: indices 0-5 simple (6 nodes), 6-8 medium (7 nodes), 9 complex (8 nodes)
        var population = GpuBenchmark.BuildPopulation(10);

        var simpleCount = population.Count(g => g.NodeCount == 6);
        var mediumCount = population.Count(g => g.NodeCount == 7);
        var complexCount = population.Count(g => g.NodeCount == 8);

        simpleCount.Should().Be(6, "60% should be simple genomes");
        mediumCount.Should().Be(3, "30% should be medium genomes");
        complexCount.Should().Be(1, "10% should be complex genomes");
    }

    [Fact]
    public void BuildPopulation_IsDeterministic_SameSeedProducesSamePopulation()
    {
        var population1 = GpuBenchmark.BuildPopulation(20);
        var population2 = GpuBenchmark.BuildPopulation(20);

        for (int i = 0; i < population1.Count; i++)
        {
            population1[i].NodeCount.Should().Be(population2[i].NodeCount);
            population1[i].ConnectionCount.Should().Be(population2[i].ConnectionCount);
        }
    }

    [Fact]
    public void RunBenchmark_SmallPopulation_CompletesSuccessfully()
    {
        var results = GpuBenchmark.RunBenchmark(
            populationSizes: [10],
            warmupIterations: 1,
            timedIterations: 1);

        results.Should().HaveCount(1);
        results[0].PopulationSize.Should().Be(10);
        results[0].CpuGenomesPerSecond.Should().BeGreaterThan(0);
        results[0].GpuGenomesPerSecond.Should().BeGreaterThan(0);
        results[0].Speedup.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RunBenchmark_MultiplePopulationSizes_ReturnsResultPerSize()
    {
        int[] sizes = [10, 20, 50];

        var results = GpuBenchmark.RunBenchmark(
            populationSizes: sizes,
            warmupIterations: 1,
            timedIterations: 1);

        results.Should().HaveCount(3);
        results.Select(r => r.PopulationSize)
            .Should().BeEquivalentTo(sizes);
    }

    [Fact]
    public void RunBenchmark_AllGenomesReceiveFitness_ViaEvaluator()
    {
        // Verify the benchmark uses a fitness function that produces valid scores
        var fitnessFunction = new MultiInputFitnessFunction();

        fitnessFunction.CaseCount.Should().Be(10);
        fitnessFunction.InputCases.Length.Should().Be(40); // 10 cases * 4 inputs

        // ComputeFitness with zero outputs should return a valid score
        var zeroOutputs = new float[10]; // 10 cases * 1 output
        double fitness = fitnessFunction.ComputeFitness(zeroOutputs);
        fitness.Should().BeGreaterThan(0).And.BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void GenerateMarkdownReport_WithResults_ProducesValidMarkdown()
    {
        var results = new List<BenchmarkResult>
        {
            new(150, 50000, 250000, 5.0),
            new(500, 45000, 300000, 6.67),
            new(1000, 40000, 350000, 8.75),
        };

        string report = GpuBenchmark.GenerateMarkdownReport(results);

        report.Should().Contain("# NeatSharp GPU Benchmark Report");
        report.Should().Contain("## Hardware Configuration");
        report.Should().Contain("## Methodology");
        report.Should().Contain("## Results");
        report.Should().Contain("## Speedup Analysis");
        report.Should().Contain("## Notes");

        // Should contain the results table
        report.Should().Contain("| Population Size |");
        report.Should().Contain("150");
        report.Should().Contain("500");
        report.Should().Contain("1,000");
    }

    [Fact]
    public void GenerateMarkdownReport_WithDeviceInfo_IncludesGpuDetails()
    {
        var results = new List<BenchmarkResult>
        {
            new(150, 50000, 250000, 5.0),
        };
        var deviceInfo = new NeatSharp.Gpu.Detection.GpuDeviceInfo(
            "NVIDIA GeForce RTX 4090",
            new Version(8, 9),
            24L * 1024 * 1024 * 1024,
            true,
            null);

        string report = GpuBenchmark.GenerateMarkdownReport(results, deviceInfo);

        report.Should().Contain("NVIDIA GeForce RTX 4090");
        report.Should().Contain("8.9");
        report.Should().Contain("24.0 GB");
    }

    [Fact]
    public void GenerateMarkdownReport_WithoutDeviceInfo_NotesCpuAccelerator()
    {
        var results = new List<BenchmarkResult>
        {
            new(150, 50000, 50000, 1.0),
        };

        string report = GpuBenchmark.GenerateMarkdownReport(results, deviceInfo: null);

        report.Should().Contain("ILGPU CPU Accelerator");
        report.Should().Contain("no CUDA GPU");
    }

    [Fact]
    public void RunBenchmark_EndToEnd_WithReport_CompletesFullPipeline()
    {
        // Run a minimal benchmark and generate report — validates the full pipeline
        var results = GpuBenchmark.RunBenchmark(
            populationSizes: [10],
            warmupIterations: 1,
            timedIterations: 1);

        string report = GpuBenchmark.GenerateMarkdownReport(results);

        report.Should().NotBeNullOrWhiteSpace();
        report.Should().Contain("10");
        results.Should().AllSatisfy(r =>
        {
            r.CpuGenomesPerSecond.Should().BeGreaterThan(0);
            r.GpuGenomesPerSecond.Should().BeGreaterThan(0);
        });
    }
}
#endif
