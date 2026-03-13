# API Contract Changes: Parallel CPU Evaluation

**Feature**: 009-parallel-cpu-eval
**Date**: 2026-03-13

## Modified APIs

### 1. EvaluationOptions — New Property

```csharp
namespace NeatSharp.Configuration;

public class EvaluationOptions
{
    // Existing
    public EvaluationErrorMode ErrorMode { get; set; } = EvaluationErrorMode.AssignFitness;
    public double ErrorFitnessValue { get; set; } = 0.0;

    // NEW
    /// <summary>
    /// Maximum number of concurrent genome evaluations.
    /// <c>null</c> (default) uses all available processor cores.
    /// <c>1</c> reverts to sequential evaluation.
    /// Must be <c>null</c> or >= 1.
    /// </summary>
    public int? MaxDegreeOfParallelism { get; set; }
}
```

### 2. EvaluationStrategy Factory — New Overloads

```csharp
namespace NeatSharp.Evaluation;

public static partial class EvaluationStrategy
{
    // Existing (unchanged — backward compatible)
    public static IEvaluationStrategy FromFunction(Func<IGenome, double> fitnessFunction);
    public static IEvaluationStrategy FromFunction(Func<IGenome, CancellationToken, Task<double>> fitnessFunction);
    public static IEvaluationStrategy FromEnvironment(IEnvironmentEvaluator evaluator);
    public static IEvaluationStrategy FromBatch(IBatchEvaluator evaluator);

    // NEW overloads with EvaluationOptions
    public static IEvaluationStrategy FromFunction(
        Func<IGenome, double> fitnessFunction,
        EvaluationOptions options);

    public static IEvaluationStrategy FromFunction(
        Func<IGenome, CancellationToken, Task<double>> fitnessFunction,
        EvaluationOptions options);

    public static IEvaluationStrategy FromEnvironment(
        IEnvironmentEvaluator evaluator,
        EvaluationOptions options);

    // FromBatch does NOT get an options overload — batch evaluators
    // manage their own parallelism internally (GPU kernels, etc.)
}
```

**Behavior**:
- When `options` is not provided → existing sequential adapters (backward compatible)
- When `options.MaxDegreeOfParallelism == 1` → sequential adapters
- When `options.MaxDegreeOfParallelism == null` or `> 1` → parallel adapters
- `options.MaxDegreeOfParallelism <= 0` → `ArgumentOutOfRangeException`

## Unchanged APIs

| API | Why Unchanged |
|-----|--------------|
| `IEvaluationStrategy` | Interface contract unchanged; parallel adapters implement it |
| `IBatchEvaluator` | Batch evaluators manage own parallelism |
| `IEnvironmentEvaluator` | Interface unchanged; caller must ensure thread safety |
| `EvaluationException` | Same error aggregation format |
| `HybridBatchEvaluator` | Automatically benefits via `EvaluationStrategyBatchAdapter` |
| `EvaluationStrategyBatchAdapter` | Pure delegation; no changes needed |
| `AddNeatSharp()` DI extension | Options flow through existing `EvaluationOptions` |

## Thread Safety Contract

**Library guarantees** (internal to parallel adapters):
- `setFitness` callback is synchronized via lock — safe to call from evaluation threads
- Error accumulation is thread-safe via `ConcurrentBag<T>`

**Caller guarantees** (documented requirement):
- Fitness function (`Func<IGenome, double>`) must be thread-safe when `MaxDegreeOfParallelism != 1`
- `IEnvironmentEvaluator.EvaluateAsync` must be thread-safe when `MaxDegreeOfParallelism != 1`
- `IGenome.Activate` is thread-safe per genome instance (each genome has its own network state)
