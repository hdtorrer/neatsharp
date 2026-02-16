# Feature Specification: Versioned Serialization + Checkpoint/Resume + Artifact Export

**Feature Branch**: `005-versioned-serialization`
**Created**: 2026-02-15
**Status**: Draft
**Input**: User description: "Provide warm-start capability from previous training runs, schema stability guidance, and interoperability exports. This is the natural next feature after the training runner (Spec 04), which explicitly deferred persistence/checkpointing to Spec 05."

## Clarifications

### Session 2026-02-15

- Q: When resuming from a checkpoint, which configuration changes should be allowed vs. blocked? → A: Require identical config — enforce config hash match; any mismatch is an error.
- Q: What format should the diagnostics bundle use — single JSON document or structured archive (ZIP)? → A: Single JSON document with checkpoint, config, environment, and history as nested sections.
- Q: When loading older-schema artifacts with missing fields, how should defaults be determined? → A: Define an explicit per-field default mapping in the migration/version adapter code.
- Q: Should serialization operations emit structured log messages via ILogger? → A: Log only at Warning (migration defaults applied, config hash mismatch) and Error (corruption, version incompatible); no Info-level logging for normal save/load.
- Q: Should the system validate checkpoint integrity beyond JSON syntax on load? → A: Full structural validation on load — verify internal references (node/connection consistency), species assignments, and counter integrity.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Save and Load a Checkpoint (Priority: P1)

A library consumer wants to save the complete state of a training run at any generation so that it can be loaded back later and training can resume with identical results on the same CPU. They call a save operation with the current training state and receive a checkpoint artifact. Later, they load that checkpoint and verify that the full training state — population, species, innovation counters, champion, configuration, seed, and RNG state — has been faithfully restored.

**Why this priority**: This is the foundational persistence capability. Every other feature in this spec (resume, export, diagnostics) depends on the ability to serialize and deserialize training state correctly. Without checkpoint save/load, none of the higher-level features can exist.

**Independent Test**: Can be fully tested by running training for N generations, saving a checkpoint, loading it back, and asserting that every field in the restored state matches the original (population genomes, species metadata, innovation counters, configuration, seed, RNG state).

**Acceptance Scenarios**:

1. **Given** a training run that has completed N generations, **When** the consumer saves a checkpoint, **Then** the checkpoint artifact contains the complete training state including all genomes, species assignments, innovation counters, champion, configuration, seed, and RNG state.
2. **Given** a saved checkpoint artifact, **When** the consumer loads it, **Then** the restored training state is identical to the state at the time of saving — every genome, species, counter, and configuration value matches exactly.
3. **Given** a checkpoint artifact, **When** the consumer inspects it, **Then** it contains a schema version stamp and artifact metadata (library version, creation timestamp, seed, configuration hash).
4. **Given** a checkpoint saved to a memory stream, **When** the consumer loads it from the same stream, **Then** the round-trip succeeds without any filesystem dependency.

---

### User Story 2 - Resume Training from Checkpoint (Priority: P2)

A library consumer wants to restart training mid-run from a previously saved checkpoint file. They load a checkpoint that was saved at generation N, resume training, and the subsequent generations produce results identical to what an uninterrupted run would have produced on the same CPU. This enables warm-start workflows and fault recovery.

**Why this priority**: Resume is the primary user-facing value of checkpointing. Saving without the ability to resume delivers incomplete value. This story validates that checkpoint state is sufficient for deterministic continuation.

**Independent Test**: Can be tested by running training for N+M generations uninterrupted, then separately running N generations, saving a checkpoint, loading it, resuming for M more generations, and asserting that both runs produce identical results (same champion, same fitness history, same final population).

**Acceptance Scenarios**:

1. **Given** a checkpoint saved at generation N, **When** the consumer resumes training for M additional generations, **Then** the results (champion, fitness history, final population) are identical to an uninterrupted run of N+M generations with the same seed and configuration.
2. **Given** a resumed training run, **When** the consumer cancels via a cancellation token, **Then** the system stops at the next generation boundary and returns a valid partial result, consistent with cancellation behavior in a fresh run.
3. **Given** a checkpoint, **When** the consumer resumes with additional stopping criteria (e.g., a higher generation limit), **Then** the new criteria are respected while preserving the checkpoint's accumulated state.

