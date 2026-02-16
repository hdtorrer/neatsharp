# Implementation Plan: CUDA Evaluator Backend + Auto GPU Use + Fallback

**Branch**: `006-cuda-evaluator` | **Date**: 2026-02-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/006-cuda-evaluator/spec.md`

## Summary

Provide GPU-accelerated batch evaluation of NEAT populations as a first-class capability using ILGPU, packaged as a separate `NeatSharp.Gpu` project. The system auto-detects compatible GPUs at startup, performs feed-forward propagation for all genomes in parallel on the GPU (fp32), computes fitness on the CPU, and falls back transparently to CPU evaluation when no GPU is available or on failure. A decorator `GpuNetworkBuilder` replaces the default `INetworkBuilder`, producing `GpuFeedForwardNetwork` instances that carry both flat GPU-ready topology arrays and a CPU-fallback phenotype. The `GpuBatchEvaluator` implements the existing `IBatchEvaluator` contract, extracting topology data from these networks to batch-evaluate on the GPU.

## Technical Context

**Language/Version**: C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>`
**Primary Dependencies**: ILGPU 1.5.x, ILGPU.Algorithms 1.5.x (GPU package only); Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (shared with core)
**Storage**: N/A (in-memory GPU buffers; `ILGPU.MemoryBuffer1D<T>` for device memory)
**Testing**: xUnit + FluentAssertions; GPU tests gated with `[Trait("Category", "GPU")]`; CPU accelerator used for logic tests without hardware
**Target Platform**: Windows + Linux; NVIDIA GPUs with CUDA compute capability >= 5.0 (Maxwell, ~2014+)
**Project Type**: Library (separate NuGet package `NeatSharp.Gpu` referencing core `NeatSharp`)
**Performance Goals**: >= 5x throughput over CPU on 500-2,000 genome populations; negligible startup overhead (<1s for GPU detection)
**Constraints**: fp32 GPU precision with 1e-4 epsilon tolerance vs. CPU fp64 baseline; single GPU only; GPU memory scales with population size × max genome complexity
**Scale/Scope**: Populations of 150-10,000 genomes; 5 built-in activation functions; kernel handles heterogeneous topologies via offset-indexed flat arrays

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Correctness | PASS | GPU results validated against CPU baseline within epsilon (1e-4). Degenerate genomes (zero connections) handled gracefully. |
| II. Reproducibility | PASS | CPU path remains fully deterministic. GPU provides best-effort determinism (sequential accumulation per thread, fixed thread assignment). Documented trade-offs per spec US-4. |
| III. Performance | PASS | This IS the performance feature. CPU fallback always exists and is correct. Benchmark suite validates GPU speedup before merge. |
| IV. Developer Experience | PASS | `AddNeatSharpGpu()` one-liner registration. User provides `IGpuFitnessFunction` (test cases + fitness computation). Same `evolver.RunAsync(batchEvaluator)` call pattern. |
| V. Minimal Dependencies | PASS | ILGPU dependency isolated to `NeatSharp.Gpu` package only. Core `NeatSharp` has zero GPU dependencies. CPU-only usage requires no CUDA installation. |
| VI. SOLID | PASS | GPU evaluator implements `IBatchEvaluator`. GPU network builder implements `INetworkBuilder` (decorator). All GPU services injected via constructor. |
| VII. TDD | PASS | All GPU code developed with Red-Green-Refactor. ILGPU CPU accelerator enables GPU logic testing without hardware. Hardware tests gated with GPU trait. |

**Coding Practices:**

| Practice | Status | Notes |
|----------|--------|-------|
| Naming & Style | PASS | .NET Runtime coding style. `NeatSharp.Gpu.*` namespaces mirror folder structure. |
| Nullability | PASS | `<Nullable>enable</Nullable>` with zero warnings. |
| Error Handling | PASS | GPU exceptions derive from `NeatSharpException`. No generic `Exception` throws. Expected failures (GPU unavailable) use status checks, not exceptions. |
| Resource Management | PASS | `GpuBatchEvaluator` implements `IDisposable` (full pattern). GPU buffers pooled across generations. `using` at all call sites. |
| CUDA & Native Interop | PASS with deviation | ILGPU is a managed .NET GPU compiler — no P/Invoke or `DllImport`. ILGPU handles PTX compilation and native library loading internally. Constitution's `NeatSharp.Native` namespace and `NativeLibrary.Load` requirements do not apply (justified: ILGPU abstracts these concerns). GPU kernel code isolated in `NeatSharp.Gpu.Kernels` namespace. Public API wrappers accept managed types only (no `IntPtr`). |
| Testing Conventions | PASS | xUnit, `[Trait("Category", "GPU")]`, `{Method}_{Scenario}_{ExpectedResult}` naming. Test projects do not reference ILGPU internals directly. |
| Dependency Injection | PASS | `AddNeatSharpGpu()` extension method. Constructor injection for all services. Options validated via DataAnnotations. No service locator. |

