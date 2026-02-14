# Implementation Plan: Genome Model + Innovation Tracking + Feed-Forward Phenotype

**Branch**: `002-genome-phenotype` | **Date**: 2026-02-13 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-genome-phenotype/spec.md`

## Summary

Implement the genome data model (immutable node genes, connection genes, typed nodes), a deterministic innovation tracker for structural mutations, an extensible activation function registry with five built-in functions, a feed-forward phenotype builder with topological sort and cycle detection, and supporting exception types — all integrated with the existing `IGenome` abstraction and DI infrastructure from Spec 001.

## Technical Context

**Language/Version**: C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>`
**Primary Dependencies**: Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2
**Storage**: N/A (in-memory; genome data structures only)
**Testing**: xUnit 2.9.3, FluentAssertions 7.0.0
**Target Platform**: Windows + Linux (cross-platform)
**Project Type**: Library (single project, extending existing codebase from Spec 001)
**Performance Goals**: Phenotype activation must be allocation-free after construction; topological sort pre-computed at build time
**Constraints**: Feed-forward only (no recurrent); single-threaded innovation tracking; CPU-only
**Scale/Scope**: ~12 new public types, ~3 new exception types, ~5 built-in activation functions

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Pre-Design | Post-Design | Notes |
|-----------|-----------|-------------|-------|
| I. Correctness | PASS | PASS | Innovation tracking implements canonical NEAT historical markings (FR-003, FR-004). Deterministic deduplication per generation. Phenotype evaluates in topological order per NEAT specification. |
| II. Reproducibility | PASS | PASS | Phenotype activation is deterministic (FR-014). Same genome + same inputs = same outputs. Innovation tracker assigns deterministic IDs. No stochastic operations in this feature. |
| III. Performance | PASS | PASS | CPU-only implementation. Topological sort pre-computed at phenotype build time. Activation is allocation-free (array reuse). Span-based API aligns with existing IGenome contract. |
| IV. Developer Experience | PASS | PASS | Genome construction from node/connection gene lists. Network builder provides a single conversion point. Built-in activation functions available out of the box. Specific exception types for each error condition (FR-015). |
| V. Minimal Dependencies | PASS | PASS | No new dependencies added. All implementations use only .NET BCL types. |
| VI. SOLID | PASS | PASS | SRP: Genome (data), NetworkBuilder (conversion), FeedForwardNetwork (evaluation), InnovationTracker (ID assignment), ActivationFunctionRegistry (function lookup) are all separate. OCP: activation functions extensible via registry. LSP: FeedForwardNetwork substitutable for IGenome. ISP: focused interfaces (IInnovationTracker, IActivationFunctionRegistry, INetworkBuilder). DIP: all dependencies injected via DI. |
| VII. TDD | PASS | PASS | All production code will follow Red-Green-Refactor. Test cases defined in spec acceptance scenarios. |
| DI Practices | PASS | PASS | IActivationFunctionRegistry (singleton), IInnovationTracker (scoped per evolution run), INetworkBuilder (singleton). All registered via AddNeatSharp(). Constructor injection only. |
| Observability | N/A | N/A | No observability requirements for this feature (genome/phenotype are data + computation types). |

**Gate result: ALL PASS** — no violations to justify.

## Project Structure

### Documentation (this feature)

```text
specs/002-genome-phenotype/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   ├── NodeType.cs
│   ├── NodeGene.cs
│   ├── ConnectionGene.cs
│   ├── Genome.cs
│   ├── IInnovationTracker.cs
│   ├── NodeSplitResult.cs
│   ├── IActivationFunctionRegistry.cs
│   ├── ActivationFunctions.cs
│   ├── INetworkBuilder.cs
│   ├── CycleDetectedException.cs
│   ├── InputDimensionMismatchException.cs
│   └── InvalidGenomeException.cs
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/NeatSharp/
├── Genetics/
│   ├── IGenome.cs                     # (existing — unchanged)
│   ├── NodeType.cs                    # NEW: enum (Input, Hidden, Output, Bias)
│   ├── NodeGene.cs                    # NEW: immutable record
│   ├── ConnectionGene.cs             # NEW: immutable record
│   ├── Genome.cs                      # NEW: immutable sealed class
│   ├── IInnovationTracker.cs          # NEW: interface
│   ├── InnovationTracker.cs           # NEW: implementation
│   ├── NodeSplitResult.cs            # NEW: readonly record struct
│   ├── IActivationFunctionRegistry.cs # NEW: interface
│   ├── ActivationFunctionRegistry.cs  # NEW: implementation
│   ├── ActivationFunctions.cs         # NEW: static class with built-ins
│   ├── INetworkBuilder.cs            # NEW: interface
│   ├── FeedForwardNetworkBuilder.cs   # NEW: INetworkBuilder implementation (builder + cycle detection)
│   └── FeedForwardNetwork.cs          # NEW: internal IGenome implementation (phenotype)
├── Exceptions/
│   ├── NeatSharpException.cs          # (existing — unchanged)
│   ├── CycleDetectedException.cs      # NEW
│   ├── InputDimensionMismatchException.cs # NEW
│   └── InvalidGenomeException.cs      # NEW
└── Extensions/
    └── ServiceCollectionExtensions.cs # MODIFIED: register new services

tests/NeatSharp.Tests/
├── Genetics/
│   ├── NodeGeneTests.cs               # NEW
│   ├── ConnectionGeneTests.cs         # NEW
│   ├── GenomeTests.cs                 # NEW
│   ├── InnovationTrackerTests.cs      # NEW
│   ├── ActivationFunctionRegistryTests.cs # NEW
│   ├── ActivationFunctionsTests.cs    # NEW
│   ├── FeedForwardNetworkTests.cs     # NEW
│   └── NetworkBuilderTests.cs         # NEW (INetworkBuilder, cycle detection, validation)
├── Exceptions/
│   ├── CycleDetectedExceptionTests.cs      # NEW
│   ├── InputDimensionMismatchExceptionTests.cs # NEW
│   └── InvalidGenomeExceptionTests.cs      # NEW
└── Extensions/
    └── ServiceCollectionExtensionsTests.cs # MODIFIED: verify new registrations
```

**Structure Decision**: Extends the existing .NET library layout from Spec 001. All new genetics types go into the existing `NeatSharp.Genetics` namespace (mirroring `src/NeatSharp/Genetics/` folder). Exception types extend the existing `NeatSharp.Exceptions` namespace. No new projects or sub-namespaces needed.

## Complexity Tracking

> No constitution violations detected. This section is intentionally empty.