---

### User Story 3 - Export Champion for Interoperability (Priority: P3)

A library consumer wants to export the champion genome as a simple, self-describing graph format (JSON adjacency list) that can be parsed by any JSON reader without requiring the NEATSharp library. This enables visualization tools, external analysis, and integration with other systems.

**Why this priority**: Champion export is a standalone, high-value feature that does not depend on checkpoint infrastructure. It enables interoperability with the broader ecosystem — visualization, analysis, and non-.NET consumers — which is critical for practical adoption.

**Independent Test**: Can be tested by exporting a champion genome, parsing the resulting JSON with a standalone JSON reader (no NEATSharp dependency), and verifying that the graph structure (nodes, edges, weights, activation functions) matches the original genome.

**Acceptance Scenarios**:

1. **Given** a completed training run with a champion, **When** the consumer exports the champion, **Then** the output is a valid JSON document containing nodes (with IDs, types, and activation functions) and edges (with source, target, weight, and enabled status).
2. **Given** an exported champion JSON file, **When** a consumer parses it with any standard JSON reader, **Then** they can reconstruct the full network graph without needing the NEATSharp library.
3. **Given** an exported champion, **When** the consumer inspects the metadata section, **Then** it includes the fitness score, the generation the champion was found, the schema version, and the seed used for the training run.

---

### User Story 4 - Version Artifacts and Detect Incompatibilities (Priority: P4)

A library consumer upgrades to a newer version of NEATSharp and attempts to load a checkpoint saved with an older version. The system detects the schema version mismatch and provides a clear, actionable error message explaining the incompatibility and what migration steps are available (if any).

**Why this priority**: Schema versioning is essential for long-term maintainability. Without version stamps and mismatch detection, users will encounter cryptic deserialization errors when loading artifacts across versions. This story ensures a clean upgrade path.

**Independent Test**: Can be tested by creating a checkpoint with a known schema version, modifying the version stamp to simulate a mismatch, attempting to load it, and verifying that the error message identifies the expected vs. actual version and provides migration guidance.

**Acceptance Scenarios**:

1. **Given** an artifact with a schema version that matches the current library version, **When** the consumer loads it, **Then** the load succeeds normally.
2. **Given** an artifact with a schema version that does not match the current library version, **When** the consumer attempts to load it, **Then** the system throws a clear error identifying the artifact's version, the expected version, and whether migration is possible.
3. **Given** a newly created artifact, **When** the consumer inspects it, **Then** it contains a schema version stamp following Semantic Versioning (Major.Minor.Patch) and a library version identifier.

---

### User Story 5 - Create Diagnostics Bundle for Bug Reports (Priority: P5)

A library consumer encounters unexpected training behavior and wants to file a reproducible bug report. They call a single operation that bundles the current checkpoint, configuration, environment metadata (OS, runtime version, CPU architecture), and run history into a single archive. This bundle contains everything needed to reproduce the issue.

**Why this priority**: Diagnostics bundles significantly reduce the time to reproduce and fix bugs. This is a convenience feature that builds on checkpoint and metadata infrastructure already required by earlier stories.

**Independent Test**: Can be tested by creating a diagnostics bundle during a training run, extracting its contents, and verifying it contains a checkpoint, the full configuration, environment metadata, and the run history.

**Acceptance Scenarios**:

1. **Given** a training run in progress or completed, **When** the consumer creates a diagnostics bundle, **Then** the bundle contains a checkpoint, the full configuration, environment metadata (OS version, runtime version, CPU architecture), and the run history.
2. **Given** a diagnostics bundle, **When** a maintainer loads the embedded checkpoint and configuration, **Then** they can resume the training run and reproduce the reported behavior.

---

### User Story 6 - Backward Compatibility with Prior Schema Versions (Priority: P6)

A library consumer has artifacts saved with a prior schema version (once multiple versions exist). The system loads these artifacts successfully, applying any necessary transformations to bring them up to the current schema. This ensures that users are not forced to discard saved work when upgrading the library.

**Why this priority**: This is a forward-looking requirement. In the initial release there will only be one schema version (v1.0.0), so backward compatibility is not yet exercisable. However, the infrastructure must be designed to support it, and a test fixture must validate the loading path once a second version is introduced.

