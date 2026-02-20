using System.Globalization;
using System.Text.Json;

namespace NeatSharp.Tools.BenchmarkCompare;

public static class Program
{
    public static int Main(string[] args)
    {
        string? baselinePath = null;
        string? currentPath = null;
        double threshold = 10.0;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--baseline" when i + 1 < args.Length:
                    baselinePath = args[++i];
                    break;
                case "--current" when i + 1 < args.Length:
                    currentPath = args[++i];
                    break;
                case "--threshold" when i + 1 < args.Length:
                    threshold = double.Parse(args[++i], CultureInfo.InvariantCulture);
                    break;
            }
        }

        if (baselinePath is null || currentPath is null)
        {
            Console.Error.WriteLine("Usage: benchmark-compare --baseline <path> --current <path> [--threshold <percent>]");
            return 2;
        }

        if (!File.Exists(baselinePath))
        {
            Console.Error.WriteLine($"Baseline file not found: {baselinePath}");
            return 2;
        }

        if (!File.Exists(currentPath))
        {
            Console.Error.WriteLine($"Current file not found: {currentPath}");
            return 2;
        }

        try
        {
            var baselineBenchmarks = ParseBenchmarks(File.ReadAllText(baselinePath));
            var currentBenchmarks = ParseBenchmarks(File.ReadAllText(currentPath));

            return CompareBenchmarks(baselineBenchmarks, currentBenchmarks, threshold);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error parsing JSON: {ex.Message}");
            return 2;
        }
    }

    public static Dictionary<string, double> ParseBenchmarks(string json)
    {
        var result = new Dictionary<string, double>();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("Benchmarks", out var benchmarks))
        {
            return result;
        }

        foreach (var benchmark in benchmarks.EnumerateArray())
        {
            if (benchmark.TryGetProperty("FullName", out var fullName) &&
                benchmark.TryGetProperty("Statistics", out var stats) &&
                stats.TryGetProperty("Mean", out var mean))
            {
                result[fullName.GetString()!] = mean.GetDouble();
            }
        }

        return result;
    }

    public static int CompareBenchmarks(
        Dictionary<string, double> baseline,
        Dictionary<string, double> current,
        double threshold)
    {
        var hasRegression = false;
        var allNames = baseline.Keys.Union(current.Keys).OrderBy(n => n).ToList();

        Console.WriteLine($"{"Benchmark",-60} {"Baseline (ns)",15} {"Current (ns)",15} {"Change (%)",12} {"Status",8}");
        Console.WriteLine(new string('-', 115));

        foreach (var name in allNames)
        {
            var hasBaseline = baseline.TryGetValue(name, out var baselineValue);
            var hasCurrent = current.TryGetValue(name, out var currentValue);

            if (hasBaseline && hasCurrent)
            {
                var changePercent = baselineValue > 0
                    ? ((currentValue - baselineValue) / baselineValue) * 100
                    : 0.0;
                var status = changePercent > threshold ? "REGRESS" : "OK";
                if (changePercent > threshold)
                {
                    hasRegression = true;
                }

                var displayName = name.Length > 58 ? name[..58] + ".." : name;
                Console.WriteLine($"{displayName,-60} {baselineValue,15:F2} {currentValue,15:F2} {changePercent,11:F2}% {status,8}");
            }
            else if (hasBaseline)
            {
                var displayName = name.Length > 58 ? name[..58] + ".." : name;
                Console.WriteLine($"{displayName,-60} {baselineValue,15:F2} {"(missing)",15} {"N/A",12} {"WARN",8}");
            }
            else
            {
                var displayName = name.Length > 58 ? name[..58] + ".." : name;
                Console.WriteLine($"{displayName,-60} {"(new)",15} {currentValue,15:F2} {"N/A",12} {"NEW",8}");
            }
        }

        Console.WriteLine();
        if (hasRegression)
        {
            Console.WriteLine($"FAIL: One or more benchmarks regressed beyond {threshold}% threshold.");
            return 1;
        }

        Console.WriteLine($"PASS: No regressions beyond {threshold}% threshold.");
        return 0;
    }
}
