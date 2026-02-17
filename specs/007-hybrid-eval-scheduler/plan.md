# Implementation Plan: Hybrid Evaluation Scheduler (CPU + GPU Concurrent, Adaptive Partitioning)

**Branch**: `007-hybrid-eval-scheduler` | **Date**: 2026-02-16 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/007-hybrid-eval-scheduler/spec.md`

## Summary

Provide concurrent CPU+GPU evaluation of NEAT populations within a single generation via a `HybridBatchEvaluator` that implements `IBatchEvaluator` as a decorator. The evaluator partitions the population across CPU and GPU backends using a configurable policy (static, cost-based, or adaptive PID controller), dispatches both backends concurrently via `Task.WhenAll`, and merges results through index-remapped `setFitness` callbacks. GPU failures are handled transparently with CPU fallback and periodic GPU re-probing. Per-generation scheduling metrics enable observability and feed the adaptive PID controller.

## Technical Context

**Language/Version**: C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>`
**Primary Dependencies**: Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (shared with core and GPU); ILGPU 1.5.x (transitive via NeatSharp.Gpu)
**Storage**: N/A (in-memory only; PID controller state and metrics held in-memory per scope)
**Testing**: xUnit + FluentAssertions; GPU integration tests gated with `[Trait("Category", "GPU")]`; unit tests use mock `IBatchEvaluator` instances
**Target Platform**: Windows + Linux; NVIDIA GPUs with CUDA compute capability >= 5.0 (when GPU is available)
**Project Type**: Library extension within existing `NeatSharp.Gpu` project (no new project required)
**Performance Goals**: Hybrid wall-clock time lower than either backend alone on transfer-dominated workloads; adaptive convergence within 10 generations (SC-002); scheduler overhead < 5% of slower backend time (FR-018)
**Constraints**: Single-GPU only; population sizes 50-10,000; CPU determinism preserved when hybrid disabled (SC-003)
**Scale/Scope**: 3 partitioning policies, 1 decorator evaluator, 1 metrics system, ~15 new types

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Correctness | PASS | Fitness results verified per-genome via index-remapped callbacks. No misalignment, duplication, or omission regardless of partition. FR-005 directly addresses this. |
| II. Reproducibility | PASS | CPU-only mode produces identical results whether hybrid is enabled or disabled (SC-003). GPU partition may produce fp32 results — accepted trade-off per 006-cuda-evaluator. Partition assignments are deterministic for same population order + same policy state. |
| III. Performance | PASS | This IS the performance feature. CPU fallback always exists. Benchmark report required (SC-007). Scheduler overhead budget enforced (FR-018). |
| IV. Developer Experience | PASS | `AddNeatSharpHybrid()` one-liner registration. Sensible defaults (adaptive policy, 50-genome threshold, PID gains). Same `evolver.RunAsync(evaluator)` call pattern. |
| V. Minimal Dependencies | PASS | No new external dependencies. All new code lives within existing `NeatSharp.Gpu` project which already depends on ILGPU. Core `NeatSharp` remains unchanged. |
| VI. SOLID | PASS | `HybridBatchEvaluator` implements `IBatchEvaluator` (decorator). `IPartitionPolicy` interface for strategy pattern. `ISchedulingMetricsReporter` for observability. All services injected via constructor. |
| VII. TDD | PASS | All hybrid code developed with Red-Green-Refactor. Mock `IBatchEvaluator` instances enable testing without GPU hardware. PID controller tested with synthetic metrics. |

**Coding Practices:**

| Practice | Status | Notes |
|----------|--------|-------|
| Naming & Style | PASS | .NET Runtime coding style. `NeatSharp.Gpu.Scheduling` namespace for hybrid types. |
| Nullability | PASS | `<Nullable>enable</Nullable>` with zero warnings. |
| Error Handling | PASS | GPU failures handled via existing `GpuEvaluationException` hierarchy. Transparent fallback per spec (no exceptions for expected failures). Scheduler validates config at startup (FR-015). |
| Resource Management | PASS | `HybridBatchEvaluator` implements `IDisposable`, forwards to inner evaluators. No new unmanaged resources (PID state and metrics are managed objects). |
| CUDA & Native Interop | N/A | No new CUDA interop. Hybrid scheduler operates at the managed `IBatchEvaluator` level, delegating GPU concerns to existing `GpuBatchEvaluator`. |
| Testing Conventions | PASS | xUnit, `{Method}_{Scenario}_{ExpectedResult}` naming. GPU integration tests gated with `[Trait("Category", "GPU")]`. |
| Dependency Injection | PASS | `AddNeatSharpHybrid()` extension method. Constructor injection for all services. Options validated via DataAnnotations. No service locator. |

