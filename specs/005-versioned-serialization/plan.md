# Implementation Plan: Versioned Serialization + Checkpoint/Resume + Artifact Export

**Branch**: `005-versioned-serialization` | **Date**: 2026-02-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/005-versioned-serialization/spec.md`

## Summary

Add checkpoint persistence, deterministic resume, champion export, and diagnostics bundles to NEATSharp. Checkpoints capture the complete mutable training state (population, species, innovation counters, RNG state, configuration, run history) at generation boundaries and serialize it as JSON via `System.Text.Json` to consumer-provided streams. Resume restores this state and continues evolution with results identical to an uninterrupted run on the same CPU. Champion export produces a standalone JSON graph parseable without the NEATSharp library. All artifacts are stamped with a semantic schema version (initially 1.0.0), library version, configuration hash, and environment metadata. A migration infrastructure is in place for future schema evolution.

## Technical Context

**Language/Version**: C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>`
**Primary Dependencies**: System.Text.Json (in-box), Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2
**Storage**: Stream-based I/O (no filesystem dependency; `System.IO.Stream` abstraction)
**Testing**: xUnit (existing test project `NeatSharp.Tests`)
**Target Platform**: .NET 8+9 cross-platform (Windows + Linux)
**Project Type**: Single library project (`src/NeatSharp/`)
**Performance Goals**: Serialization is I/O-bound; no specific throughput target. Checkpoint save/load should not dominate training time for typical populations (up to 10K genomes).
**Constraints**: No new NuGet dependencies beyond System.Text.Json (in-box). All existing APIs remain backward-compatible.
**Scale/Scope**: Population sizes up to 100,000 genomes (per `NeatSharpOptions.PopulationSize` range). Checkpoints for a 10K-genome population should be <100 MB JSON.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Correctness | PASS | Checkpoint round-trip fidelity ensures training state is preserved exactly. Structural validation on load (FR-023) catches corruption. |
| II. Reproducibility | PASS | Deterministic resume is the core requirement (FR-004). RNG state capture/restore ensures identical continuation. |
| III. Performance | PASS | Serialization is not on the hot path. No GPU concerns. CPU fallback is the only path. |
| IV. Developer Experience | PASS | Simple API: `Save`/`Load`/`Resume`. Checkpoint callback for mid-run saves. Champion export is one method call. Diagnostics bundle is one method call. |
| V. Minimal Dependencies | PASS | System.Text.Json is in-box (no new dependency). Stream abstraction decouples from storage. |
| VI. SOLID Design | PASS | Serialization is separated from domain logic (new `Serialization` namespace). Interfaces for all public contracts. DI registration via `AddNeatSharp()`. |
| VII. TDD | PASS | All new code will follow Red-Green-Refactor. Round-trip tests, resume determinism tests, validation tests, export tests. |
| Naming & Style | PASS | .NET Runtime Coding Style. PascalCase public, `_camelCase` private. One type per file. Namespace mirrors folder. |
| Nullability | PASS | Nullable enabled project-wide. No null parameters in public APIs unless explicitly nullable. |
| Error Handling | PASS | Custom exceptions under `NeatSharpException` hierarchy. Actionable error messages (FR-008, FR-023). |
| DI | PASS | New services registered via `AddNeatSharp()`. Constructor injection. Scoped/singleton lifetimes. No service locator. |

**Pre-Phase 0 Gate: PASS** — No violations.

## Project Structure

### Documentation (this feature)

