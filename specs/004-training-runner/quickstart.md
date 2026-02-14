# Quickstart: Training Runner

**Feature Branch**: `004-training-runner`
**Date**: 2026-02-14

## Minimal XOR Example (< 20 lines)

```csharp
using Microsoft.Extensions.DependencyInjection;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Reporting;

// 1. Configure services
var services = new ServiceCollection();
services.AddNeatSharp(options =>
{
    options.InputCount = 2;
    options.OutputCount = 1;
    options.PopulationSize = 150;
    options.Seed = 42;
    options.Stopping.MaxGenerations = 150;
    options.Stopping.FitnessTarget = 3.9;
});

var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

// 2. Define fitness function
double[][] xorInputs = [[0, 0], [0, 1], [1, 0], [1, 1]];
double[] xorExpected = [0, 1, 1, 0];

// 3. Run evolution
var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();
var result = await evolver.RunAsync(genome =>
{
    double fitness = 0;
    Span<double> output = stackalloc double[1];
    for (int i = 0; i < 4; i++)
    {
        genome.Activate(xorInputs[i], output);
        double error = Math.Abs(xorExpected[i] - output[0]);
        fitness += 1.0 - error; // Max 1.0 per case, 4.0 total
    }
    return fitness;
});

// 4. Display result
var reporter = scope.ServiceProvider.GetRequiredService<IRunReporter>();
Console.WriteLine(reporter.GenerateSummary(result));
```

## Function Approximation Example (Sine)

```csharp
using Microsoft.Extensions.DependencyInjection;
using NeatSharp.Evolution;
using NeatSharp.Extensions;

var services = new ServiceCollection();
services.AddNeatSharp(options =>
{
    options.InputCount = 1;
    options.OutputCount = 1;
    options.PopulationSize = 150;
    options.Seed = 123;
    options.Stopping.MaxGenerations = 500;
    options.Stopping.FitnessTarget = 0.95;
});

var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

// Sample points for sin(x) over [0, 2π]
const int sampleCount = 20;
double[] inputs = new double[sampleCount];
double[] expected = new double[sampleCount];
for (int i = 0; i < sampleCount; i++)
{
    inputs[i] = i * 2.0 * Math.PI / (sampleCount - 1);
    expected[i] = (Math.Sin(inputs[i]) + 1.0) / 2.0; // Normalize to [0, 1]
}

var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();
var result = await evolver.RunAsync(genome =>
{
    double mse = 0;
    Span<double> output = stackalloc double[1];
    for (int i = 0; i < sampleCount; i++)
    {
        genome.Activate([inputs[i] / (2.0 * Math.PI)], output); // Normalize input to [0, 1]
        double error = expected[i] - output[0];
        mse += error * error;
    }
    mse /= sampleCount;
    return 1.0 / (1.0 + mse); // Fitness in (0, 1]
});

Console.WriteLine($"Champion fitness: {result.Champion.Fitness:F4}");
Console.WriteLine($"Found at generation: {result.Champion.Generation}");
Console.WriteLine($"Total generations: {result.History.TotalGenerations}");
```

## Cancellation Example

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await evolver.RunAsync(fitnessFunction, cts.Token);

if (result.WasCancelled)
{
    Console.WriteLine("Run was cancelled. Best result so far:");
}
Console.WriteLine($"Champion fitness: {result.Champion.Fitness:F4}");
```

## Batch Evaluation Example

```csharp
var result = await evolver.RunAsync(new MyBatchEvaluator());

class MyBatchEvaluator : IBatchEvaluator
{
    public Task EvaluateAsync(
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        CancellationToken cancellationToken)
    {
        // Evaluate all genomes at once (e.g., shared test data setup)
        for (int i = 0; i < genomes.Count; i++)
        {
            double score = EvaluateGenome(genomes[i]);
            setFitness(i, score);
        }
        return Task.CompletedTask;
    }
}
```

## Environment Evaluation Example

```csharp
var result = await evolver.RunAsync(new CartPoleEnvironment());

class CartPoleEnvironment : IEnvironmentEvaluator
{
    public async Task<double> EvaluateAsync(IGenome genome, CancellationToken ct)
    {
        double totalReward = 0;
        Span<double> outputs = stackalloc double[1];

        for (int step = 0; step < 500; step++)
        {
            double[] state = GetState(); // [position, velocity, angle, angular_velocity]
            genome.Activate(state, outputs);

            bool pushRight = outputs[0] > 0.5;
            ApplyAction(pushRight);
            totalReward += 1.0;

            if (IsDone()) break;
        }

        return totalReward;
    }
}
```

## Accessing Metrics

```csharp
// Metrics enabled by default (options.EnableMetrics = true)
foreach (var gen in result.History.Generations)
{
    Console.WriteLine(
        $"Gen {gen.Generation}: " +
        $"best={gen.BestFitness:F4}, avg={gen.AverageFitness:F4}, " +
        $"species={gen.SpeciesCount}, " +
        $"nodes={gen.Complexity.AverageNodes:F1}, conns={gen.Complexity.AverageConnections:F1}, " +
        $"eval={gen.Timing.Evaluation.TotalMilliseconds:F0}ms");
}

// Disable metrics for zero overhead
services.AddNeatSharp(options =>
{
    options.EnableMetrics = false;
    // ... other config
});
// result.History.Generations will be empty
```
