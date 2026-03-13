using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Genetics;
using NeatSharp.Reporting;

Console.WriteLine("=== NeatSharp Samples ===");
Console.WriteLine();

if (args.Length > 0 && args[0] == "hybrid-benchmark")
{
    // Detect real GPU first
    var hybridDetector = new NeatSharp.Gpu.Detection.GpuDeviceDetector(
        Microsoft.Extensions.Options.Options.Create(new NeatSharp.Gpu.Configuration.GpuOptions()));
    var hybridDevice = hybridDetector.Detect();
    if (hybridDevice is not null)
    {
        Console.WriteLine($"GPU Detected: {hybridDevice.DeviceName} (CC {hybridDevice.ComputeCapability}, {hybridDevice.MemoryBytes / (1024 * 1024)} MB, Compatible: {hybridDevice.IsCompatible})");
    }
    else
    {
        Console.WriteLine("No CUDA GPU detected.");
    }

    Console.WriteLine();

    // Run scaling benchmark (transfer-dominated + compute-dominated)
    var (transferResults, computeResults) = NeatSharp.Samples.HybridBenchmark.RunScalingBenchmark(
        populationSizes: [200, 1000, 5000]);

    Console.WriteLine();

    // Run bimodal comparison (SC-006)
    var bimodalResults = NeatSharp.Samples.HybridBenchmark.RunBimodalComparison(
        populationSizes: [200, 1000, 5000]);

    Console.WriteLine();
    Console.WriteLine("=== Final Report ===");
    Console.WriteLine(NeatSharp.Samples.HybridBenchmark.GenerateMarkdownReport(
        transferResults, computeResults, bimodalResults, hybridDevice));
    return;
}

if (args.Length > 0 && args[0] == "benchmark")
{
    // Detect real GPU first
    var detector = new NeatSharp.Gpu.Detection.GpuDeviceDetector(
        Microsoft.Extensions.Options.Options.Create(new NeatSharp.Gpu.Configuration.GpuOptions()));
    var device = detector.Detect();
    if (device is not null)
    {
        Console.WriteLine($"GPU Detected: {device.DeviceName} (CC {device.ComputeCapability}, {device.MemoryBytes / (1024 * 1024)} MB, Compatible: {device.IsCompatible})");
    }
    else
    {
        Console.WriteLine("No CUDA GPU detected.");
    }

    Console.WriteLine();
    var scalingResults = NeatSharp.Samples.GpuBenchmark.RunScalingBenchmark(
        populationSizes: [500, 1000, 2000, 5000],
        hiddenNodeCounts: [0, 10, 20, 50]);

    Console.WriteLine();

    // Also test with more test cases (100) to increase per-genome GPU workload
    Console.WriteLine();
    Console.WriteLine("=== High Test-Case Count Benchmark (100 test cases) ===");
    var heavyFitness = new NeatSharp.Samples.ParametricFitnessFunction(caseCount: 100, inputCount: 4);
    var heavyResults = NeatSharp.Samples.GpuBenchmark.RunScalingBenchmark(
        populationSizes: [500, 1000, 2000, 5000],
        hiddenNodeCounts: [0, 10, 20, 50],
        fitnessFunction: heavyFitness);

    Console.WriteLine();
    Console.WriteLine("=== Final Report ===");
    Console.WriteLine(NeatSharp.Samples.GpuBenchmark.GenerateScalingMarkdownReport(
        scalingResults, device,
        heavyResults: heavyResults,
        heavyCaseCount: 100));
    return;
}

if (args.Length > 0 && args[0] == "cart-pole")
{
    await NeatSharp.Samples.CartPole.CartPoleExample.RunAsync(args[1..]);
    return;
}

await RunXor();
Console.WriteLine();
await RunSineApproximation();

