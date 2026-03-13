# Data Model: Parallel CPU Evaluation

**Feature**: 009-parallel-cpu-eval
**Date**: 2026-03-13

## Entities

### 1. EvaluationOptions (MODIFIED)

**Location**: `src/NeatSharp/Configuration/EvaluationOptions.cs`
**Change**: Add `MaxDegreeOfParallelism` property

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| ErrorMode | `EvaluationErrorMode` | `AssignFitness` | Existing — how to handle per-genome evaluation errors |
| ErrorFitnessValue | `double` | `0.0` | Existing — fitness assigned to failed genomes in AssignFitness mode |
| **MaxDegreeOfParallelism** | `int?` | `null` | **NEW** — Maximum concurrent genome evaluations. `null` = all cores (`Environment.ProcessorCount`), `1` = sequential, `> 1` = exact concurrency. Values `≤ 0` are invalid. |

**Validation Rules**:
- `MaxDegreeOfParallelism` must be `null` or `≥ 1`
- Validation occurs in the existing `EvaluationOptions` validation path (if any), or in the adapter factory methods

**State Transitions**: N/A — configuration is immutable after construction.

### 2. ParallelSyncFunctionAdapter (NEW — nested class)

**Location**: `src/NeatSharp/Evaluation/EvaluationStrategy.Parallel.cs`
**Implements**: `IEvaluationStrategy`

| Field | Type | Description |
|-------|------|-------------|
| _fitnessFunction | `Func<IGenome, double>` | User-provided sync fitness function |
| _options | `EvaluationOptions` | Evaluation configuration (error mode, parallelism) |

**Behavior**: Uses `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` to evaluate genomes concurrently. Error accumulation via `ConcurrentBag`. Callback wrapped with lock for thread safety.

### 3. ParallelAsyncFunctionAdapter (NEW — nested class)

**Location**: `src/NeatSharp/Evaluation/EvaluationStrategy.Parallel.cs`
**Implements**: `IEvaluationStrategy`

| Field | Type | Description |
|-------|------|-------------|
| _fitnessFunction | `Func<IGenome, CancellationToken, Task<double>>` | User-provided async fitness function |
| _options | `EvaluationOptions` | Evaluation configuration (error mode, parallelism) |

**Behavior**: Uses `SemaphoreSlim` to bound concurrency + `Task.WhenAll` to await all evaluations. Error accumulation via `ConcurrentBag`. Callback wrapped with lock for thread safety.

### 4. ParallelEnvironmentAdapter (NEW — nested class)

**Location**: `src/NeatSharp/Evaluation/EvaluationStrategy.Parallel.cs`
**Implements**: `IEvaluationStrategy`

| Field | Type | Description |
|-------|------|-------------|
| _evaluator | `IEnvironmentEvaluator` | User-provided environment evaluator |
| _options | `EvaluationOptions` | Evaluation configuration (error mode, parallelism) |

**Behavior**: Uses `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` to evaluate genomes concurrently. Error accumulation via `ConcurrentBag`. Callback wrapped with lock for thread safety.

## Relationships

```
EvaluationOptions (1) ──configures──▶ (0..*) Parallel*Adapter
EvaluationStrategy (factory) ──creates──▶ (1) Parallel*Adapter or Sequential*Adapter
IBatchEvaluator ◁── EvaluationStrategyBatchAdapter ──delegates──▶ IEvaluationStrategy (parallel or sequential)
HybridBatchEvaluator ──uses──▶ EvaluationStrategyBatchAdapter ──uses──▶ IEvaluationStrategy
```

## Thread Safety Model

| Component | Thread Safety Mechanism | Owned By |
|-----------|------------------------|----------|
| `setFitness` callback | `lock(_callbackLock)` | Parallel adapter (internal) |
| Error list | `ConcurrentBag<(int, Exception)>` | Parallel adapter (internal) |
| Fitness function | Thread-safe by contract | Caller (documented requirement) |
| `IEnvironmentEvaluator` | Thread-safe by contract | Caller (documented requirement) |
