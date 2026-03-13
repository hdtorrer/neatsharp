# Research: Parallel CPU Evaluation

**Feature**: 009-parallel-cpu-eval
**Date**: 2026-03-13

## Research Topics

### 1. Parallel Evaluation Mechanism for Synchronous Fitness Functions

**Decision**: Use `Parallel.ForEachAsync` with `ParallelOptions.MaxDegreeOfParallelism`.

**Rationale**: `Parallel.ForEachAsync` (available in .NET 6+) provides:
- Built-in `MaxDegreeOfParallelism` support matching our configuration model
- Built-in `CancellationToken` propagation
- Dynamic work-stealing scheduler (handles variable-duration evaluations, addressing US-4 AS-2)
- No manual thread pool management
- Returns `Task` (consistent with `IEvaluationStrategy.EvaluatePopulationAsync`)

For the sync adapter, the fitness function `Func<IGenome, double>` is wrapped in an async lambda that runs the sync function inline (no `Task.Run` — `Parallel.ForEachAsync` already distributes across the thread pool).

**Alternatives considered**:
- `Parallel.ForEach` (sync): Returns void, harder to integrate with async interface. Would require wrapping in `Task.Run` or blocking.
- `Task.WhenAll` with manual partitioning: More code, less efficient scheduling, doesn't handle MaxDegreeOfParallelism natively.
- Custom `TaskScheduler`: Over-engineering for this use case.

### 2. Parallel Evaluation Mechanism for Asynchronous Fitness Functions

**Decision**: Use `SemaphoreSlim` to bound concurrency, `Task.WhenAll` to await all.

**Rationale**: Async fitness functions (`Func<IGenome, CancellationToken, Task<double>>`) are inherently async and should not be forced onto thread pool threads. Instead:
1. Create a `SemaphoreSlim(maxDegreeOfParallelism)` to bound concurrency
2. Launch all evaluation tasks, each waiting on the semaphore before starting
3. `await Task.WhenAll(tasks)` to collect results
4. This respects the async nature of the fitness function while bounding concurrency

When `MaxDegreeOfParallelism` is null (all cores), use `Environment.ProcessorCount`.

**Alternatives considered**:
- `Parallel.ForEachAsync`: Works but schedules onto thread pool threads, defeating the purpose of async I/O-bound fitness functions.
- `Channel<T>` with consumer tasks: More complex, no benefit over semaphore for this pattern.

### 3. Parallel Evaluation Mechanism for Environment Evaluators

**Decision**: Use `Parallel.ForEachAsync`, same as synchronous path.

**Rationale**: `IEnvironmentEvaluator.EvaluateAsync` returns `Task<double>`, but environment evaluations are typically CPU-bound (simulation steps). `Parallel.ForEachAsync` handles both sync and async workloads and provides work-stealing for variable-duration episodes (some Cart-Pole episodes terminate early, others run to max steps).

**Alternatives considered**:
- Semaphore + Task.WhenAll (like async adapter): Would work but `Parallel.ForEachAsync` is simpler and handles scheduling better for CPU-bound work.

### 4. Thread-Safe Callback Dispatch

**Decision**: Wrap the `Action<int, double> setFitness` callback in a `lock`-based wrapper inside the parallel adapter.

**Rationale**: The spec requires the parallel adapter to guarantee thread-safe callback dispatch (FR-011, clarification session). Options:
- `lock(syncObj)`: Simple, correct, minimal overhead (callback invocation is fast — just sets a value in an array)
- `ConcurrentDictionary<int, double>` + post-copy: Adds allocation and complexity
- `Interlocked`: Not applicable to `Action<int, double>` invocation

The lock wrapper is internal to the adapter — callers don't see it. Performance impact is negligible because the callback body (setting `fitness[index] = value`) is sub-microsecond.

**Alternatives considered**:
- No synchronization (rely on caller): Violates FR-011 and the clarification decision.
- Collect results in a `ConcurrentBag<(int, double)>` and call callback sequentially after: Adds latency and memory; the training runner's `scored[]` array is already set up for concurrent writes at different indices.

### 5. Thread-Safe Error Accumulation

**Decision**: Use `ConcurrentBag<(int Index, Exception Error)>` for collecting per-genome errors.

