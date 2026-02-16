# Tasks: Versioned Serialization + Checkpoint/Resume + Artifact Export

**Input**: Design documents from `/specs/005-versioned-serialization/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included — the feature specification requires TDD (Red-Green-Refactor per Constitution Principle VII).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create folder structure for the serialization feature within the existing project

- [X] T001 Create Serialization namespace folder structure at src/NeatSharp/Serialization/, src/NeatSharp/Serialization/Dto/, and src/NeatSharp/Serialization/Migration/
- [X] T002 Create test folder structure at tests/NeatSharp.Tests/Serialization/ and tests/NeatSharp.Tests/Serialization/Fixtures/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core domain models, exceptions, utilities, DTOs, and interface modifications that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Tests for Foundational Phase

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T003 [P] Write SchemaVersion tests (Parse semver string, IsCompatible for current/older/newer versions, NeedsMigration for older-but-supported versions, reject invalid version strings) in tests/NeatSharp.Tests/Serialization/SchemaVersionTests.cs
- [X] T004 [P] Write RngStateHelper tests (Capture then Restore produces identical next-N random values, works with seeded Random, round-trip preserves SeedArray/Inext/Inextp exactly) in tests/NeatSharp.Tests/Serialization/RngStateHelperTests.cs
- [X] T005 [P] Write ConfigurationHasher tests (deterministic hash for same NeatSharpOptions instance, same config produces same hash across calls, different configs produce different hashes, hash is 64-char lowercase hex SHA-256) in tests/NeatSharp.Tests/Serialization/ConfigurationHasherTests.cs

### Exception Types

- [X] T006 [P] Create CheckpointException base class inheriting NeatSharpException with message and optional inner exception constructor in src/NeatSharp/Exceptions/CheckpointException.cs
- [X] T007 [P] Create CheckpointCorruptionException inheriting CheckpointException with IReadOnlyList&lt;string&gt; ValidationErrors property listing all structural integrity failures in src/NeatSharp/Exceptions/CheckpointCorruptionException.cs
- [X] T008 [P] Create SchemaVersionException inheriting CheckpointException with ArtifactVersion, ExpectedVersion, and IsMigrationAvailable properties in src/NeatSharp/Exceptions/SchemaVersionException.cs

### Domain Models

- [X] T009 [P] Create SchemaVersion static class with Current="1.0.0", MinimumSupported="1.0.0" constants; IsCompatible(string), NeedsMigration(string), Parse(string) methods in src/NeatSharp/Serialization/SchemaVersion.cs
- [X] T010 [P] Create EnvironmentInfo record with OsDescription, RuntimeVersion, Architecture properties; static CreateCurrent() factory using RuntimeInformation in src/NeatSharp/Serialization/EnvironmentInfo.cs
- [X] T011 [P] Create ArtifactMetadata record with SchemaVersion, LibraryVersion, Seed, ConfigurationHash, CreatedAtUtc (ISO 8601 UTC string), Environment (EnvironmentInfo) in src/NeatSharp/Serialization/ArtifactMetadata.cs
- [X] T012 [P] Create RngState record with SeedArray (int[56]), Inext, Inextp fields and validation (array length 56, indices in [0,55]) in src/NeatSharp/Serialization/RngState.cs
- [X] T013 [P] Create SpeciesCheckpoint record with Id, RepresentativeIndex, BestFitnessEver, GenerationsSinceImprovement, MemberIndices (IReadOnlyList&lt;int&gt;), MemberFitnesses (IReadOnlyList&lt;double&gt;) in src/NeatSharp/Serialization/SpeciesCheckpoint.cs
- [X] T014 Create TrainingCheckpoint record with Population (IReadOnlyList&lt;Genome&gt;), Species (IReadOnlyList&lt;SpeciesCheckpoint&gt;), NextInnovationNumber, NextNodeId, NextSpeciesId, ChampionGenome, ChampionFitness, ChampionGeneration, Generation, Seed, RngState, Configuration (NeatSharpOptions), ConfigurationHash, History (RunHistory), Metadata (ArtifactMetadata) in src/NeatSharp/Serialization/TrainingCheckpoint.cs

### Utilities

- [X] T015 Implement RngStateHelper static class with Capture(Random) returning RngState and Restore(Random, RngState) using reflection to access Net5CompatSeedImpl._seedArray, _inext, _inextp via Random._impl field in src/NeatSharp/Serialization/RngStateHelper.cs
- [X] T016 Implement ConfigurationHasher static class with ComputeHash(NeatSharpOptions) that serializes options to JSON (camelCase, enum-as-string) then computes SHA-256 returning 64-char lowercase hex string in src/NeatSharp/Serialization/ConfigurationHasher.cs

### Serialization DTOs (Shared)

- [X] T017 [P] Create NodeGeneDto with Id, Type (string), ActivationFunction properties and static ToDto(NodeGene)/ToDomain() mapping methods in src/NeatSharp/Serialization/Dto/NodeGeneDto.cs
- [X] T018 [P] Create ConnectionGeneDto with InnovationNumber, SourceNodeId, TargetNodeId, Weight, IsEnabled properties and static ToDto(ConnectionGene)/ToDomain() mapping methods in src/NeatSharp/Serialization/Dto/ConnectionGeneDto.cs
- [X] T019 [P] Create GenomeDto with Nodes (List&lt;NodeGeneDto&gt;), Connections (List&lt;ConnectionGeneDto&gt;) properties and static ToDto(Genome)/ToDomain() mapping methods in src/NeatSharp/Serialization/Dto/GenomeDto.cs
- [X] T020 [P] Create ArtifactMetadataDto with SchemaVersion, LibraryVersion, Seed, ConfigurationHash, CreatedAtUtc, Environment properties and mapping methods in src/NeatSharp/Serialization/Dto/ArtifactMetadataDto.cs
- [X] T021 [P] Create RngStateDto with SeedArray (int[]), Inext, Inextp properties and mapping methods in src/NeatSharp/Serialization/Dto/RngStateDto.cs
- [X] T022 [P] Create SpeciesCheckpointDto with Id, RepresentativeIndex, BestFitnessEver, GenerationsSinceImprovement, MemberIndices, MemberFitnesses properties and mapping methods in src/NeatSharp/Serialization/Dto/SpeciesCheckpointDto.cs
- [X] T023 Create CheckpointDto with SchemaVersion, Metadata, Population (List&lt;GenomeDto&gt;), Species (List&lt;SpeciesCheckpointDto&gt;), Counters (nested object with NextInnovationNumber, NextNodeId, NextSpeciesId), Champion (index+fitness+generation), Generation, Seed, RngState, Configuration (NeatSharpOptions), ConfigurationHash, History (RunHistory) and ToDto(TrainingCheckpoint)/ToDomain() mapping methods in src/NeatSharp/Serialization/Dto/CheckpointDto.cs

### Interface Modifications

- [X] T024 [P] Add int NextInnovationNumber { get; }, int NextNodeId { get; } read-only properties and RestoreState(int nextInnovationNumber, int nextNodeId) method to IInnovationTracker interface in src/NeatSharp/Genetics/IInnovationTracker.cs
- [X] T025 [P] Add int NextSpeciesId { get; } read-only property and RestoreState(int nextSpeciesId) method to ISpeciationStrategy interface in src/NeatSharp/Evolution/Speciation/ISpeciationStrategy.cs
- [X] T026 Implement NextInnovationNumber and NextNodeId properties (expose existing _nextInnovationNumber and _nextNodeId fields) and implement RestoreState(int nextInnovationNumber, int nextNodeId) from IInnovationTracker in InnovationTracker in src/NeatSharp/Genetics/InnovationTracker.cs
- [X] T027 Implement NextSpeciesId property (expose existing _nextSpeciesId field) and implement RestoreState(int nextSpeciesId) from ISpeciationStrategy in CompatibilitySpeciation in src/NeatSharp/Evolution/Speciation/CompatibilitySpeciation.cs

### Schema Version Migration Infrastructure

- [X] T028 [P] Create ISchemaVersionMigrator interface with CanMigrate(string fromVersion) and Migrate(JsonDocument doc, string fromVersion) methods in src/NeatSharp/Serialization/Migration/ISchemaVersionMigrator.cs
- [X] T029 Implement SchemaVersionMigrator composite class with Dictionary&lt;string, ISchemaVersionMigrator&gt; registry (empty for v1.0.0 — no prior versions to migrate from) in src/NeatSharp/Serialization/Migration/SchemaVersionMigrator.cs

### Evolution Run Options

- [X] T030 Create EvolutionRunOptions class with ResumeFrom (TrainingCheckpoint?) and OnCheckpoint (Func&lt;TrainingCheckpoint, CancellationToken, Task&gt;?) properties in src/NeatSharp/Evolution/EvolutionRunOptions.cs

**Checkpoint**: Foundation ready — all domain models, DTOs, exceptions, utilities, and interface modifications are in place. User story implementation can now begin.

---

## Phase 3: User Story 1 — Save and Load a Checkpoint (Priority: P1) 🎯 MVP

**Goal**: Save the complete training state at a generation boundary and load it back with full fidelity

**Independent Test**: Run training for N generations, save a checkpoint, load it back, assert every field in the restored state matches the original (population genomes, species metadata, innovation counters, configuration, seed, RNG state)

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T031 [P] [US1] Write checkpoint round-trip tests (save to MemoryStream, load back, compare: population genome count and node/connection equality, species metadata, innovation counters, champion genome/fitness/generation, config, seed, RNG state, generation number, history; also test stream-based I/O with no filesystem dependency; also test round-trip with empty population — zero genomes, zero species — verify save succeeds, load succeeds, and restored state has empty population and empty species list) in tests/NeatSharp.Tests/Serialization/CheckpointRoundTripTests.cs
- [X] T032 [P] [US1] Write checkpoint validator tests (valid checkpoint passes; connection referencing non-existent node fails; species referencing genome not in population fails; NextInnovationNumber <= max innovation number fails; NextNodeId <= max node ID fails; NextSpeciesId <= max species ID fails; champion not in population fails; collect all errors in single result) in tests/NeatSharp.Tests/Serialization/CheckpointValidatorTests.cs

### Implementation for User Story 1

- [X] T033 [P] [US1] Create ICheckpointValidator interface with Validate(TrainingCheckpoint) method and CheckpointValidationResult record with Errors list and IsValid property in src/NeatSharp/Serialization/ICheckpointValidator.cs
- [X] T034 [P] [US1] Create ICheckpointSerializer interface with SaveAsync(Stream, TrainingCheckpoint, CancellationToken) and LoadAsync(Stream, CancellationToken) methods in src/NeatSharp/Serialization/ICheckpointSerializer.cs
- [X] T035 [US1] Implement CheckpointValidator: verify (a) all connections reference existing nodes per genome, (b) species member/representative indices are valid population indices, (c) NextInnovationNumber > max innovation number across all genomes, (d) NextNodeId > max node ID across all genomes, (e) NextSpeciesId > max species ID across all species, (f) champion genome exists in population; collect all errors into CheckpointValidationResult in src/NeatSharp/Serialization/CheckpointValidator.cs
- [X] T036 [US1] Implement CheckpointSerializer: SaveAsync maps TrainingCheckpoint to CheckpointDto and writes to stream via JsonSerializer.SerializeAsync with camelCase/indented/enum-string options; LoadAsync reads stream, checks schema version (throw SchemaVersionException on mismatch), routes through SchemaVersionMigrator if needed, maps CheckpointDto to TrainingCheckpoint, validates via CheckpointValidator (throw CheckpointCorruptionException on failure) in src/NeatSharp/Serialization/CheckpointSerializer.cs
- [X] T037 [US1] Register ICheckpointSerializer → CheckpointSerializer (Singleton), ICheckpointValidator → CheckpointValidator (Singleton), and ISchemaVersionMigrator → SchemaVersionMigrator (Singleton) in ServiceCollectionExtensions.AddNeatSharp() in src/NeatSharp/Extensions/ServiceCollectionExtensions.cs

**Checkpoint**: Checkpoint save/load is fully functional. Can save a checkpoint to any stream, load it back, and verify full round-trip fidelity.

---

## Phase 4: User Story 2 — Resume Training from Checkpoint (Priority: P2)

**Goal**: Resume training from a loaded checkpoint with results identical to an uninterrupted run on the same CPU (deterministic continuation)

**Independent Test**: Run training for N+M generations uninterrupted, then separately run N generations + save checkpoint + load + resume for M more generations — assert both runs produce identical champion, fitness history, and final population

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T038 [US2] Write resume determinism tests (run N+M generations uninterrupted with seed=42; separately run N generations, capture checkpoint via OnCheckpoint callback, load checkpoint, resume for M generations; assert champion genome, champion fitness, generation statistics, and final population match exactly; also test cancellation during resumed run stops at generation boundary; also test config hash mismatch on resume throws CheckpointException) in tests/NeatSharp.Tests/Serialization/ResumeDeterminismTests.cs

### Implementation for User Story 2

- [X] T039 [US2] Add RunAsync(IEvaluationStrategy evaluator, EvolutionRunOptions options, CancellationToken cancellationToken) overload to INeatEvolver interface in src/NeatSharp/Evolution/INeatEvolver.cs
- [X] T040 [US2] Implement resume logic in NeatEvolver.RunAsync overload: if EvolutionRunOptions.ResumeFrom is not null, validate config hash match (FR-006a), restore population and fitness arrays from checkpoint, reconstruct List<Species> from checkpoint's SpeciesCheckpoint records (rebuild each Species with its Id, representative genome from population by index, member genomes with fitnesses by index, BestFitnessEver, and GenerationsSinceImprovement), restore species counter via ISpeciationStrategy.RestoreState(nextSpeciesId), restore innovation counters via IInnovationTracker.RestoreState(nextInnovationNumber, nextNodeId), restore RNG state via RngStateHelper.Restore, set generation counter and champion from checkpoint, restore run history; invoke OnCheckpoint callback at each generation boundary after speciation (FR-005) passing a new TrainingCheckpoint snapshot; delegate to existing loop logic for evolution steps in src/NeatSharp/Evolution/NeatEvolver.cs

**Checkpoint**: Training can be saved, stopped, loaded, and resumed with deterministic results. The core persistence workflow is complete.

---

## Phase 5: User Story 3 — Export Champion for Interoperability (Priority: P3)

**Goal**: Export the champion genome as a self-describing JSON graph (nodes + edges) parseable by any standard JSON reader without requiring NEATSharp

**Independent Test**: Export champion, parse resulting JSON with System.Text.Json (no NEATSharp DTO types), verify graph structure — node IDs/types/activation functions and edge source/target/weight/enabled — matches the original genome

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T041 [US3] Write champion exporter tests (exported JSON contains nodes array with id/type/activationFunction, edges array with source/target/weight/enabled, metadata with fitness/generationFound/seed/schemaVersion/libraryVersion; parse with raw JsonDocument to verify no NEATSharp types needed; test export from both EvolutionResult and TrainingCheckpoint; verify graph structure matches original genome) in tests/NeatSharp.Tests/Serialization/ChampionExporterTests.cs

### Implementation for User Story 3

- [X] T042 [P] [US3] Create ChampionExportDto with SchemaVersion, Metadata (ArtifactMetadataDto — full FR-013 fields), Champion (fitness, generationFound), Nodes (list with id/type/activationFunction), Edges (list with source/target/weight/enabled) and mapping from Genome/Champion in src/NeatSharp/Serialization/Dto/ChampionExportDto.cs
- [X] T043 [P] [US3] Create IChampionExporter interface with ExportAsync(Stream, EvolutionResult, CancellationToken) and ExportAsync(Stream, TrainingCheckpoint, CancellationToken) overloads in src/NeatSharp/Serialization/IChampionExporter.cs
- [X] T044 [US3] Implement ChampionExporter: map champion Genome nodes to export nodes (id, type as string, activation function), map connections to export edges (source, target, weight, enabled), build metadata (fitness, generation, seed, schema version, library version, config hash), serialize ChampionExportDto to stream via STJ in src/NeatSharp/Serialization/ChampionExporter.cs
- [X] T045 [US3] Register IChampionExporter → ChampionExporter (Singleton) in ServiceCollectionExtensions.AddNeatSharp() in src/NeatSharp/Extensions/ServiceCollectionExtensions.cs

**Checkpoint**: Champion genomes can be exported as standalone interoperable JSON documents.

---

## Phase 6: User Story 4 — Version Artifacts and Detect Incompatibilities (Priority: P4)

**Goal**: Detect schema version mismatches on load and provide clear, actionable error messages identifying the artifact's version, expected version, and migration availability

**Independent Test**: Create checkpoint, modify schema version stamp to simulate mismatch, attempt load, verify SchemaVersionException contains expected vs. actual version and migration info

**Note**: Core versioning infrastructure (SchemaVersion, SchemaVersionException, migration routing) was built in Phase 2 and Phase 3. This phase adds focused acceptance test coverage for version mismatch scenarios.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T046 [US4] Write version mismatch detection tests: artifact with matching version loads successfully; artifact with newer version (e.g., "2.0.0") throws SchemaVersionException with correct ArtifactVersion/ExpectedVersion/IsMigrationAvailable=false; artifact with unsupported older version (e.g., "0.5.0") throws SchemaVersionException; newly created artifact JSON contains "schemaVersion":"1.0.0" and libraryVersion string — add to tests/NeatSharp.Tests/Serialization/CheckpointRoundTripTests.cs

### Implementation for User Story 4

- [X] T047 [US4] Verify and refine SchemaVersionException error messages in CheckpointSerializer.LoadAsync to include artifact version, expected version, and whether migration is available per FR-008; ensure message is human-readable and actionable in src/NeatSharp/Serialization/CheckpointSerializer.cs

**Checkpoint**: Schema version mismatches produce clear, actionable error messages. All artifacts contain semantic version stamps.

---

## Phase 7: User Story 5 — Create Diagnostics Bundle for Bug Reports (Priority: P5)

**Goal**: Single-call operation that bundles checkpoint, configuration, environment metadata (OS, runtime, architecture), and run history into one JSON document

**Independent Test**: Create diagnostics bundle, parse JSON, verify it contains a checkpoint section, configuration, environment metadata with OS/runtime/architecture, and run history

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T048 [US5] Write diagnostics bundle tests (bundle JSON contains schemaVersion, metadata, checkpoint, configuration, environment with osDescription/runtimeVersion/architecture, history sections; embedded checkpoint can be extracted and loaded; single CreateAsync call produces complete bundle) in tests/NeatSharp.Tests/Serialization/DiagnosticsBundleTests.cs

### Implementation for User Story 5

- [X] T049 [P] [US5] Create DiagnosticsBundleDto with SchemaVersion, Metadata (ArtifactMetadataDto), Checkpoint (CheckpointDto), Configuration (NeatSharpOptions), Environment (EnvironmentInfo), History (RunHistory) and mapping from TrainingCheckpoint in src/NeatSharp/Serialization/Dto/DiagnosticsBundleDto.cs
- [X] T050 [P] [US5] Create IDiagnosticsBundleCreator interface with CreateAsync(Stream, TrainingCheckpoint, CancellationToken) method in src/NeatSharp/Serialization/IDiagnosticsBundleCreator.cs
- [X] T051 [US5] Implement DiagnosticsBundleCreator: assemble DiagnosticsBundleDto from TrainingCheckpoint (embed full checkpoint DTO, configuration, EnvironmentInfo.CreateCurrent(), run history, artifact metadata), serialize to stream via STJ in src/NeatSharp/Serialization/DiagnosticsBundleCreator.cs
- [X] T052 [US5] Register IDiagnosticsBundleCreator → DiagnosticsBundleCreator (Singleton) in ServiceCollectionExtensions.AddNeatSharp() in src/NeatSharp/Extensions/ServiceCollectionExtensions.cs

**Checkpoint**: Single-call diagnostics bundles can be created for reproducible bug reports.

---

## Phase 8: User Story 6 — Backward Compatibility with Prior Schema Versions (Priority: P6)

**Goal**: Infrastructure validates current v1.0.0 schema loads correctly; placeholder test structure is ready for future v1→v2 migration

**Independent Test**: Load a v1.0.0 fixture file, verify all fields populated correctly. SchemaVersionMigrator reports no migration needed for current version.

### Tests for User Story 6

- [X] T053 [US6] Write backward compatibility tests (load v1.0.0 fixture JSON file and verify all fields are populated correctly; SchemaVersionMigrator.CanMigrate returns false for current version; placeholder test method for future v1.0.0→v2.0.0 migration marked with Skip/Fact attribute) in tests/NeatSharp.Tests/Serialization/BackwardCompatibilityTests.cs

### Implementation for User Story 6

- [X] T054 [US6] Generate a valid v1.0.0 checkpoint JSON fixture file (small population, 2-3 species, known seed, all fields populated) for backward-compatibility regression testing in tests/NeatSharp.Tests/Serialization/Fixtures/v1_0_0_checkpoint.json

**Checkpoint**: Backward-compatibility infrastructure is validated. Ready for future schema evolution.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Observability, DI validation, and quickstart validation

### Tests for Observability

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T055 Write observability logging tests — inject mock ILogger into CheckpointSerializer and CheckpointValidator; verify Warning emitted when migration defaults are applied during artifact loading; verify Warning emitted on configuration hash mismatch detection; verify Error emitted on artifact corruption detection; verify Error emitted on incompatible schema version; verify no Info-level logs emitted during normal save/load operations (FR-021, FR-022) in tests/NeatSharp.Tests/Serialization/ObservabilityLoggingTests.cs

### Implementation for Observability

- [X] T056 Add Warning-level ILogger messages for migration defaults applied during artifact loading and for configuration hash mismatch detection per FR-021 in src/NeatSharp/Serialization/CheckpointSerializer.cs
- [X] T057 Add Error-level ILogger messages for artifact corruption detection and incompatible schema version errors per FR-022 in src/NeatSharp/Serialization/CheckpointSerializer.cs and src/NeatSharp/Serialization/CheckpointValidator.cs
- [X] T058 Update ServiceCollectionExtensionsTests to verify all new serialization services (ICheckpointSerializer, ICheckpointValidator, IChampionExporter, IDiagnosticsBundleCreator, ISchemaVersionMigrator) are registered and resolvable in tests/NeatSharp.Tests/Extensions/ServiceCollectionExtensionsTests.cs
- [X] T059 Run quickstart.md validation — verify all code examples from specs/005-versioned-serialization/quickstart.md compile and execute correctly against the implemented API

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational (Phase 2) — no dependencies on other stories
- **US2 (Phase 4)**: Depends on US1 (Phase 3) — needs checkpoint save/load for resume testing
- **US3 (Phase 5)**: Depends on Foundational (Phase 2) — independent of US1/US2
- **US4 (Phase 6)**: Depends on US1 (Phase 3) — tests version mismatch in checkpoint load path
- **US5 (Phase 7)**: Depends on Foundational (Phase 2) — independent of US1/US2
- **US6 (Phase 8)**: Depends on US1 (Phase 3) — needs checkpoint serializer for fixture loading
- **Polish (Phase 9)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no dependencies on other stories
- **US2 (P2)**: Depends on US1 — needs checkpoint save/load for resume testing
- **US3 (P3)**: Can start after Foundational — fully independent (different artifact type)
- **US4 (P4)**: Depends on US1 — tests version detection in checkpoint load path
- **US5 (P5)**: Can start after Foundational — fully independent (different artifact type)
- **US6 (P6)**: Depends on US1 — needs checkpoint serializer for fixture loading

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Interfaces/contracts before implementations
- Domain models before services
- Services before DI registration
- Story complete before moving to next priority

### Parallel Opportunities

- All foundational tests (T003–T005) can run in parallel
- All foundational exception types (T006–T008) can run in parallel
- All foundational domain models (T009–T013) can run in parallel
- All shared DTOs (T017–T022) can run in parallel
- Interface modifications (T024–T025) can run in parallel
- Migration interface + run options (T028, T030) can run in parallel
- US1 tests (T031–T032) can run in parallel
- US1 interfaces (T033–T034) can run in parallel
- US3 interface + DTO (T042–T043) can run in parallel
- US5 interface + DTO (T049–T050) can run in parallel
- **US3 and US5 can be worked on in parallel with each other and with US2** (after Foundational)

---

## Parallel Example: Foundational Phase

```bash
# Launch all foundational tests in parallel:
Task: T003 "Write SchemaVersion tests in tests/NeatSharp.Tests/Serialization/SchemaVersionTests.cs"
Task: T004 "Write RngStateHelper tests in tests/NeatSharp.Tests/Serialization/RngStateHelperTests.cs"
Task: T005 "Write ConfigurationHasher tests in tests/NeatSharp.Tests/Serialization/ConfigurationHasherTests.cs"