## Project Structure

### Documentation (this feature)

```text
specs/006-cuda-evaluator/
├── plan.md              # This file
├── research.md          # Phase 0: ILGPU integration decisions
├── data-model.md        # Phase 1: GPU data structures and entities
├── quickstart.md        # Phase 1: Usage guide
├── contracts/           # Phase 1: Public API interfaces
│   ├── IGpuFitnessFunction.cs
│   ├── IGpuDeviceInfo.cs
│   └── GpuOptions.cs
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── NeatSharp/                          # Existing core library (NO changes)
│   └── NeatSharp.csproj
│
└── NeatSharp.Gpu/                      # NEW: GPU acceleration package
    ├── NeatSharp.Gpu.csproj
    ├── Configuration/
    │   ├── GpuOptions.cs               # GPU configuration options
    │   └── GpuOptionsValidator.cs      # Options validation
    ├── Detection/
    │   ├── IGpuDeviceDetector.cs        # GPU detection contract
    │   ├── IGpuDeviceInfo.cs            # Device info contract
    │   ├── GpuDeviceDetector.cs         # ILGPU device detection
    │   └── GpuDeviceInfo.cs             # Device capability info record
    ├── Evaluation/
    │   ├── IGpuFitnessFunction.cs       # User-defined test cases + fitness
    │   ├── GpuBatchEvaluator.cs         # IBatchEvaluator implementation
    │   ├── GpuNetworkBuilder.cs         # INetworkBuilder decorator
    │   ├── GpuFeedForwardNetwork.cs     # IGenome with flat GPU topology
    │   └── GpuPopulationData.cs         # Flattened batch topology buffers
    ├── Exceptions/
    │   ├── GpuEvaluationException.cs    # GPU evaluation failure
    │   ├── GpuOutOfMemoryException.cs   # GPU memory exhaustion
    │   └── GpuDeviceException.cs        # Device incompatibility
    ├── Extensions/
    │   └── ServiceCollectionExtensions.cs  # AddNeatSharpGpu()
    └── Kernels/
        ├── ActivationKernels.cs         # GPU activation functions (fp32)
        └── ForwardPropagationKernel.cs  # Batch forward propagation

tests/
├── NeatSharp.Tests/                    # Existing tests (unchanged)
└── NeatSharp.Gpu.Tests/               # NEW: GPU test project
    ├── NeatSharp.Gpu.Tests.csproj
    ├── Configuration/
    │   └── GpuOptionsValidatorTests.cs
    ├── Detection/
    │   └── GpuDeviceDetectorTests.cs
    ├── Evaluation/
    │   ├── GpuBatchEvaluatorTests.cs
    │   ├── GpuNetworkBuilderTests.cs
    │   └── GpuFeedForwardNetworkTests.cs
    └── Integration/
        └── GpuTrainingIntegrationTests.cs

samples/
└── NeatSharp.Samples/                  # Update existing sample with GPU example
```

**Structure Decision**: Two new projects (`NeatSharp.Gpu` + `NeatSharp.Gpu.Tests`) added alongside existing projects. This follows the spec requirement for a separate GPU package with no ILGPU dependency in core. The core `NeatSharp` project requires no changes — the GPU layer depends only on public API types (`INetworkBuilder`, `IGenome`, `Genome`).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| ILGPU managed interop instead of `NeatSharp.Native` namespace | ILGPU compiles C# to PTX at runtime — no P/Invoke, no `DllImport`, no native binaries to load. The constitution's native interop rules target raw CUDA interop. | Wrapping ILGPU calls in a `NeatSharp.Native` namespace would add an unnecessary indirection layer with no safety benefit, since ILGPU is already a managed abstraction. |
| ILGPU `ArrayView<T>` instead of `Span<T>` in kernel code | Constitution requires `ReadOnlySpan<T>`/`Span<T>` for kernel buffer parameters. ILGPU kernels operate on device memory via `ArrayView<T>` — `Span<T>` cannot address GPU device memory. Public API surfaces (`IGpuFitnessFunction.ComputeFitness`, `GpuBatchEvaluator`) use `ReadOnlySpan<T>`/`Span<T>` for CPU-side buffers. The spirit of the rule (no `IntPtr` in public signatures) is maintained. | Wrapping `ArrayView<T>` in `Span<T>` adapters is physically impossible for device memory. The constitution's rule was written for raw P/Invoke scenarios where `Span<T>` replaces `IntPtr`. |