**Independent Test**: Can be tested (once a second schema version exists) by loading a fixture file saved in the prior schema format and verifying that all fields are correctly populated in the current data model. Until then, a placeholder test validates that the current schema version loads correctly.

**Acceptance Scenarios**:

1. **Given** an artifact in the current schema format, **When** the consumer loads it, **Then** the load succeeds and all fields are populated correctly.
2. **Given** an artifact in a prior schema format (once versions exist), **When** the consumer loads it, **Then** the system applies the necessary transformations and the loaded state matches the expected current-schema representation.
3. **Given** an artifact in a schema version that is too old to migrate, **When** the consumer attempts to load it, **Then** the system provides a clear error identifying the unsupported version and suggesting a manual migration path.

---

### Edge Cases

- What happens when a checkpoint file is corrupt (truncated, invalid JSON, or structurally invalid)? The system must perform full structural validation on load — verifying JSON syntax, internal reference integrity (e.g., connections reference existing nodes, species reference existing genomes), and counter consistency — and throw a clear error identifying the corruption without crashing or returning partial state.
- What happens when a checkpoint write is interrupted mid-stream (e.g., process killed)? With stream-based I/O, atomic write behavior is the consumer's responsibility — the library writes to the consumer-provided stream and has no knowledge of the underlying storage backend. Consumers using file streams SHOULD implement a temporary-then-rename pattern at the call site (see `quickstart.md` "Atomic File Writes" section for a recommended pattern). The library itself MUST NOT assume filesystem access.
- What happens when the schema version in an artifact does not match the current library version? The system must produce an actionable error message (see US4).
- What happens when a checkpoint is saved with an empty population (all genomes extinct)? The system must serialize the empty state faithfully and allow it to be loaded, even though resuming from it would immediately terminate.
- What happens when a consumer attempts to save a checkpoint mid-evaluation (between individual genome evaluations within a generation)? The checkpoint must only be saveable at generation boundaries — the system must not expose partial evaluation state.
- What happens when optional fields are missing from an older schema version's artifact? The migration code must define an explicit per-field default mapping for each missing field (not derived from current `NeatSharpOptions` defaults). The system must log which defaults were applied.
- What happens when a consumer loads a checkpoint and attempts to resume with a different configuration? The system must compare the configuration hash from the checkpoint against the current configuration hash and throw a clear error on any mismatch, regardless of which parameters differ. No partial configuration changes are permitted on resume.

## Requirements *(mandatory)*

### Functional Requirements

#### Checkpoint Save/Load

- **FR-001**: System MUST save a complete checkpoint containing: all genomes in the population, species assignments and metadata (ID, representative, BestFitnessEver, GenerationsSinceImprovement), innovation counter state (next innovation number, next node ID), the champion (genome, fitness, generation found), the full configuration snapshot, the random seed, the RNG state, the current generation number, and the run history collected so far.
- **FR-002**: System MUST load a checkpoint and restore the complete training state such that all fields match the state at the time of saving — population genomes, species metadata, innovation counters, champion, configuration, seed, RNG state, generation number, and run history.
- **FR-003**: System MUST support checkpoint save and load via stream-based I/O (reading from and writing to any `Stream`), with no hard dependency on the filesystem. *(See FR-018 for the general stream-based I/O requirement that applies to all artifact types.)*

#### Resume

- **FR-004**: System MUST support resuming a training run from a loaded checkpoint such that subsequent generations produce results identical to an uninterrupted run on the same CPU (deterministic continuation). This requires restoring the RNG state, innovation counters, species state, and generation counter exactly.
- **FR-005**: System MUST only allow checkpoint saves at generation boundaries (after a generation is fully completed, before the next begins). Saving mid-evaluation is not permitted.
- **FR-006**: System MUST support cancellation via a cancellation token during a resumed run, with the same generation-boundary semantics as a fresh run (FR-004 from Spec 04).
- **FR-006a**: System MUST enforce identical configuration on resume by comparing the checkpoint's configuration hash against the current configuration hash. Any mismatch MUST result in a clear error before training resumes. No partial configuration changes are permitted.

#### Schema Versioning