## Project Structure

### Documentation (this feature)

```text
specs/007-hybrid-eval-scheduler/
├── plan.md              # This file
├── research.md          # Phase 0: PID, cost model, failure recovery decisions
├── data-model.md        # Phase 1: Hybrid entities and relationships
├── quickstart.md        # Phase 1: Usage guide
├── contracts/           # Phase 1: Public API interfaces
│   ├── HybridOptions.cs
│   ├── IPartitionPolicy.cs
│   ├── SchedulingMetrics.cs
│   └── ISchedulingMetricsReporter.cs
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── NeatSharp/                              # Existing core library (NO changes)
│   └── NeatSharp.csproj
│
└── NeatSharp.Gpu/                          # EXISTING: GPU acceleration package (EXTENDED)
    ├── NeatSharp.Gpu.csproj
    ├── Configuration/
    │   ├── GpuOptions.cs                   # Existing (unchanged)
    │   ├── GpuOptionsValidator.cs          # Existing (unchanged)
    │   ├── HybridOptions.cs                # NEW: Hybrid evaluation config
    │   └── HybridOptionsValidator.cs       # NEW: Hybrid options validation
    ├── Detection/                          # Existing (unchanged)
    ├── Evaluation/                         # Existing (unchanged)
    ├── Exceptions/                         # Existing (unchanged)
    ├── Extensions/
    │   └── ServiceCollectionExtensions.cs  # MODIFIED: Add AddNeatSharpHybrid()
    ├── Kernels/                            # Existing (unchanged)
    └── Scheduling/                         # NEW: Hybrid scheduler types
        ├── IPartitionPolicy.cs             # Partition policy abstraction
        ├── PartitionResult.cs              # Partition output record
        ├── StaticPartitionPolicy.cs        # Fixed-ratio split
        ├── CostBasedPartitionPolicy.cs     # Complexity-driven split
        ├── AdaptivePartitionPolicy.cs      # PID-controlled split
        ├── PidController.cs                # PID controller with anti-windup
        ├── HybridBatchEvaluator.cs         # IBatchEvaluator decorator
        ├── SchedulingMetrics.cs            # Per-generation metrics record
        ├── FallbackEventInfo.cs            # GPU fallback event details
        ├── ISchedulingMetricsReporter.cs   # Metrics reporter abstraction
        └── LoggingMetricsReporter.cs       # Default: log-based reporter

tests/
├── NeatSharp.Tests/                        # Existing (unchanged)
└── NeatSharp.Gpu.Tests/                    # EXISTING: GPU tests (EXTENDED)
    ├── NeatSharp.Gpu.Tests.csproj
    ├── Configuration/
    │   ├── GpuOptionsValidatorTests.cs     # Existing (unchanged)
    │   └── HybridOptionsValidatorTests.cs  # NEW: Hybrid options validation tests
    ├── Scheduling/                         # NEW: Hybrid scheduler tests
    │   ├── StaticPartitionPolicyTests.cs
    │   ├── CostBasedPartitionPolicyTests.cs
    │   ├── AdaptivePartitionPolicyTests.cs
    │   ├── PidControllerTests.cs
    │   ├── HybridBatchEvaluatorTests.cs
    │   └── SchedulingMetricsTests.cs
    └── Integration/
        ├── GpuTrainingIntegrationTests.cs  # Existing (unchanged)
        └── HybridTrainingIntegrationTests.cs  # NEW: End-to-end hybrid tests
```

**Structure Decision**: All new types are added to the existing `NeatSharp.Gpu` project under a new `Scheduling/` directory. This avoids creating a third project — the hybrid evaluator needs access to GPU internals (for GPU failure detection and re-probe coordination) and shares the same DI registration entry point. The core `NeatSharp` project requires no changes. Tests extend the existing `NeatSharp.Gpu.Tests` project.

## Complexity Tracking

> No constitution violations. All gates pass.
