# Implementation Plan: Core Package, Public API & Reproducibility Baseline

**Branch**: `001-core-api-baseline` | **Date**: 2026-02-12 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-core-api-baseline/spec.md`

## Summary

Establish the minimal public API surface for the NeatSharp library: a .NET 8/9 multi-targeted NuGet package with DI registration, configuration via the Options pattern, pluggable evaluation strategies (simple, environment, batch), deterministic seeded runs (CPU), structured logging/metrics, and a quickstart path to a working NEAT evolution in <20 lines of user code.

## Technical Context

**Language/Version**: C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>`
**Primary Dependencies**: Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2
**Storage**: N/A (in-memory evolution; no persistence in this feature scope)
**Testing**: xUnit 2.9.3, FluentAssertions 7.0.0, Microsoft.NET.Test.Sdk 18.0.1, coverlet.collector 6.0.4
**Target Platform**: Windows + Linux (cross-platform; FR-015, Principle V)
**Project Type**: Library (NuGet package)
**Performance Goals**: N/A for this feature — CPU-only baseline; no runtime performance targets defined
**Constraints**: CPU-only evaluation; no native dependencies required (FR-016); nullable reference types enabled project-wide
**Scale/Scope**: Single library package (~15-20 public types), single test project

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Pre-Design | Post-Design | Notes |
|-----------|-----------|-------------|-------|
| I. Correctness | PASS | PASS | API contracts enable canonical NEAT (innovation numbers, speciation, crossover, fitness sharing). Algorithm internals out of scope for this feature. |
| II. Reproducibility | PASS | PASS | FR-009/FR-010: `NeatSharpOptions.Seed` captures seed; auto-generated when null. Single seeded PRNG consumed in deterministic order. |
| III. Performance | PASS | PASS | CPU-only baseline. `IEvaluationStrategy` abstraction allows GPU backends in future features without modifying existing code. |
| IV. Developer Experience | PASS | PASS | SC-004: <20 LOC. Sensible defaults via Options pattern. `AddNeatSharp()` one-liner registration. Convenience extension methods for common patterns. FR-014: actionable validation errors. |
| V. Minimal Dependencies | PASS | PASS | Only M.E.DI.Abstractions, M.E.Options, M.E.Logging.Abstractions. Constitution explicitly accepts DI abstractions as standard .NET ecosystem contract. |
| VI. SOLID | PASS | PASS | SRP: focused types (config, evaluation, reporting separate). OCP: `IEvaluationStrategy` + factory methods for extensibility. LSP: all strategy implementations substitutable. ISP: `INeatEvolver` single method; convenience via extension methods. DIP: constructor injection throughout; `AddNeatSharp` composition root. |
| VII. TDD | PASS | PASS | Implementation will follow Red-Green-Refactor. Tests committed in same PR as production code. |
| DI Practices | PASS | PASS | `AddNeatSharp(IServiceCollection, Action<NeatSharpOptions>?)` extension. Constructor injection only. Scoped per-run services via `IServiceScopeFactory`. No service locator. |
| Observability | PARTIAL PASS | PARTIAL PASS | Summary report: FR-019/IRunReporter. Metrics history: FR-008/RunHistory. Champion genome: FR-006/Champion. Checkpoint files: out of scope for this feature (no persistence layer); to be addressed in a future serialization/persistence feature. |

**Gate result: ALL PASS (Observability PARTIAL — checkpoint files deferred to persistence feature)** — no violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/001-core-api-baseline/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── INeatEvolver.cs
│   ├── IEvaluationStrategy.cs
│   ├── IEnvironmentEvaluator.cs
│   ├── IBatchEvaluator.cs
│   ├── IRunReporter.cs
│   └── IGenome.cs
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
NeatSharp.sln
Directory.Build.props

src/
└── NeatSharp/
    ├── NeatSharp.csproj
    ├── Configuration/
    │   ├── NeatSharpOptions.cs
    │   ├── StoppingCriteria.cs
    │   └── ComplexityLimits.cs
    ├── Evaluation/
    │   ├── IEvaluationStrategy.cs
    │   ├── IEnvironmentEvaluator.cs
    │   ├── IBatchEvaluator.cs
    │   └── EvaluationStrategy.cs
    ├── Evolution/
    │   ├── INeatEvolver.cs
    │   └── EvolutionResult.cs
    ├── Genetics/
    │   └── IGenome.cs
    ├── Reporting/
    │   ├── Champion.cs
    │   ├── RunHistory.cs
    │   ├── GenerationStatistics.cs
    │   ├── ComplexityStatistics.cs
    │   ├── GenomeInfo.cs
    │   ├── IRunReporter.cs
    │   ├── TimingBreakdown.cs
    │   ├── PopulationSnapshot.cs
    │   ├── RunReporter.cs
    │   └── SpeciesSnapshot.cs
    ├── Exceptions/
    │   └── NeatSharpException.cs
    └── Extensions/
        └── ServiceCollectionExtensions.cs

tests/
└── NeatSharp.Tests/
    ├── NeatSharp.Tests.csproj
    ├── Configuration/
    │   ├── ComplexityLimitsTests.cs
    │   ├── NeatSharpOptionsTests.cs
    │   └── StoppingCriteriaTests.cs
    ├── Evaluation/
    │   └── EvaluationStrategyTests.cs
    ├── Evolution/
    │   └── EvolutionResultTests.cs
    ├── Exceptions/
    │   └── NeatSharpExceptionTests.cs
    ├── Extensions/
    │   └── ServiceCollectionExtensionsTests.cs
    └── Reporting/
        └── RunReporterTests.cs
```

**Structure Decision**: Standard .NET library layout with `src/` and `tests/` directories. Single library project (`NeatSharp`) multi-targeting net8.0;net9.0. Single test project (`NeatSharp.Tests`) also multi-targeting to validate both runtimes. `Directory.Build.props` centralizes shared MSBuild settings (LangVersion, Nullable, TreatWarningsAsErrors). Namespaces mirror folder structure per constitution (e.g., `NeatSharp.Configuration`, `NeatSharp.Evaluation`).

## Complexity Tracking

> No constitution violations detected. This section is intentionally empty.