- **FR-007**: System MUST stamp every serialized artifact (checkpoint, champion export, diagnostics bundle) with a schema version following Semantic Versioning (Major.Minor.Patch). The initial schema version is 1.0.0.
- **FR-008**: System MUST detect schema version mismatches when loading an artifact and produce an actionable error message identifying the artifact's version, the expected version, and whether automatic migration is available.
- **FR-009**: System MUST support loading artifacts from at least one prior schema version once multiple versions exist. The initial release only supports v1.0.0; backward-compat infrastructure must be present but exercised by a placeholder test until a second version is introduced.
- **FR-009a**: System MUST define an explicit per-field default mapping in migration code for any fields added in a newer schema version. Defaults MUST NOT be derived from the current `NeatSharpOptions` at runtime. The system MUST log (via `ILogger`) which fields had defaults applied during migration.

#### Observability

- **FR-021**: System MUST log at Warning level (via `ILogger`) when migration defaults are applied during artifact loading or when a configuration hash mismatch is detected.
- **FR-022**: System MUST log at Error level (via `ILogger`) when artifact corruption is detected or a schema version is incompatible. Normal save/load operations MUST NOT emit Info-level log messages.

#### Load Validation

- **FR-023**: System MUST perform full structural validation when loading a checkpoint, verifying: (a) all connection genes reference nodes that exist in the genome's node list, (b) species assignments reference genomes present in the population, (c) innovation counters are consistent with the highest innovation/node IDs in the population, (d) the champion genome exists in the population, and (e) NextSpeciesId is greater than the maximum species ID in the checkpoint's species list. Validation failures MUST produce a clear error identifying which checks failed.

#### Champion Export

- **FR-010**: System MUST export the champion genome as a JSON document containing: a nodes array (each with ID, type, and activation function) and an edges array (each with source node ID, target node ID, weight, and enabled status).
- **FR-011**: System MUST produce champion export files that are self-describing and parseable by any standard JSON reader without requiring the NEATSharp library.
- **FR-012**: System MUST include metadata in the champion export: the full artifact metadata required by FR-013 (library version, schema version, seed, configuration hash, creation timestamp UTC, environment information) plus champion-specific fields: fitness score and generation found.

#### Artifact Metadata

- **FR-013**: System MUST include the following metadata in every serialized artifact: library version, schema version, random seed, configuration hash, creation timestamp (UTC), and target environment information (OS, runtime version).
- **FR-014**: System MUST compute a deterministic configuration hash from the full `NeatSharpOptions` state, so that consumers can verify whether a checkpoint was created with the same configuration they intend to use for resuming.
- **FR-015**: System MUST include the creation timestamp as a UTC ISO 8601 string to avoid timezone ambiguity.

#### Diagnostics Bundle

- **FR-016**: System MUST produce a diagnostics bundle containing: a checkpoint of the current training state, the full configuration, environment metadata (OS version, runtime version, CPU architecture), and the run history.
- **FR-017**: System MUST produce diagnostics bundles via a single operation (one method call), minimizing the effort required from the consumer to create a reproducible bug report.

#### Storage Abstraction

