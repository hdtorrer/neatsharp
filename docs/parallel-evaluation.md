# Parallel CPU Evaluation Guide

NeatSharp supports parallel fitness evaluation across multiple CPU cores, significantly reducing wall-clock time for CPU-bound workloads. This guide covers configuration, thread-safety requirements, and when to use parallel vs sequential evaluation.

## Quick Setup

Pass an `EvaluationOptions` to any `FromFunction` or `FromEnvironment` factory method, or use the `RunAsync` overloads that accept `EvaluationOptions`:

```csharp
using NeatSharp.Configuration;
using NeatSharp.Evaluation;

// All cores (default when MaxDegreeOfParallelism is null)
var result = await evolver.RunAsync(
    genome => MyFitnessFunction(genome),
    new EvaluationOptions());
```

No additional packages are required -- parallel evaluation is built into the core `NeatSharp` package.

## Configuring Parallelism

The `MaxDegreeOfParallelism` property on `EvaluationOptions` controls concurrency:

| Value | Behavior |
|-------|----------|
| `null` (default) | Uses all available cores (`Environment.ProcessorCount`) |
| `1` | Sequential evaluation (same as calling `RunAsync` without options) |
| `N` (>1) | Uses at most `N` concurrent evaluations |

```csharp
// Use exactly 4 cores
var options = new EvaluationOptions { MaxDegreeOfParallelism = 4 };
var result = await evolver.RunAsync(genome => Fitness(genome), options);
```

```csharp
// Sequential (opt out of parallelism)
var options = new EvaluationOptions { MaxDegreeOfParallelism = 1 };
var result = await evolver.RunAsync(genome => Fitness(genome), options);
```

## Supported Evaluation Patterns

Parallel evaluation works with all three evaluation patterns:

### Synchronous Fitness Function

```csharp
var options = new EvaluationOptions { MaxDegreeOfParallelism = null };
var strategy = EvaluationStrategy.FromFunction(
    genome => ComputeFitness(genome), options);
```

### Asynchronous Fitness Function

Useful for I/O-bound evaluations (e.g., calling an external simulator):

```csharp
var options = new EvaluationOptions { MaxDegreeOfParallelism = 8 };
var strategy = EvaluationStrategy.FromFunction(
    async (genome, ct) => await EvaluateAsync(genome, ct), options);
```

### Environment Evaluator

For episode-based evaluation (e.g., Cart-Pole):

```csharp
var options = new EvaluationOptions { MaxDegreeOfParallelism = null };
var strategy = EvaluationStrategy.FromEnvironment(
    new CartPoleEvaluator(), options);
```

## Thread-Safety Requirements

When using parallel evaluation, your fitness function **must be thread-safe**. Multiple genomes are evaluated concurrently on different threads.

**Safe patterns:**
- Stateless functions that only read inputs and return a score
- Functions that allocate local state (no shared mutable data)
- `IGenome.Activate()` is safe to call concurrently on different genome instances

```csharp
// SAFE: all state is local
double Fitness(IGenome genome)
{
    Span<double> output = stackalloc double[1];
    double[] inputs = [0.5, 0.3, 0.7, 0.1];
    genome.Activate(inputs, output);
    return output[0];
}
```

**Unsafe patterns to avoid:**
- Writing to shared variables without synchronization
- Mutating shared collections
- Using non-thread-safe external resources

```csharp
// UNSAFE: shared mutable state
int counter = 0;
double BadFitness(IGenome genome)
{
    counter++;  // Race condition!
    return /* ... */;
}
```

If your fitness function cannot be made thread-safe, use `MaxDegreeOfParallelism = 1` to fall back to sequential evaluation.

## Error Handling

Parallel evaluation uses the same error-handling configuration as sequential:

```csharp
var options = new EvaluationOptions
{
    MaxDegreeOfParallelism = null,
    ErrorMode = EvaluationErrorMode.AssignFitness,
    ErrorFitnessValue = 0.0,
};
```

When a genome's evaluation throws:
- **`AssignFitness` mode** (default): The failed genome receives `ErrorFitnessValue`; remaining genomes are still evaluated. An `EvaluationException` is thrown after all evaluations complete, containing all failures.
- **`StopRun` mode**: Same behavior (all evaluations complete), but no default fitness is assigned to failed genomes.

## When to Use Parallel vs Sequential

| Scenario | Recommendation |
|----------|----------------|
| CPU-bound fitness, 100+ genomes | Parallel (default) |
| I/O-bound async fitness | Parallel with bounded concurrency |
| Microsecond-scale fitness function | Sequential (`MaxDegreeOfParallelism = 1`) -- thread scheduling overhead exceeds evaluation time |
| Non-thread-safe fitness function | Sequential (`MaxDegreeOfParallelism = 1`) |
| Debugging | Either -- results are identical for deterministic functions |
| GPU or hybrid evaluation | See [GPU Setup](gpu-setup.md) -- the hybrid evaluator's CPU batch uses parallel evaluation automatically |

## Cart-Pole Sample

The included Cart-Pole sample demonstrates parallel evaluation with the `--parallel` flag:

```bash
# Sequential (default)
dotnet run --project samples/NeatSharp.Samples -- cart-pole

# Parallel (all cores)
dotnet run --project samples/NeatSharp.Samples -- cart-pole --parallel

# Parallel (4 cores)
dotnet run --project samples/NeatSharp.Samples -- cart-pole --parallel 4
```

## Further Reading

- [Parameter Tuning](parameter-tuning.md) -- population size and evaluation options
- [GPU Setup](gpu-setup.md) -- GPU and hybrid CPU+GPU evaluation
- [Reproducibility](reproducibility.md) -- determinism guarantees with parallel evaluation
- [NEAT Basics](neat-basics.md) -- how NEAT works and the training loop
