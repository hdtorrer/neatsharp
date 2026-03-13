# Implementation Plan: Parallel CPU Evaluation

**Branch**: `009-parallel-cpu-eval` | **Date**: 2026-03-13 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/009-parallel-cpu-eval/spec.md`

## Summary

Add multi-core parallel evaluation to the CPU evaluation path. The existing sequential adapters (sync, async, environment) in `EvaluationStrategy.cs` will be extended with parallel counterparts that use .NET's `Parallel.ForEachAsync` (sync/environment) and `SemaphoreSlim`-bounded `Task.WhenAll` (async). A `MaxDegreeOfParallelism` property on `EvaluationOptions` controls concurrency (null = all cores, 1 = sequential). Thread-safe error accumulation uses `ConcurrentBag<T>`. The callback `Action<int, double>` is made thread-safe via a lock wrapper inside the parallel adapters. Hybrid evaluator integration is automatic — the `EvaluationStrategyBatchAdapter` already delegates to `IEvaluationStrategy`, so parallelizing the strategy parallelizes the CPU batch.

## Technical Context

**Language/Version**: C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted
**Primary Dependencies**: Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (existing; no new dependencies)
**Storage**: N/A (in-memory only)
**Testing**: xUnit (existing test infrastructure in NeatSharp.Tests)
**Target Platform**: Windows + Linux (cross-platform)
**Project Type**: Library (NuGet package)
**Performance Goals**: Wall-clock time ≤ 2× sequential_time / N for CPU-bound fitness functions with populations ≥ 100 genomes on N cores (SC-001)
**Constraints**: No new external package dependencies; .NET built-in threading primitives only
**Scale/Scope**: Populations of 100–10,000+ genomes evaluated per generation

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Correctness | PASS | Parallel evaluation must produce identical fitness scores as sequential for deterministic functions (FR-005). No NEAT algorithm changes. |
| II. Reproducibility | PASS | Fitness results are order-independent (each genome evaluated independently). For deterministic fitness functions, results are identical regardless of evaluation order. PRNG is not involved in evaluation — randomness is consumed only during reproduction/mutation. |
| III. Performance | PASS | This feature IS the performance improvement. Benchmark evidence will be required (existing BenchmarkDotNet infrastructure). |
| IV. Developer Experience | PASS | ≤2 lines to enable (SC-005): set `MaxDegreeOfParallelism` on `EvaluationOptions`. Default (null) enables all-core parallelism. |
| V. Minimal Dependencies | PASS | No new dependencies. Uses `System.Threading.Tasks.Parallel`, `SemaphoreSlim`, `ConcurrentBag<T>` — all in-box .NET BCL types. |
| VI. SOLID Design | PASS | Extends existing adapter pattern via new nested classes in `EvaluationStrategy`. No modification to existing interfaces. Open/Closed: new adapters added, existing ones unchanged. |
| VII. TDD | PASS | All new adapters will have tests following Red-Green-Refactor. Concurrency tests will verify thread safety and correctness. |
| DI Practices | PASS | `EvaluationOptions.MaxDegreeOfParallelism` flows through existing options pattern. No new DI registrations needed — the factory methods in `EvaluationStrategy` read options and select the appropriate adapter. |

**Pre-Phase 0 gate: ALL PASS — proceed to Phase 0.**

**Post-Phase 1 re-check: ALL PASS.** Design uses only in-box BCL types (no new deps), extends existing adapter pattern without modifying interfaces (Open/Closed), and adds factory overloads that are backward-compatible. No constitution violations.

## Project Structure

### Documentation (this feature)

```text
specs/009-parallel-cpu-eval/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/
  NeatSharp/
    Configuration/
      EvaluationOptions.cs          # MODIFY: Add MaxDegreeOfParallelism property
    Evaluation/
      EvaluationStrategy.cs         # MODIFY: Add 3 parallel adapter nested classes + factory logic
      EvaluationStrategy.Parallel.cs # NEW: Partial class with parallel adapters (keeps file sizes manageable)

tests/
  NeatSharp.Tests/
    Evaluation/
      ParallelSyncFunctionAdapterTests.cs     # NEW: Parallel sync adapter tests
      ParallelAsyncFunctionAdapterTests.cs     # NEW: Parallel async adapter tests
      ParallelEnvironmentAdapterTests.cs       # NEW: Parallel environment adapter tests
      EvaluationOptionsTests.cs               # MODIFY: Add MaxDegreeOfParallelism validation tests
```

**Structure Decision**: All new code lives within existing projects. The parallel adapters are nested classes inside `EvaluationStrategy` (matching the existing pattern) but placed in a partial class file for readability. No new projects or assemblies needed.

## Complexity Tracking

No constitution violations — table not applicable.
