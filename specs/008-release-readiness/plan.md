# Implementation Plan: Release Readiness — Benchmarks, Examples, Docs, CI Gates & NuGet Packaging

**Branch**: `008-release-readiness` | **Date**: 2026-02-18 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/008-release-readiness/spec.md`

## Summary

Make the NeatSharp project announce-ready by adding NuGet packaging metadata (SemVer 0.1.0), a Cart-Pole example (the third required example), a comprehensive README, GitHub Actions CI pipelines (Windows + Linux, format check, test, pack, benchmark trend reporting), a BenchmarkDotNet-based benchmark suite with a local regression comparison tool, conceptual documentation covering NEAT basics through GPU troubleshooting, and contribution/release process documentation (CONTRIBUTING.md, PR template, release checklist, CHANGELOG).

## Technical Context

**Language/Version**: C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>`
**Primary Dependencies**: Existing — Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2, ILGPU 1.5.3, ILGPU.Algorithms 1.5.3; New — BenchmarkDotNet (benchmarks project only, not shipped in NuGet packages)
**Storage**: N/A (no new data storage; benchmark baselines stored as JSON files in version control)
**Testing**: xUnit + FluentAssertions; GPU tests gated with `[Trait("Category", "GPU")]`; Cart-Pole physics unit-tested with deterministic assertions
**Target Platform**: Windows + Linux; NuGet packages target net8.0 + net9.0; CI runs on GitHub-hosted ubuntu-latest + windows-latest
**Project Type**: Infrastructure + documentation + one new example; new `benchmarks/NeatSharp.Benchmarks` project for BenchmarkDotNet suite
**Performance Goals**: Benchmark suite measures CPU and GPU evaluation throughput across 3+ population sizes (150, 500, 1000, 5000); local regression detection at 10% threshold
**Constraints**: CI must complete within 10 minutes (SC-003); each example must complete within 60 seconds (FR-008); NuGet packages unsigned (non-goal for 0.1.0)
**Scale/Scope**: ~30 new files (docs, CI workflows, editorconfig, examples, benchmarks, contribution artifacts); 2 modified csproj files + Directory.Build.props

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Correctness | PASS | Cart-Pole example uses standard Barto/Stanley physics equations. Existing XOR and Sine examples already demonstrate correct NEAT behavior. No changes to NEAT algorithm code. |
| II. Reproducibility | PASS | All examples use fixed seeds and produce deterministic CPU output. Cart-Pole uses `options.Seed` for PRNG. Benchmark suite uses fixed seeds for genome generation to ensure comparable runs. |
| III. Performance | PASS | This feature builds the benchmark infrastructure to validate Principle III. BenchmarkDotNet provides statistical rigor (warmup, variance, confidence intervals). Local regression detection enforces 10% degradation threshold. |
| IV. Developer Experience | PASS | Core deliverable of this feature. README quickstart enables <20 lines to first run. Examples demonstrate 3 distinct problem domains. `dotnet add package NeatSharp` one-liner install. |
| V. Minimal Dependencies | PASS | BenchmarkDotNet is a dev/benchmark-only dependency — not shipped in NuGet packages. No new runtime dependencies added to NeatSharp or NeatSharp.Gpu. EditorConfig uses built-in `dotnet format`. CI uses GitHub Actions (no external services). |
| VI. SOLID | PASS | Cart-Pole example demonstrates DI pattern via `AddNeatSharp()` composition root. Fitness function injected via `IFitnessFunction`. No new production library types that would violate SOLID. |
| VII. TDD | PASS | Cart-Pole physics simulation unit-tested with deterministic assertions (known-state Euler integration). Benchmark comparison tool tested with synthetic baseline/current JSON pairs. CI workflow validated via intentional-failure test PR. |

**Coding Practices:**

| Practice | Status | Notes |
|----------|--------|-------|
| Naming & Style | PASS | `.editorconfig` created as part of this feature. `dotnet format` CI gate enforces compliance. One-time reformatting commit precedes gate activation. |
| Nullability | PASS | `<Nullable>enable</Nullable>` already enabled globally. New code follows existing patterns. |
| Error Handling | N/A | No new public API surfaces. Examples use existing API error paths. |
| Resource Management | N/A | No new unmanaged resources. Examples follow existing `using` patterns. |
| CUDA & Native Interop | N/A | No new CUDA interop. GPU benchmarks use existing `GpuBatchEvaluator`. |
| Testing Conventions | PASS | Cart-Pole physics tests follow `{Method}_{Scenario}_{ExpectedResult}` naming. xUnit + FluentAssertions. |
| Dependency Injection | PASS | Cart-Pole example uses `AddNeatSharp()` composition root. README quickstart demonstrates same pattern. |

## Project Structure

### Documentation (this feature)