# Launch all exception types in parallel:
Task: T006 "Create CheckpointException in src/NeatSharp/Exceptions/CheckpointException.cs"
Task: T007 "Create CheckpointCorruptionException in src/NeatSharp/Exceptions/CheckpointCorruptionException.cs"
Task: T008 "Create SchemaVersionException in src/NeatSharp/Exceptions/SchemaVersionException.cs"

# Launch all domain models in parallel:
Task: T009 "Create SchemaVersion in src/NeatSharp/Serialization/SchemaVersion.cs"
Task: T010 "Create EnvironmentInfo in src/NeatSharp/Serialization/EnvironmentInfo.cs"
Task: T011 "Create ArtifactMetadata in src/NeatSharp/Serialization/ArtifactMetadata.cs"
Task: T012 "Create RngState in src/NeatSharp/Serialization/RngState.cs"
Task: T013 "Create SpeciesCheckpoint in src/NeatSharp/Serialization/SpeciesCheckpoint.cs"

# Launch all shared DTOs in parallel:
Task: T017 "Create NodeGeneDto in src/NeatSharp/Serialization/Dto/NodeGeneDto.cs"
Task: T018 "Create ConnectionGeneDto in src/NeatSharp/Serialization/Dto/ConnectionGeneDto.cs"
Task: T019 "Create GenomeDto in src/NeatSharp/Serialization/Dto/GenomeDto.cs"
Task: T020 "Create ArtifactMetadataDto in src/NeatSharp/Serialization/Dto/ArtifactMetadataDto.cs"
Task: T021 "Create RngStateDto in src/NeatSharp/Serialization/Dto/RngStateDto.cs"
Task: T022 "Create SpeciesCheckpointDto in src/NeatSharp/Serialization/Dto/SpeciesCheckpointDto.cs"
```

## Parallel Example: User Story 1

```bash
# Launch US1 tests in parallel (TDD: write first, verify they fail):
Task: T031 "Write checkpoint round-trip tests in tests/NeatSharp.Tests/Serialization/CheckpointRoundTripTests.cs"
Task: T032 "Write checkpoint validator tests in tests/NeatSharp.Tests/Serialization/CheckpointValidatorTests.cs"

