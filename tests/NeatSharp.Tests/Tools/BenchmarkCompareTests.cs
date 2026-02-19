using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace NeatSharp.Tests.Tools;

/// <summary>
/// Tests for the benchmark comparison logic used by the benchmark-compare tool.
/// The <see cref="BenchmarkComparer"/> helper class mirrors the comparison algorithm
/// in tools/benchmark-compare/Program.cs.
/// </summary>
public class BenchmarkCompareTests
{
    [Fact]
    public void CompareBenchmarks_WithinThreshold_ExitCodeZero()
    {
        // Arrange: current is 5% slower than baseline (within default 10% threshold)
        string baseline = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0));
        string current = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_050_000.0));

        // Act
        var result = BenchmarkComparer.Compare(baseline, current, threshold: 10.0);

        // Assert
        result.ExitCode.Should().Be(0);
        result.Comparisons.Should().HaveCount(1);
        result.Comparisons[0].PercentChange.Should().BeApproximately(5.0, 0.01);
    }

    [Fact]
    public void CompareBenchmarks_RegressionExceedsThreshold_ExitCodeOne()
    {
        // Arrange: current is 15% slower than baseline (exceeds default 10% threshold)
        string baseline = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0));
        string current = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_150_000.0));

        // Act
        var result = BenchmarkComparer.Compare(baseline, current, threshold: 10.0);

        // Assert
        result.ExitCode.Should().Be(1);
        result.Comparisons[0].PercentChange.Should().BeApproximately(15.0, 0.01);
        result.Comparisons[0].IsRegression.Should().BeTrue();
    }

    [Fact]
    public void CompareBenchmarks_Improvement_ExitCodeZero()
    {
        // Arrange: current is 20% faster than baseline (improvement, not regression)
        string baseline = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0));
        string current = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 800_000.0));

        // Act
        var result = BenchmarkComparer.Compare(baseline, current, threshold: 10.0);

        // Assert
        result.ExitCode.Should().Be(0);
        result.Comparisons[0].PercentChange.Should().BeApproximately(-20.0, 0.01);
        result.Comparisons[0].IsRegression.Should().BeFalse();
    }

    [Fact]
    public void CompareBenchmarks_MatchesByFullName()
    {
        // Arrange: multiple benchmarks, matched by FullName
        string baseline = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch(PopulationSize: 500)", 500_000.0),
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch(PopulationSize: 1000)", 1_000_000.0));
        string current = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch(PopulationSize: 1000)", 1_050_000.0),
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch(PopulationSize: 500)", 525_000.0));

        // Act
        var result = BenchmarkComparer.Compare(baseline, current, threshold: 10.0);

        // Assert: both matched correctly
        result.ExitCode.Should().Be(0);
        result.Comparisons.Should().HaveCount(2);
        result.Comparisons.Should().Contain(c =>
            c.FullName == "NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch(PopulationSize: 500)"
            && Math.Abs(c.PercentChange - 5.0) < 0.01);
        result.Comparisons.Should().Contain(c =>
            c.FullName == "NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch(PopulationSize: 1000)"
            && Math.Abs(c.PercentChange - 5.0) < 0.01);
    }

    [Fact]
    public void CompareBenchmarks_MissingInCurrent_Handled()
    {
        // Arrange: benchmark exists in baseline but not in current
        string baseline = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0),
            ("NeatSharp.Benchmarks.GpuEvaluatorBenchmarks.EvaluateBatch", 500_000.0));
        string current = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_050_000.0));

        // Act
        var result = BenchmarkComparer.Compare(baseline, current, threshold: 10.0);

        // Assert: should not fail, just skip the missing benchmark
        result.ExitCode.Should().Be(0);
        result.Comparisons.Should().HaveCount(1);
        result.MissingBenchmarks.Should().Contain("NeatSharp.Benchmarks.GpuEvaluatorBenchmarks.EvaluateBatch");
    }

    [Fact]
    public void CompareBenchmarks_MalformedJson_Handled()
    {
        // Arrange: malformed JSON
        string baseline = "{ not valid json }}}";
        string current = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0));

        // Act
        var result = BenchmarkComparer.Compare(baseline, current, threshold: 10.0);

        // Assert: should return error exit code
        result.ExitCode.Should().Be(2);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void CompareBenchmarks_EmptyBaseline_Handled()
    {
        // Arrange: empty JSON
        string baseline = "{}";
        string current = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0));

        // Act
        var result = BenchmarkComparer.Compare(baseline, current, threshold: 10.0);

        // Assert: no comparisons, but no crash
        result.ExitCode.Should().Be(0);
        result.Comparisons.Should().BeEmpty();
    }

    [Fact]
    public void CompareBenchmarks_ExactlyAtThreshold_ExitCodeZero()
    {
        // Arrange: current is exactly 10% slower (at the threshold boundary)
        string baseline = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_000_000.0));
        string current = CreateBenchmarkJson(
            ("NeatSharp.Benchmarks.CpuEvaluatorBenchmarks.EvaluateBatch", 1_100_000.0));

        // Act
        var result = BenchmarkComparer.Compare(baseline, current, threshold: 10.0);

        // Assert: exactly at threshold should NOT count as regression
        result.ExitCode.Should().Be(0);
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

