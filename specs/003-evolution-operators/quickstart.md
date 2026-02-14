# Quickstart: Evolution Operators

**Feature Branch**: `003-evolution-operators`

## Minimal Setup

After this feature is implemented, the evolution operators are automatically registered when calling `AddNeatSharp()`. No additional configuration is required — all mutation rates, crossover parameters, speciation settings, and selection strategies use sensible NEAT-paper defaults.

```csharp
var services = new ServiceCollection();
services.AddNeatSharp(options =>
{
    options.PopulationSize = 150;
    options.Seed = 42;
    options.Stopping.MaxGenerations = 100;
});

var provider = services.BuildServiceProvider();
var evolver = provider.GetRequiredService<INeatEvolver>();

var result = await evolver.RunAsync(
    EvaluationStrategy.FromFunction(genome =>
    {
        // Your fitness function here
        Span<double> inputs = stackalloc double[2];
        Span<double> outputs = stackalloc double[1];
        inputs[0] = 1.0;
        inputs[1] = 0.0;
        genome.Activate(inputs, outputs);
        return outputs[0]; // fitness = output value
    }));

Console.WriteLine($"Best fitness: {result.Champion.Fitness}");
Console.WriteLine($"Achieved at generation: {result.Champion.Generation}");
Console.WriteLine($"Seed used: {result.Seed}");
```

## Custom Mutation Rates

```csharp
services.AddNeatSharp(options =>
{
    options.Seed = 42;
    options.Stopping.FitnessTarget = 3.9;

    // Increase structural mutation rates
    options.Mutation.AddConnectionRate = 0.10;
    options.Mutation.AddNodeRate = 0.05;

    // Use Gaussian perturbation instead of uniform
    options.Mutation.PerturbationDistribution = WeightDistributionType.Gaussian;
    options.Mutation.PerturbationPower = 0.3;
});
```

## Custom Speciation

```csharp
services.AddNeatSharp(options =>
{
    options.Seed = 42;
    options.Stopping.MaxGenerations = 200;

    // Tighter speciation (more species, more protection)
    options.Speciation.CompatibilityThreshold = 2.0;
    options.Speciation.WeightDifferenceCoefficient = 0.6;
});
```

## Custom Parent Selection

```csharp
// Use roulette wheel instead of tournament
services.AddNeatSharp(options =>
{
    options.Seed = 42;
    options.Stopping.MaxGenerations = 100;
});

// Override the default parent selector
services.AddSingleton<IParentSelector, RouletteWheelSelector>();
```

## Complexity Controls

```csharp
services.AddNeatSharp(options =>
{
    options.Seed = 42;
    options.Stopping.MaxGenerations = 500;

    // Hard caps on genome size
    options.Complexity.MaxNodes = 50;
    options.Complexity.MaxConnections = 200;

    // Soft penalty on complexity
    options.ComplexityPenalty.Coefficient = 0.01;
    options.ComplexityPenalty.Metric = ComplexityPenaltyMetric.Both;
});
```

## Elitism and Stagnation

```csharp
services.AddNeatSharp(options =>
{
    options.Seed = 42;
    options.Stopping.MaxGenerations = 300;

    // Preserve champions in species with 3+ members (default 5)
    options.Selection.ElitismThreshold = 3;

    // Penalize stagnant species after 20 generations (default 15)
    options.Selection.StagnationThreshold = 20;

    // Allow top 30% of species to reproduce (default 20%)
    options.Selection.SurvivalThreshold = 0.3;
});
```

## Dependencies Registered by AddNeatSharp()

After Spec 003, `AddNeatSharp()` registers these additional services:

| Interface / Concrete | Implementation | Lifetime |
|-----------|---------------|----------|
| `WeightPerturbationMutation` + `IMutationOperator` | `WeightPerturbationMutation` | Singleton |
| `WeightReplacementMutation` + `IMutationOperator` | `WeightReplacementMutation` | Singleton |
| `AddConnectionMutation` + `IMutationOperator` | `AddConnectionMutation` | Singleton |
| `AddNodeMutation` + `IMutationOperator` | `AddNodeMutation` | Singleton |
| `ToggleEnableMutation` + `IMutationOperator` | `ToggleEnableMutation` | Singleton |
| `ICrossoverOperator` | `NeatCrossover` | Singleton |
| `ICompatibilityDistance` | `CompatibilityDistance` | Singleton |
| `ISpeciationStrategy` | `CompatibilitySpeciation` | Singleton |
| `IParentSelector` | `TournamentSelector` | Singleton |
| `CompositeMutationOperator` | `CompositeMutationOperator` | Singleton |
| `ReproductionAllocator` | `ReproductionAllocator` | Singleton |
| `ReproductionOrchestrator` | `ReproductionOrchestrator` | Singleton |