- **FR-018**: System MUST perform all serialization I/O through a stream-based abstraction, decoupling the serialization logic from any specific storage backend (filesystem, memory, network, etc.).
- **FR-019**: System MUST use System.Text.Json for all JSON serialization, consistent with the existing technology stack and avoiding additional dependencies.
- **FR-020**: System MUST support serialization and deserialization of all existing domain types relevant to checkpointing: `Genome` (including `NodeGene`, `ConnectionGene`), `Species` metadata, `InnovationTracker` counter state, `NeatSharpOptions`, `RunHistory`, `GenerationStatistics`, and `Champion`. *(Note: `PopulationSnapshot` is a read-only reporting type; population state is captured via the checkpoint's genome list and species checkpoint records, which provide richer mutable state than `PopulationSnapshot`.)*

### Key Entities

- **Checkpoint**: A complete, serialized snapshot of training state at a generation boundary. Contains everything needed to resume training with deterministic results: population, species, counters, champion, configuration, seed, RNG state, generation number, and run history. Stamped with schema version and artifact metadata.
- **Artifact Metadata**: Common header present in all serialized artifacts (checkpoints, exports, bundles). Contains library version, schema version, seed, configuration hash, creation timestamp (UTC), and environment information.
- **Champion Export**: A standalone JSON document representing the champion genome as a graph (nodes and edges) with metadata. Designed for interoperability — parseable without the NEATSharp library.
- **Diagnostics Bundle**: A single JSON document containing a checkpoint, configuration, environment metadata, and run history as nested top-level sections. Designed for reproducible bug reports — one call produces everything a maintainer needs to reproduce an issue.
- **Schema Version**: A Semantic Versioning (Major.Minor.Patch) identifier stamped on every artifact. Used for compatibility detection and migration routing. The initial version is 1.0.0.

## Assumptions

- The existing `EvolutionResult` record captures champion, population snapshot, run history, and seed — but does not capture the full mutable state needed for resume (RNG state, innovation counters, species assignments). The checkpoint must capture additional state beyond what `EvolutionResult` provides.
- `System.Random` in .NET 8+ uses the Xoshiro256** algorithm, whose internal state can be captured and restored for deterministic continuation. The implementation will need to serialize and restore this state.
- The `InnovationTracker` exposes its counter values (`_nextInnovationNumber`, `_nextNodeId`) which must be serialized. The per-generation caches (`_connectionCache`, `_nodeSplitCache`) do not need serialization because checkpoints are saved at generation boundaries after `NextGeneration()` clears them.
- The `CompatibilitySpeciation._nextSpeciesId` counter must be serialized to ensure new species created after resume receive correct IDs.
- `NeatSharpOptions` and all its nested option classes are plain data objects suitable for System.Text.Json serialization.
- `Genome` is immutable after construction — serializing and deserializing it produces a functionally equivalent instance (same nodes, connections, input/output counts).
- Checkpoints are saved at generation boundaries by design. The training loop already has a natural generation boundary check point between generations where cancellation is checked.
- The "configuration hash" is a deterministic hash of the serialized `NeatSharpOptions`, not a cryptographic hash. Its purpose is quick equality checking, not security.
- The diagnostics bundle is a single JSON document with checkpoint, configuration, environment metadata, and run history as nested top-level sections. It is not a compressed archive; consumers can apply compression at the stream level if needed.

## Non-Goals

- **GPU/CUDA evaluation**: Hardware-accelerated evaluation is deferred to Spec 06.
- **Parallel CPU evaluation**: Multi-threaded genome evaluation is not in scope.
- **Compressed artifact format**: Artifacts are plain JSON. Compression (gzip, etc.) can be applied by the consumer at the stream level but is not built into the serialization layer.
- **Cloud storage integrations**: No built-in support for S3, Azure Blob Storage, or similar. The stream abstraction enables consumers to provide their own storage backends.
- **Automatic migration between arbitrary schema versions**: The system supports migration from at most one prior version. Multi-hop migrations (v1 -> v2 -> v3) are not in scope for the initial release.
- **Encrypted artifacts**: No built-in encryption. Consumers can encrypt at the stream level.
- **Checkpoint scheduling / auto-save**: The consumer controls when checkpoints are saved. No automatic periodic checkpointing is provided.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Save a checkpoint at generation N, load it, resume training for M additional generations, and the result (champion, fitness history, final population) is identical to an uninterrupted run of N+M generations with the same seed and configuration on the same CPU.
- **SC-002**: Checkpoint round-trip (save then load) preserves all training state: population genomes (every node and connection), species metadata (ID, representative, BestFitnessEver, GenerationsSinceImprovement), innovation counters (next innovation number, next node ID), champion, seed, RNG state, generation number, and run history.
- **SC-003**: Champion export is a valid JSON document parseable by any standard JSON reader without the NEATSharp library, and the parsed graph structure (nodes, edges, weights, types) matches the original genome exactly.
- **SC-004**: Loading an artifact with a mismatched schema version produces an error message that identifies the artifact's version, the expected version, and whether migration is available — not a generic deserialization exception.
- **SC-005**: A diagnostics bundle contains all information needed to reproduce a training run: checkpoint, configuration, environment metadata (OS, runtime, architecture), and run history.
- **SC-006**: Every serialized artifact includes complete metadata: library version, schema version, seed, configuration hash, creation timestamp (UTC), and environment information.
- **SC-007**: All serialization operations work with streams (memory streams, file streams, or custom streams) — no filesystem path is hardcoded or required.
- **SC-008**: A backward-compatibility test fixture validates loading an artifact from the prior schema version (placeholder test until a second version is introduced; the test validates that the current v1.0.0 schema loads correctly).
