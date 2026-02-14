# Implementation Plan: Training Runner + Evaluation Adapters + Reporting

**Branch**: `004-training-runner` | **Date**: 2026-02-14 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/004-training-runner/spec.md`

## Summary

Implement the end-to-end NEAT evolution loop that orchestrates population initialization, fitness evaluation, speciation, selection, and reproduction across generations. This includes the real `INeatEvolver` implementation (replacing the current stub), population initialization for minimal-topology genomes, metrics collection, structured logging, champion tracking, stopping criteria, cancellation support, and two runnable examples (XOR, function approximation). All existing building blocks (mutation, crossover, speciation, selection, evaluation adapters, reporting records) are in place; this spec wires them into a complete training pipeline.

## Technical Context

**Language/Version**: C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted
**Primary Dependencies**: Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2
**Storage**: N/A (in-memory only)
**Testing**: xUnit + FluentAssertions (via existing `NeatSharp.Tests` project)
**Target Platform**: Windows + Linux (cross-platform .NET)
**Project Type**: Single library project + single test project (existing)
**Performance Goals**: Zero overhead when metrics disabled (FR-019); sequential CPU baseline (no parallelism)
**Constraints**: Deterministic reproducibility from seeded PRNG (FR-005); cancellation at generation boundaries (FR-004)
**Scale/Scope**: XOR solvable within 150 generations; function approximation within 500 generations

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Correctness | PASS | Loop follows canonical NEAT: evaluate → speciate → select → reproduce. Innovation tracker `NextGeneration()` called between generations. |
| II. Reproducibility | PASS | Single seeded `Random` instance consumed in deterministic order. All stochastic operations flow through this PRNG. |
| III. Performance | PASS | CPU-only sequential path. No GPU code in scope. |
| IV. Developer Experience | PASS | `INeatEvolver.RunAsync` + convenience extensions already defined. < 20 lines for end-to-end usage via examples. |
| V. Minimal Dependencies | PASS | No new dependencies. Uses existing M.E.DI, M.E.Options, M.E.Logging abstractions. |
| VI. SOLID | PASS | Training loop depends on abstractions (`IEvaluationStrategy`, `ISpeciationStrategy`, `IParentSelector`, etc.). Population factory injected via interface. All components replaceable. |
| VII. TDD | PASS | All new types will follow Red-Green-Refactor. Tests in same PR. |
| DI Composition Root | PASS | `NeatEvolver` replaces `NeatEvolverStub` in `AddNeatSharp()`. New `IPopulationFactory` registered. No service locator usage. |

**Post-Design Re-check**: All gates still pass. No constitution violations introduced.

## Project Structure

### Documentation (this feature)

```text
specs/004-training-runner/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (C# interfaces — no REST/GraphQL)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/NeatSharp/
├── Configuration/
│   └── NeatSharpOptions.cs          # Add InputCount, OutputCount properties
├── Evolution/
│   ├── NeatEvolver.cs               # NEW: Real INeatEvolver implementation
│   ├── IPopulationFactory.cs        # NEW: Interface for creating initial population
│   ├── PopulationFactory.cs         # NEW: Creates minimal-topology genomes
│   └── TrainingLog.cs               # NEW: [LoggerMessage] source-generated structured logging
├── Extensions/
│   └── ServiceCollectionExtensions.cs  # Update: replace NeatEvolverStub, register IPopulationFactory
└── Reporting/
    └── GenerationStatistics.cs      # Existing (already defined)

tests/NeatSharp.Tests/
├── Evolution/
│   ├── NeatEvolverTests.cs          # NEW: Training loop integration tests
│   └── PopulationFactoryTests.cs    # NEW: Population initialization tests
└── Examples/
    ├── XorExampleTests.cs           # NEW: XOR end-to-end validation
    └── FunctionApproximationExampleTests.cs  # NEW: Function approx validation
```

**Structure Decision**: Follows existing single-project structure (`src/NeatSharp` + `tests/NeatSharp.Tests`). New files placed in existing namespace folders matching the established pattern. No new projects needed.

## Complexity Tracking

> No constitution violations. Table intentionally left empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