static async Task RunXor()
{
    Console.WriteLine("--- XOR Problem ---");
    Console.WriteLine();

    double[][] xorInputs = [[0, 0], [0, 1], [1, 0], [1, 1]];
    double[] xorExpected = [0, 1, 1, 0];

    var services = new ServiceCollection();
    services.AddNeatSharp(options =>
    {
        options.InputCount = 2;
        options.OutputCount = 1;
        options.PopulationSize = 150;
        options.Seed = 300;
        options.EnableMetrics = true;
        options.Stopping.MaxGenerations = 150;
        options.Stopping.FitnessTarget = 3.9;
    });
    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();

    var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();

    var sw = Stopwatch.StartNew();
    var result = await evolver.RunAsync(genome =>
    {
        double fitness = 0;
        Span<double> output = stackalloc double[1];
        for (int i = 0; i < 4; i++)
        {
            genome.Activate(xorInputs[i], output);
            double error = Math.Abs(xorExpected[i] - output[0]);
            fitness += 1.0 - error;
        }
        return fitness;
    });
    sw.Stop();

    // Summary
    var reporter = scope.ServiceProvider.GetRequiredService<IRunReporter>();
    Console.WriteLine(reporter.GenerateSummary(result));
    Console.WriteLine($"Elapsed: {sw.Elapsed.TotalMilliseconds:F0} ms");

    // Verify champion outputs
    Console.WriteLine();
    Console.WriteLine("Champion XOR outputs:");
    var champion = result.Champion.Genome;
    Span<double> buf = stackalloc double[1];
    for (int i = 0; i < 4; i++)
    {
        champion.Activate(xorInputs[i], buf);
        Console.WriteLine($"  XOR({xorInputs[i][0]}, {xorInputs[i][1]}) = {buf[0]:F4}  (expected {xorExpected[i]})");
    }

    // Metrics snapshot
    if (result.History.Generations.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Generation progress (every 10th):");
        Console.WriteLine($"  {"Gen",-5} {"Best",8} {"Avg",8} {"Species",8} {"AvgNodes",9} {"AvgConns",9}");
        foreach (var gen in result.History.Generations)
        {
            if (gen.Generation % 10 == 0 || gen.Generation == result.History.TotalGenerations - 1)
            {
                Console.WriteLine(
                    $"  {gen.Generation,-5} {gen.BestFitness,8:F4} {gen.AverageFitness,8:F4} {gen.SpeciesCount,8} {gen.Complexity.AverageNodes,9:F1} {gen.Complexity.AverageConnections,9:F1}");
            }
        }
    }
}

static async Task RunSineApproximation()
{
    Console.WriteLine("--- Sine Approximation ---");
    Console.WriteLine();

    const int sampleCount = 20;
    double[] inputs = new double[sampleCount];
    double[] expected = new double[sampleCount];
    for (int i = 0; i < sampleCount; i++)
    {
        inputs[i] = i * 2.0 * Math.PI / (sampleCount - 1);
        expected[i] = (Math.Sin(inputs[i]) + 1.0) / 2.0; // Normalize to [0, 1]
    }

    var services = new ServiceCollection();
    services.AddNeatSharp(options =>
    {
        options.InputCount = 1;
        options.OutputCount = 1;
        options.PopulationSize = 150;
        options.Seed = 123;
        options.EnableMetrics = true;
        options.Stopping.MaxGenerations = 500;
        options.Stopping.FitnessTarget = 0.95;
    });
    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();

    var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();

    var sw = Stopwatch.StartNew();
    var result = await evolver.RunAsync(genome =>
    {
        double mse = 0;
        Span<double> output = stackalloc double[1];
        for (int i = 0; i < sampleCount; i++)
        {
            genome.Activate([inputs[i] / (2.0 * Math.PI)], output);
            double error = expected[i] - output[0];
            mse += error * error;
        }
        mse /= sampleCount;
        return 1.0 / (1.0 + mse);
    });
    sw.Stop();

    // Summary
    var reporter = scope.ServiceProvider.GetRequiredService<IRunReporter>();
    Console.WriteLine(reporter.GenerateSummary(result));
    Console.WriteLine($"Elapsed: {sw.Elapsed.TotalMilliseconds:F0} ms");

    // Sample champion outputs
    Console.WriteLine();
    Console.WriteLine("Champion sine outputs (sample):");
    Console.WriteLine($"  {"x",-8} {"Expected",10} {"Output",10} {"Error",10}");
    var champion = result.Champion.Genome;
    Span<double> buf = stackalloc double[1];
    for (int i = 0; i < sampleCount; i += 4)
    {
        champion.Activate([inputs[i] / (2.0 * Math.PI)], buf);
        double error = Math.Abs(expected[i] - buf[0]);
        Console.WriteLine($"  {inputs[i],-8:F3} {expected[i],10:F4} {buf[0],10:F4} {error,10:F4}");
    }

    // Metrics snapshot
    if (result.History.Generations.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Generation progress (every 50th):");
        Console.WriteLine($"  {"Gen",-5} {"Best",8} {"Avg",8} {"Species",8} {"AvgNodes",9} {"AvgConns",9}");
        foreach (var gen in result.History.Generations)
        {
            if (gen.Generation % 50 == 0 || gen.Generation == result.History.TotalGenerations - 1)
            {
                Console.WriteLine(
                    $"  {gen.Generation,-5} {gen.BestFitness,8:F4} {gen.AverageFitness,8:F4} {gen.SpeciesCount,8} {gen.Complexity.AverageNodes,9:F1} {gen.Complexity.AverageConnections,9:F1}");
            }
        }
    }
}