**Rationale**: The existing sequential adapters use `List<(int, Exception)>` which is not thread-safe. `ConcurrentBag<T>` is the simplest lock-free concurrent collection for unordered accumulation. After parallel evaluation completes, convert to the existing `EvaluationException` format.

**Alternatives considered**:
- `lock` + `List<T>`: Works but `ConcurrentBag` is purpose-built for this pattern.
- `ConcurrentQueue<T>`: Equivalent for this use case; `ConcurrentBag` is marginally more efficient for producer-only workloads.

### 6. MaxDegreeOfParallelism Configuration Model

**Decision**: Add `int? MaxDegreeOfParallelism` property to existing `EvaluationOptions`.

**Rationale**: Per clarification session decision. Semantics:
- `null` (default): Use `Environment.ProcessorCount` (all available cores)
- `1`: Sequential behavior (opt-out of parallelism)
- `> 1`: Use exactly that many concurrent evaluations
- `≤ 0`: Invalid, throw `ArgumentOutOfRangeException` during validation

This is simpler than a separate toggle + concurrency value. Users enable parallelism by leaving the default or setting a specific value.

**Alternatives considered**:
- Separate `EnableParallelEvaluation` bool + `MaxDegreeOfParallelism`: Redundant — the int value encodes both enablement and level.
- New `ParallelEvaluationOptions` class: Over-engineering for a single property.

### 7. Factory Method Changes

**Decision**: The existing `EvaluationStrategy.FromFunction(...)` and `FromEnvironment(...)` factory methods gain an optional `EvaluationOptions?` parameter. When `MaxDegreeOfParallelism != 1`, return the parallel adapter variant; when `== 1` or unspecified (backward compat), return the existing sequential adapter.

**Rationale**: Maintains backward compatibility — existing code without options continues to use sequential evaluation. The factory encapsulates the decision of which adapter to instantiate, consistent with the existing pattern.

**Alternatives considered**:
- Always use parallel adapter (with degree=1 for sequential): Would work but adds overhead for users who don't need parallelism. The sequential path should remain zero-overhead.
- Separate factory methods (`FromFunctionParallel`): Duplicates API surface unnecessarily.

### 8. Hybrid Evaluator Integration

**Decision**: No changes to `HybridBatchEvaluator` or `EvaluationStrategyBatchAdapter`. Integration is automatic.

**Rationale**: The hybrid evaluator's CPU backend is `EvaluationStrategyBatchAdapter`, which simply delegates to `IEvaluationStrategy.EvaluatePopulationAsync()`. If the resolved `IEvaluationStrategy` is a parallel adapter (because `MaxDegreeOfParallelism != 1`), the CPU batch automatically gets parallel evaluation. The hybrid evaluator already handles:
- Index remapping (local → global): works regardless of evaluation order
- Concurrent CPU + GPU execution via `Task.WhenAll`: no conflict with CPU-internal parallelism
- Fallback routing: same mechanism, just the CPU backend is now faster

**Alternatives considered**:
- Explicit parallel wiring in `HybridBatchEvaluator`: Unnecessary coupling. The hybrid evaluator shouldn't know or care about CPU-side parallelism.

### 9. Cancellation Token Propagation

**Decision**: Pass the `CancellationToken` through to `Parallel.ForEachAsync` / individual task lambdas. `Parallel.ForEachAsync` natively supports cancellation — it stops scheduling new iterations and throws `OperationCanceledException`.

**Rationale**: FR-010 requires cooperative cancellation. `Parallel.ForEachAsync` already provides this. Already-completed fitness scores are preserved because `setFitness` was already called for those genomes before cancellation. The training runner's existing `scored[]` array mechanism handles partial results correctly.

**Alternatives considered**:
- Manual cancellation check per iteration: Redundant — `Parallel.ForEachAsync` handles this.

### 10. Reproducibility Considerations

**Decision**: No special handling needed. Parallel evaluation does not affect reproducibility.

**Rationale**: Per Constitution Principle II, reproducibility means same seed → same results. Evaluation is purely a read operation on genomes — no PRNG consumption, no mutation. Fitness scores depend only on the genome's network weights and the fitness function, not evaluation order. The `setFitness` callback sets values by index, so order of completion doesn't matter. Deterministic fitness functions produce identical results regardless of parallelism (FR-005).

**Alternatives considered**:
- Ordered output buffer: Not needed since callback uses explicit indices.