```text
specs/005-versioned-serialization/
├── plan.md              # This file
├── research.md          # Phase 0: RNG state, config hashing, STJ patterns, migration design
├── data-model.md        # Phase 1: all serialization DTOs and domain models
├── quickstart.md        # Phase 1: consumer usage examples
├── contracts/           # Phase 1: C# interface contracts
│   ├── ICheckpointSerializer.cs
│   ├── IChampionExporter.cs
│   ├── IDiagnosticsBundleCreator.cs
│   └── ICheckpointValidator.cs
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/NeatSharp/
├── Configuration/                       # Existing — no changes
├── Evaluation/                          # Existing — no changes
├── Evolution/
│   ├── Crossover/                       # Existing — no changes
│   ├── Mutation/                        # Existing — no changes
│   ├── Selection/                       # Existing — no changes
│   ├── Speciation/
│   │   ├── CompatibilitySpeciation.cs   # MODIFIED: add NextSpeciesId property, RestoreState()
│   │   ├── ISpeciationStrategy.cs       # MODIFIED: add NextSpeciesId property
│   │   └── Species.cs                   # Existing — no changes
│   ├── EvolutionResult.cs               # Existing — no changes
│   ├── EvolutionRunOptions.cs           # NEW: options for checkpoint/resume
│   ├── INeatEvolver.cs                  # MODIFIED: add RunAsync overload with EvolutionRunOptions
│   ├── NeatEvolver.cs                   # MODIFIED: support resume + checkpoint callback
│   └── PopulationFactory.cs             # Existing — no changes
├── Exceptions/
│   ├── CheckpointException.cs           # NEW: base checkpoint exception
│   ├── CheckpointCorruptionException.cs # NEW: structural validation failure
│   ├── NeatSharpException.cs            # Existing — no changes
│   └── SchemaVersionException.cs        # NEW: version mismatch
├── Extensions/
│   └── ServiceCollectionExtensions.cs   # MODIFIED: register serialization services
├── Genetics/
│   ├── IInnovationTracker.cs            # MODIFIED: add NextInnovationNumber, NextNodeId properties
│   ├── InnovationTracker.cs             # MODIFIED: implement new properties, add RestoreState()
│   └── ...                              # Existing — no changes
├── Reporting/                           # Existing — no changes
└── Serialization/                       # NEW namespace
    ├── ArtifactMetadata.cs              # Domain model: common artifact header
    ├── CheckpointSerializer.cs          # Implementation: STJ-based save/load
    ├── CheckpointValidator.cs           # Structural validation (FR-023)
    ├── ChampionExporter.cs              # Champion export implementation
    ├── ConfigurationHasher.cs           # Deterministic config hash (SHA-256)
    ├── DiagnosticsBundleCreator.cs      # Diagnostics bundle implementation
    ├── EnvironmentInfo.cs               # OS/runtime/architecture metadata
    ├── ICheckpointSerializer.cs         # Interface: save/load checkpoints
    ├── ICheckpointValidator.cs          # Interface: structural validation
    ├── IChampionExporter.cs             # Interface: champion export
    ├── IDiagnosticsBundleCreator.cs     # Interface: diagnostics bundle
    ├── RngState.cs                      # RNG internal state capture
    ├── RngStateHelper.cs                # Reflection-based RNG state capture/restore
    ├── SchemaVersion.cs                 # Semantic version constants and comparison
    ├── SpeciesCheckpoint.cs             # Species state for checkpoint
    ├── TrainingCheckpoint.cs            # Complete checkpoint domain model
    ├── Dto/                             # JSON serialization DTOs
    │   ├── ArtifactMetadataDto.cs
    │   ├── CheckpointDto.cs
    │   ├── ChampionExportDto.cs
    │   ├── ConnectionGeneDto.cs
    │   ├── DiagnosticsBundleDto.cs
    │   ├── GenomeDto.cs
    │   ├── NodeGeneDto.cs
    │   ├── RngStateDto.cs
    │   └── SpeciesCheckpointDto.cs
    └── Migration/                       # Schema version migration infrastructure
        ├── ISchemaVersionMigrator.cs
        └── SchemaVersionMigrator.cs

tests/NeatSharp.Tests/
├── Serialization/                       # NEW test namespace
│   ├── CheckpointRoundTripTests.cs
│   ├── CheckpointValidatorTests.cs
│   ├── ChampionExporterTests.cs
│   ├── ConfigurationHasherTests.cs
│   ├── DiagnosticsBundleTests.cs
│   ├── ResumeDeterminismTests.cs
│   ├── RngStateHelperTests.cs
│   ├── SchemaVersionTests.cs
│   └── BackwardCompatibilityTests.cs
└── ...                                  # Existing tests — no changes
```

**Structure Decision**: All new serialization code lives in the `NeatSharp.Serialization` namespace within the existing `src/NeatSharp/` project. No new projects are needed — this follows the single-project pattern established by all prior specs. Tests go in the existing `tests/NeatSharp.Tests/` project under a `Serialization/` folder.

## Complexity Tracking

> No constitution violations — section intentionally left empty.

## Key Design Decisions

### 1. RNG State Serialization via Reflection

**Decision**: Use reflection to extract and restore the internal state of `System.Random` (seeded variant).

