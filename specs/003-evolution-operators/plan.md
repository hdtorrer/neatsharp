# Implementation Plan: Evolution Operators (Mutation/Crossover) + Speciation + Selection

**Branch**: `003-evolution-operators` | **Date**: 2026-02-13 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-evolution-operators/spec.md`

## Summary

Implement the core NEAT evolutionary mechanics: five mutation operators (weight perturbation, weight replacement, add-connection, add-node, toggle-enable), innovation-aligned crossover, compatibility-distance speciation, fitness-proportional selection with elitism/stagnation, and optional complexity penalties. All operators produce immutable genomes, use the existing `InnovationTracker`, and are deterministically reproducible via seeded PRNG. Three injectable `IParentSelector` implementations (tournament, roulette wheel, SUS) support extensibility via DI.

## Technical Context

**Language/Version**: C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted
**Primary Dependencies**: Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2
**Storage**: N/A (in-memory; genome data structures only)
**Testing**: xUnit 2.9.3 + FluentAssertions 7.0.0
**Target Platform**: Windows + Linux (cross-platform .NET)
**Project Type**: Single library + test project
**Performance Goals**: N/A (correctness-first; single-threaded CPU execution per spec non-goals)
**Constraints**: Single-threaded execution, feed-forward only (no recurrent), deterministic reproducibility
**Scale/Scope**: Population size up to 100,000 (per `NeatSharpOptions.PopulationSize` range)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Correctness | PASS | Implementing canonical NEAT: innovation-aligned crossover, speciation via compatibility distance, fitness sharing. All per Kenneth Stanley's original paper. |
| II. Reproducibility | PASS | All stochastic operations consume randomness from a single seeded PRNG in deterministic order. FR-007, FR-014, FR-019, FR-025, FR-028 mandate this. |
| III. Performance | PASS (N/A) | CPU-only scope. No GPU kernels introduced. No benchmark required per constitution (no performance-sensitive claims). |
| IV. Developer Experience | PASS | Sensible defaults for all rates/thresholds (NEAT paper values). Injectable `IParentSelector` for advanced users. No required configuration beyond what Spec 01 already provides. |
| V. Minimal Dependencies | PASS | No new dependencies added. Uses only existing Microsoft.Extensions packages. |
| VI. SOLID Design | PASS | Single-responsibility mutation operators, injectable `IParentSelector`, `ISpeciationStrategy` for speciation, `ICrossoverOperator`/`IMutationOperator` interfaces. DI-wired throughout. |
| VII. TDD | PASS | All new code follows Red-Green-Refactor. Fixed-seed deterministic tests for all stochastic operations. |
| DI Composition Root | PASS | New services registered via `AddNeatSharp()` extension. No service locator. Constructor injection only. |
| Naming & Style | PASS | PascalCase public types, `_camelCase` private fields, one type per file, namespace mirrors folder. |
| Nullability | PASS | Nullable reference types enabled project-wide. No null parameters unless explicitly typed nullable. |

## Project Structure

### Documentation (this feature)

```text
specs/003-evolution-operators/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── mutation.md
│   ├── crossover.md
│   ├── speciation.md
│   └── selection.md
└── tasks.md             # Phase 2 output (NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/NeatSharp/
├── Configuration/
│   ├── NeatSharpOptions.cs          # Extended with evolution config properties
│   ├── MutationOptions.cs           # NEW: mutation rates and parameters
│   ├── CrossoverOptions.cs          # NEW: crossover parameters
│   ├── SpeciationOptions.cs         # NEW: compatibility distance coefficients/threshold
│   ├── SelectionOptions.cs          # NEW: elitism, stagnation, survival threshold
│   ├── ComplexityLimits.cs          # Existing (no changes)
│   ├── StoppingCriteria.cs          # Existing (no changes)
│   └── WeightDistributionType.cs    # NEW: enum for Uniform/Gaussian
├── Evolution/
│   ├── INeatEvolver.cs              # Existing interface (no changes)
│   ├── EvolutionResult.cs           # Existing (no changes)
│   ├── Mutation/
│   │   ├── IMutationOperator.cs     # NEW: interface
│   │   ├── WeightPerturbationMutation.cs
│   │   ├── WeightReplacementMutation.cs
│   │   ├── AddConnectionMutation.cs
│   │   ├── AddNodeMutation.cs
│   │   ├── ToggleEnableMutation.cs
│   │   └── CompositeMutationOperator.cs  # NEW: applies mutations by configured rates
│   ├── Crossover/
│   │   ├── ICrossoverOperator.cs    # NEW: interface
│   │   └── NeatCrossover.cs         # NEW: implementation
│   ├── Speciation/
│   │   ├── ICompatibilityDistance.cs # NEW: interface
│   │   ├── CompatibilityDistance.cs  # NEW: implementation
│   │   ├── ISpeciationStrategy.cs   # NEW: interface
│   │   ├── CompatibilitySpeciation.cs # NEW: implementation
│   │   └── Species.cs               # NEW: mutable species tracking
│   └── Selection/
│       ├── IParentSelector.cs       # NEW: injectable interface
│       ├── TournamentSelector.cs    # NEW: default, size 2
│       ├── RouletteWheelSelector.cs # NEW
│       ├── StochasticUniversalSamplingSelector.cs # NEW
│       ├── ReproductionAllocator.cs # NEW: offspring allocation logic
│       └── ReproductionOrchestrator.cs # NEW: per-offspring reproduction loop
├── Genetics/
│   └── (existing files unchanged)
├── Extensions/
│   └── ServiceCollectionExtensions.cs # Updated: register new evolution services
└── (other existing directories unchanged)

tests/NeatSharp.Tests/
├── Evolution/
│   ├── Mutation/
│   │   ├── WeightPerturbationMutationTests.cs
│   │   ├── WeightReplacementMutationTests.cs
│   │   ├── AddConnectionMutationTests.cs
│   │   ├── AddNodeMutationTests.cs
│   │   ├── ToggleEnableMutationTests.cs
│   │   └── CompositeMutationOperatorTests.cs
│   ├── Crossover/
│   │   └── NeatCrossoverTests.cs
│   ├── Speciation/
│   │   ├── CompatibilityDistanceTests.cs
│   │   └── CompatibilitySpeciationTests.cs
│   └── Selection/
│       ├── TournamentSelectorTests.cs
│       ├── RouletteWheelSelectorTests.cs
│       ├── StochasticUniversalSamplingSelectorTests.cs
│       ├── ReproductionAllocatorTests.cs
│       └── ReproductionOrchestratorTests.cs
└── (existing test directories unchanged)
```

**Structure Decision**: Extends the existing `src/NeatSharp` project with new subdirectories under `Evolution/` organized by domain concern (Mutation, Crossover, Speciation, Selection). Mirrors existing namespace-folder conventions. No new projects needed — all code lives in the single `NeatSharp` library.

## Complexity Tracking

No constitution violations detected. No complexity justifications required.