# Launch US1 interfaces in parallel:
Task: T033 "Create ICheckpointValidator interface in src/NeatSharp/Serialization/ICheckpointValidator.cs"
Task: T034 "Create ICheckpointSerializer interface in src/NeatSharp/Serialization/ICheckpointSerializer.cs"
```

## Parallel Example: Independent Stories After Foundational

```bash
# US3 and US5 can run in parallel with US2 (after Phase 2 complete):
# Developer A: US2 (resume — depends on US1)
# Developer B: US3 (champion export — independent)
# Developer C: US5 (diagnostics bundle — independent)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1 — Checkpoint Save/Load
4. **STOP and VALIDATE**: Test round-trip fidelity independently
5. Deliver checkpoint save/load capability

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add User Story 1 → Test save/load round-trip → Deliver (MVP!)
3. Add User Story 2 → Test deterministic resume → Deliver
4. Add User Story 3 → Test champion export → Deliver (can parallel with US2)
5. Add User Story 4 → Test version mismatch detection → Deliver
6. Add User Story 5 → Test diagnostics bundle → Deliver (can parallel with US2)
7. Add User Story 6 → Test backward compat fixture → Deliver
8. Polish → Logging, DI validation, quickstart validation → Final delivery

### Parallel Team Strategy

With multiple developers after Foundational is complete:
- **Developer A**: US1 → US2 → US4 → US6 (checkpoint → resume → versioning → compat — sequential chain)
- **Developer B**: US3 (champion export — fully independent after Foundational)
- **Developer C**: US5 (diagnostics bundle — fully independent after Foundational)
- **All**: Polish phase after all stories complete

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- TDD: Write tests first, verify they fail, then implement
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- All JSON serialization uses System.Text.Json with camelCase naming, indented output, and enum-as-string
- Stream-based I/O throughout — no filesystem dependencies in library code
- No new NuGet dependencies — System.Text.Json is in-box for .NET 8+
