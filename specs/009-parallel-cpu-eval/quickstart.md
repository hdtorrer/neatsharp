# Quickstart: Parallel CPU Evaluation

**Feature**: 009-parallel-cpu-eval

## Enable Parallel Evaluation (All Cores)

No configuration needed — parallel evaluation uses all available cores by default:

```csharp
// Default: MaxDegreeOfParallelism = null → uses Environment.ProcessorCount
var strategy = EvaluationStrategy.FromFunction(
    genome => MyFitnessFunction(genome),
    new EvaluationOptions());
```

## Configure Degree of Parallelism

```csharp
// Use exactly 4 cores
var options = new EvaluationOptions
{
    MaxDegreeOfParallelism = 4
};

var strategy = EvaluationStrategy.FromFunction(
    genome => MyFitnessFunction(genome),
    options);
```

## Opt Out (Sequential)

```csharp
// Sequential evaluation (same as pre-parallel behavior)
var options = new EvaluationOptions
{
    MaxDegreeOfParallelism = 1
};

var strategy = EvaluationStrategy.FromFunction(
    genome => MyFitnessFunction(genome),
    options);
```

## Async Fitness Functions

```csharp
var options = new EvaluationOptions
{
    MaxDegreeOfParallelism = 8  // Bounds concurrent async evaluations
};

var strategy = EvaluationStrategy.FromFunction(
    async (genome, ct) => await EvaluateAsync(genome, ct),
    options);
```

## Environment Evaluators

```csharp
var options = new EvaluationOptions
{
    MaxDegreeOfParallelism = null  // All cores (default)
};

var strategy = EvaluationStrategy.FromEnvironment(
    new CartPoleEvaluator(),
    options);
```

## Hybrid Evaluator Integration

No additional configuration — the hybrid evaluator's CPU batch automatically uses parallel evaluation when `MaxDegreeOfParallelism != 1`:

```csharp
services.AddNeatSharp(options =>
{
    options.Evaluation.MaxDegreeOfParallelism = null; // CPU batch parallelized
});
services.AddNeatSharpGpu();
services.AddNeatSharpHybrid(hybrid =>
{
    hybrid.SplitPolicyType = SplitPolicyType.Adaptive;
});
```

## Thread Safety Requirements

When using parallel evaluation, your fitness function **must be thread-safe**:

```csharp
// GOOD: Stateless or thread-safe fitness function
double Fitness(IGenome genome)
{
    var inputs = new double[] { /* ... */ };
    var outputs = new double[1];
    genome.Activate(inputs, outputs);  // Activate is thread-safe per genome
    return outputs[0];
}

// BAD: Shared mutable state without synchronization
int counter = 0;
double BadFitness(IGenome genome)
{
    counter++;  // Race condition!
    // ...
}
```

## Error Handling

Parallel evaluation preserves the same error-handling behavior as sequential:

```csharp
var options = new EvaluationOptions
{
    ErrorMode = EvaluationErrorMode.AssignFitness,
    ErrorFitnessValue = 0.0,
    MaxDegreeOfParallelism = null
};

// If some genomes throw, others still get correct fitness scores.
// Failed genomes receive ErrorFitnessValue.
```

## When to Use Parallel vs Sequential

| Scenario | Recommendation |
|----------|---------------|
| CPU-bound fitness, 100+ genomes | Parallel (default) |
| I/O-bound async fitness | Parallel with bounded concurrency |
| Microsecond-scale fitness | Sequential (`MaxDegreeOfParallelism = 1`) |
| Debugging / reproducibility | Either — results are identical for deterministic functions |
| Non-thread-safe fitness function | Sequential (`MaxDegreeOfParallelism = 1`) |