**Rationale**: In .NET 8+, `new Random(int seed)` creates a `Net5CompatSeedImpl` internally (the legacy Knuth subtractive RNG for backward compatibility). Its state consists of `int[] _seedArray` (56 elements), `int _inext`, and `int _inextp`. These three fields fully determine the RNG's future output sequence. We capture them via reflection at checkpoint time and restore them on resume.

**Alternatives considered**:
- **Custom PRNG class**: Implement our own Xoshiro256** or Knuth SRNG that extends `Random` with public state. Rejected because it requires changing how `Random` is created/passed throughout the codebase, and the reflection approach is simpler for v1.
- **Replay from seed**: Record the seed and replay all random calls to reach the checkpoint state. Rejected because it's impractical for large runs (millions of random calls).
- **Third-party PRNG library**: Use a library like Redzen. Rejected per Principle V (minimal dependencies).

**Risk mitigation**: Unit tests validate that capture → restore produces identical random sequences on both .NET 8 and .NET 9. If a future .NET version changes the internal implementation, these tests will fail immediately.

### 2. DTO Layer for JSON Serialization

**Decision**: Use dedicated DTO types (in `NeatSharp.Serialization.Dto`) for JSON serialization, mapped to/from domain types.

**Rationale**: Domain types (`Genome`, `Species`, `InnovationTracker`) were designed for runtime behavior, not serialization. `Genome` validates its invariants in the constructor (throwing on invalid state). `Species` has mutable members. Serializing directly would require `[JsonConstructor]` attributes, parameterless constructors, or settable properties — all of which compromise the domain model. DTOs keep serialization concerns separate (Principle VI: Single Responsibility).

### 3. Stream-Based I/O with System.Text.Json

**Decision**: All serialization uses `System.Text.Json` async serialization to `Stream`.

**Rationale**: Spec requires System.Text.Json (FR-019) and stream-based I/O (FR-018). Async methods (`SerializeAsync`/`DeserializeAsync`) work well with file streams, network streams, and memory streams without blocking. The consumer controls the stream lifecycle.

### 4. Configuration Hash via SHA-256

**Decision**: Compute configuration hash by serializing `NeatSharpOptions` to JSON, then computing SHA-256 of the UTF-8 bytes. Output as lowercase hex string.

**Rationale**: SHA-256 is deterministic, collision-resistant, and available in-box (`System.Security.Cryptography.SHA256`). JSON serialization ensures all fields (including nested objects) are included. The spec clarifies this is not for security, just quick equality checking (Assumption §7).

### 5. Evolver API Extension for Checkpoint/Resume

**Decision**: Add a new `RunAsync` overload accepting `EvolutionRunOptions` (which includes `ResumeFrom` and `OnCheckpoint` callback). Keep the existing simple `RunAsync(evaluator, ct)` unchanged.

**Rationale**: Backward compatible — existing consumers are unaffected. The `EvolutionRunOptions` type is extensible for future needs. The `OnCheckpoint` callback is invoked at generation boundaries, enforcing FR-005. The `ResumeFrom` checkpoint provides all state needed for deterministic continuation.

### 6. Schema Version Migration Strategy Pattern

**Decision**: Define `ISchemaVersionMigrator` with a `Migrate(JsonDocument, string fromVersion, string toVersion)` method. For v1, the migrator is a no-op (only 1.0.0 exists). Infrastructure is in place for future migrations.

**Rationale**: The spec requires migration infrastructure (FR-009) even though v1 has only one schema version. The strategy pattern allows adding version-specific migrators without modifying existing code (Principle VI: Open/Closed).

### 7. Exposing Counter State via Interface Properties

**Decision**: Add `int NextInnovationNumber { get; }` and `int NextNodeId { get; }` plus `RestoreState(int nextInnovationNumber, int nextNodeId)` to `IInnovationTracker`. Add `int NextSpeciesId { get; }` plus `RestoreState(int nextSpeciesId)` to `ISpeciationStrategy`. NeatEvolver calls `RestoreState` through the interface abstractions.

**Rationale**: Counter state must be captured for checkpoints and restored for resume. Read-only properties on interfaces provide clean access for serialization. The restore methods are on the interfaces because state restoration is inherent to the responsibility of managing counter state, and placing them on concrete classes would force NeatEvolver to downcast (violating Dependency Inversion and Liskov Substitution per Principle VI). Any custom implementation of these interfaces must support state restoration to participate in the resume workflow.