/// <summary>
/// Core benchmark comparison logic. This mirrors the algorithm in
/// tools/benchmark-compare/Program.cs for testability without process invocation.
/// </summary>
internal static class BenchmarkComparer
{
    public static ComparisonResult Compare(string baselineJson, string currentJson, double threshold)
    {
        Dictionary<string, double>? baselineBenchmarks;
        Dictionary<string, double>? currentBenchmarks;

        try
        {
            baselineBenchmarks = ParseBenchmarkJson(baselineJson);
            currentBenchmarks = ParseBenchmarkJson(currentJson);
        }
        catch (Exception ex)
        {
            return new ComparisonResult
            {
                ExitCode = 2,
                ErrorMessage = $"Failed to parse benchmark JSON: {ex.Message}"
            };
        }

        var comparisons = new List<BenchmarkComparison>();
        var missingBenchmarks = new List<string>();
        bool hasRegression = false;

        foreach (var (fullName, baselineMean) in baselineBenchmarks)
        {
            if (!currentBenchmarks.TryGetValue(fullName, out double currentMean))
            {
                missingBenchmarks.Add(fullName);
                continue;
            }

            double percentChange = baselineMean > 0
                ? ((currentMean - baselineMean) / baselineMean) * 100.0
                : 0.0;

            // A positive percentChange means slower (regression) since Mean is time
            bool isRegression = percentChange > threshold;
            if (isRegression)
            {
                hasRegression = true;
            }

            comparisons.Add(new BenchmarkComparison
            {
                FullName = fullName,
                BaselineMean = baselineMean,
                CurrentMean = currentMean,
                PercentChange = percentChange,
                IsRegression = isRegression
            });
        }

        return new ComparisonResult
        {
            ExitCode = hasRegression ? 1 : 0,
            Comparisons = comparisons,
            MissingBenchmarks = missingBenchmarks
        };
    }

    private static Dictionary<string, double> ParseBenchmarkJson(string json)
    {
        var result = new Dictionary<string, double>();

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("Benchmarks", out var benchmarks))
        {
            return result;
        }

        foreach (var benchmark in benchmarks.EnumerateArray())
        {
            if (benchmark.TryGetProperty("FullName", out var fullNameProp)
                && benchmark.TryGetProperty("Statistics", out var stats)
                && stats.TryGetProperty("Mean", out var meanProp))
            {
                string? fullName = fullNameProp.GetString();
                if (fullName is not null && meanProp.TryGetDouble(out double mean))
                {
                    result[fullName] = mean;
                }
            }
        }

        return result;
    }
}

internal sealed class ComparisonResult
{
    public int ExitCode { get; init; }
    public string? ErrorMessage { get; init; }
    public List<BenchmarkComparison> Comparisons { get; init; } = [];
    public List<string> MissingBenchmarks { get; init; } = [];
}

internal sealed class BenchmarkComparison
{
    public string FullName { get; init; } = "";
    public double BaselineMean { get; init; }
    public double CurrentMean { get; init; }
    public double PercentChange { get; init; }
    public bool IsRegression { get; init; }
}