```text
specs/008-release-readiness/
├── plan.md              # This file
├── research.md          # Phase 0: Benchmark strategy, CI configuration, Cart-Pole physics, editorconfig decisions
├── data-model.md        # Phase 1: Cart-Pole state, benchmark baseline schema, NuGet metadata structure
├── quickstart.md        # Phase 1: Implementation guide for all workstreams
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
# Root configuration files (NEW or MODIFIED)
.editorconfig                              # NEW: Code style rules for dotnet format enforcement
.gitattributes                             # NEW: Line ending normalization (LF for cross-platform CI)
Directory.Build.props                      # MODIFIED: Add Version 0.1.0, packaging metadata
NeatSharp.sln                              # MODIFIED: Add benchmarks project

# GitHub configuration (ALL NEW)
.github/
├── workflows/
│   ├── ci.yml                             # Primary CI: format, build/test matrix, pack, benchmark
│   └── docs.yml                           # README validation: link check, required sections
└── pull_request_template.md               # PR template with spec impact section

# Existing source projects (MODIFIED for packaging only)
src/
├── NeatSharp/
│   └── NeatSharp.csproj                   # MODIFIED: Add PackageId, Description, PackageTags, PackageReadmeFile
└── NeatSharp.Gpu/
    └── NeatSharp.Gpu.csproj               # MODIFIED: Add PackageId, Description, PackageTags, PackageReadmeFile

# Existing test projects (UNCHANGED)
tests/
├── NeatSharp.Tests/
└── NeatSharp.Gpu.Tests/

# Samples project (EXTENDED with Cart-Pole example)
samples/
└── NeatSharp.Samples/
    ├── NeatSharp.Samples.csproj           # UNCHANGED
    ├── Program.cs                         # MODIFIED: Add cart-pole command
    ├── CartPole/                           # NEW: Cart-Pole example
    │   ├── CartPoleSimulator.cs           # Physics simulation (Euler integration)
    │   ├── CartPoleConfig.cs              # Simulation parameters (configurable)
    │   └── CartPoleExample.cs             # Example runner with DI setup and console output
    ├── GpuBenchmark.cs                    # EXISTING (retained for quick smoke tests)
    └── HybridBenchmark.cs                 # EXISTING (retained for quick smoke tests)

# Benchmarks project (ALL NEW)
benchmarks/
└── NeatSharp.Benchmarks/
    ├── NeatSharp.Benchmarks.csproj        # net9.0; BenchmarkDotNet; IsPackable=false
    ├── Program.cs                         # BenchmarkDotNet entry point with category filters
    ├── CpuEvaluatorBenchmarks.cs          # CPU throughput across population sizes
    ├── GpuEvaluatorBenchmarks.cs          # GPU throughput (graceful skip when no GPU)
    └── HybridEvaluatorBenchmarks.cs       # Hybrid evaluation across partition policies

# Benchmark tooling (NEW)
tools/
└── benchmark-compare/
    ├── benchmark-compare.csproj           # net9.0 console tool; System.Text.Json only
    └── Program.cs                         # Reads two BDN JSON exports; compares Mean; reports >10% deltas

# Benchmark baseline (NEW)
benchmarks/
└── baseline.json                          # Versioned benchmark baseline (updated by maintainers)

# Documentation (ALL NEW except README.md rewrite)
README.md                                  # REWRITTEN: Full README per FR-021
LICENSE                                    # EXISTING (MIT, already present)
CONTRIBUTING.md                            # NEW: Development setup, build/test, code style, PR process
CHANGELOG.md                              # NEW: SemVer changelog with spec ID references
docs/
├── neat-basics.md                         # NEAT algorithm concepts as applied to NeatSharp
├── parameter-tuning.md                    # Parameter tuning guidance
├── reproducibility.md                     # Determinism guarantees (CPU vs GPU), seed usage
├── checkpointing.md                       # Serialization, checkpoint save/restore
├── gpu-setup.md                           # GPU prerequisites, configuration, performance tips
├── troubleshooting.md                     # GPU not detected, driver mismatch, OOM, training stalls
└── offline-usage.md                       # What works offline after initial install
```

**Structure Decision**: This feature is primarily infrastructure and documentation — it adds no new production library code to `NeatSharp` or `NeatSharp.Gpu`. The Cart-Pole example extends the existing `NeatSharp.Samples` project. The BenchmarkDotNet suite lives in a new `benchmarks/NeatSharp.Benchmarks` project (separate from the existing hand-rolled benchmarks in Samples, which are retained for quick developer smoke tests). The benchmark comparison tool is a standalone console app in `tools/`. All documentation goes to `docs/` as Markdown files per spec assumptions.

## Complexity Tracking

> No constitution violations. All gates pass.
>
> The only notable complexity addition is BenchmarkDotNet as a new dependency, which is restricted to the benchmarks project and never ships in NuGet packages. This is a standard .NET ecosystem choice aligned with the project's existing recommendation in `specs/006-cuda-evaluator/benchmark-report.md` ("Use BenchmarkDotNet for publication-quality data").
