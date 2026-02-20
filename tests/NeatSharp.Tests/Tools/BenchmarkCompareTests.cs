#if NET9_0_OR_GREATER
using System.Text.Json;
using FluentAssertions;
using NeatSharp.Tools.BenchmarkCompare;
using Xunit;

namespace NeatSharp.Tests.Tools;

/// <summary>
/// Tests for the benchmark comparison logic in tools/benchmark-compare/Program.cs.
/// Calls <see cref="Program.ParseBenchmarks"/> and <see cref="Program.CompareBenchmarks"/>
/// directly to verify the real tool logic.
/// </summary>
public class BenchmarkCompareTests
{
    [Fact]
    public void CompareBenchmarks_WithinThreshold_ExitCodeZero()
    {
        // Arrange: current is 5% slower than baseline (within default 10% threshold)
        var baseline = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0)));
        var current = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_050_000.0)));

        // Act
        int exitCode = Program.CompareBenchmarks(baseline, current, threshold: 10.0);

        // Assert
        exitCode.Should().Be(0);
    }

    [Fact]
    public void CompareBenchmarks_RegressionExceedsThreshold_ExitCodeOne()
    {
        // Arrange: current is 15% slower than baseline (exceeds default 10% threshold)
        var baseline = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0)));
        var current = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_150_000.0)));

        // Act
        int exitCode = Program.CompareBenchmarks(baseline, current, threshold: 10.0);

        // Assert
        exitCode.Should().Be(1);
    }

    [Fact]
    public void CompareBenchmarks_Improvement_ExitCodeZero()
    {
        // Arrange: current is 20% faster than baseline (improvement, not regression)
        var baseline = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0)));
        var current = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 800_000.0)));

        // Act
        int exitCode = Program.CompareBenchmarks(baseline, current, threshold: 10.0);

        // Assert
        exitCode.Should().Be(0);
    }

    [Fact]
    public void CompareBenchmarks_MatchesByFullName()
    {
        // Arrange: multiple benchmarks, matched by FullName
        var baseline = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch(PopulationSize: 500)", 500_000.0),
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch(PopulationSize: 1000)", 1_000_000.0)));
        var current = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch(PopulationSize: 1000)", 1_050_000.0),
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch(PopulationSize: 500)", 525_000.0)));

        // Act
        int exitCode = Program.CompareBenchmarks(baseline, current, threshold: 10.0);

        // Assert: both within 10%, should pass
        exitCode.Should().Be(0);
    }

    [Fact]
    public void CompareBenchmarks_MissingInCurrent_DoesNotFail()
    {
        // Arrange: benchmark exists in baseline but not in current
        var baseline = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0),
            ("NeatSharp.Benchmarks.GpuEvaluatorBenchmarks.EvaluateBatch", 500_000.0)));
        var current = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_050_000.0)));

        // Act
        int exitCode = Program.CompareBenchmarks(baseline, current, threshold: 10.0);

        // Assert: should not fail due to missing benchmark
        exitCode.Should().Be(0);
    }

    [Fact]
    public void ParseBenchmarks_MalformedJson_Throws()
    {
        // Arrange: malformed JSON
        string malformed = "{ not valid json }}}";

        // Act & Assert
        var act = () => Program.ParseBenchmarks(malformed);
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void ParseBenchmarks_EmptyBenchmarks_ReturnsEmpty()
    {
        // Arrange: valid JSON but no Benchmarks array
        string json = "{}";

        // Act
        var result = Program.ParseBenchmarks(json);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void CompareBenchmarks_ExactlyAtThreshold_ExitCodeZero()
    {
        // Arrange: current is exactly 10% slower (at the threshold boundary)
        var baseline = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0)));
        var current = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_100_000.0)));

        // Act
        int exitCode = Program.CompareBenchmarks(baseline, current, threshold: 10.0);

        // Assert: exactly at threshold should NOT count as regression
        exitCode.Should().Be(0);
    }

    [Fact]
    public void CompareBenchmarks_ZeroBaseline_TreatedAsNoChange()
    {
        // Arrange: baseline mean is 0 (placeholder), should not cause division by zero
        var baseline = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 0.0)));
        var current = Program.ParseBenchmarks(CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0)));

        // Act
        int exitCode = Program.CompareBenchmarks(baseline, current, threshold: 10.0);

        // Assert: zero baseline should be treated as no change, not a regression
        exitCode.Should().Be(0);
    }

    /// <summary>
    /// Creates a minimal BenchmarkDotNet JSON export string with the given benchmarks.
    /// </summary>
    private static string CreateBenchmarkJson(params (string FullName, double MeanNs)[] benchmarks)
    {
        var entries = new List<object>();
        foreach (var (fullName, meanNs) in benchmarks)
        {
            entries.Add(new
            {
                FullName = fullName,
                Statistics = new { Mean = meanNs }
            });
        }

        var root = new { Benchmarks = entries };
        return JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
    }
}
#endif
