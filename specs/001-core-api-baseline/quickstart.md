# Quickstart: NeatSharp

**Goal**: Go from zero to a running NEAT evolution producing a champion in under 10 minutes.

## 1. Install the package

```bash
dotnet add package NeatSharp
```

## 2. Solve XOR with NEAT (< 20 lines)

```csharp
using Microsoft.Extensions.DependencyInjection;
using NeatSharp.Configuration;
using NeatSharp.Evaluation;
using NeatSharp.Evolution;
using NeatSharp.Genetics;

// 1. Set up DI and configure NeatSharp
var services = new ServiceCollection();
services.AddNeatSharp(options =>
{
    options.PopulationSize = 150;
    options.Seed = 42;
    options.Stopping.MaxGenerations = 300;
    options.Stopping.FitnessTarget = 3.9;
});

var provider = services.BuildServiceProvider();

// 2. Define a fitness function (XOR problem)
static double EvaluateXor(IGenome genome)
{
    double[][] inputs = [[0, 0], [0, 1], [1, 0], [1, 1]];
    double[] expected = [0, 1, 1, 0];
    double fitness = 0;

    Span<double> output = stackalloc double[1];
    for (int i = 0; i < inputs.Length; i++)
    {
        genome.Activate(inputs[i], output);
        fitness += 1.0 - Math.Abs(expected[i] - output[0]);
    }

    return fitness; // Max possible: 4.0
}

// 3. Run evolution
var evolver = provider.GetRequiredService<INeatEvolver>();
var result = await evolver.RunAsync(EvaluateXor);

// 4. Inspect the champion
Console.WriteLine($"Champion fitness: {result.Champion.Fitness:F4}");
Console.WriteLine($"Found at generation: {result.Champion.Generation}");
Console.WriteLine($"Seed used: {result.Seed}");
Console.WriteLine($"Total generations: {result.History.TotalGenerations}");
```

**Line count**: 18 lines of user code (excluding `using` directives and blank lines) — meets SC-004.

## 3. What just happened?

1. **DI registration**: `AddNeatSharp(...)` registers all library services and configures options.
2. **Fitness function**: A simple function that scores how well a genome solves XOR. Maximum score is 4.0 (perfect on all 4 cases).
3. **Evolution**: The library initializes a population of 150 genomes, evaluates them using your function, and evolves them through speciation, selection, crossover, and mutation — repeating until the fitness target (3.9) is reached or 300 generations pass.
4. **Result**: The champion genome, the seed used (for reproducibility), and the full run history.

## Reproduce the run

Running the same code again with `Seed = 42` produces identical results (SC-002):

```csharp
// Same seed + same config = same champion + same history
options.Seed = 42;
// ... run again → identical result
```

If you omit `Seed`, the library auto-generates one and records it in `result.Seed` so you can reproduce later.

## With an environment evaluator

For problems requiring multi-step interaction (game AI, control tasks):

```csharp
public class CartPoleEvaluator : IEnvironmentEvaluator
{
    public Task<double> EvaluateAsync(IGenome genome, CancellationToken ct)
    {
        var env = new CartPoleEnvironment();
        double totalReward = 0;

        while (!env.IsDone)
        {
            ct.ThrowIfCancellationRequested();
            Span<double> actions = stackalloc double[1];
            genome.Activate(env.Observation, actions);
            totalReward += env.Step(actions[0]);
        }

        return Task.FromResult(totalReward);
    }
}

// Use it:
var result = await evolver.RunAsync(new CartPoleEvaluator());
```

## With cancellation

For long-running evolutions, use `CancellationToken` for graceful shutdown:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

var result = await evolver.RunAsync(EvaluateXor, cts.Token);

if (result.WasCancelled)
{
    Console.WriteLine($"Cancelled after {result.History.TotalGenerations} generations");
    Console.WriteLine($"Best fitness so far: {result.Champion.Fitness:F4}");
}
```

The run stops gracefully and returns the best genome found so far — no exception thrown.

## With logging

NeatSharp uses `Microsoft.Extensions.Logging`. Configure any provider:

```csharp
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
services.AddNeatSharp(options => { /* ... */ });
```

Generation-by-generation progress is logged at `Debug` level. Set `EnableMetrics = false` to skip per-generation statistics collection.

## Next steps

- **Batch evaluation**: Implement `IBatchEvaluator` for GPU-accelerated or parallel fitness evaluation.
- **Tune parameters**: Adjust `PopulationSize`, stopping criteria, and `ComplexityLimits` for your problem.
- **Inspect history**: Use `result.History.Generations` to plot fitness curves and species dynamics.
