# Data Model: Versioned Serialization

**Feature**: 005-versioned-serialization
**Date**: 2026-02-15

## Domain Models

### TrainingCheckpoint

Complete snapshot of training state at a generation boundary. Contains everything needed for deterministic resume.

| Field | Type | Description |
|-------|------|-------------|
| `Population` | `IReadOnlyList<Genome>` | All genomes in the current generation |
| `Species` | `IReadOnlyList<SpeciesCheckpoint>` | Species state including metadata and members |
| `NextInnovationNumber` | `int` | InnovationTracker's next innovation counter |
| `NextNodeId` | `int` | InnovationTracker's next node ID counter |
| `NextSpeciesId` | `int` | CompatibilitySpeciation's next species ID counter |
| `ChampionGenome` | `Genome` | Best genome found so far (genotype, not phenotype) |
| `ChampionFitness` | `double` | Champion's fitness score |
| `ChampionGeneration` | `int` | Generation in which champion was found |
| `Generation` | `int` | Number of completed generations (0-based index of next generation) |
| `Seed` | `int` | Random seed used for the run |
| `RngState` | `RngState` | Captured System.Random internal state |
| `Configuration` | `NeatSharpOptions` | Full configuration snapshot |
| `ConfigurationHash` | `string` | SHA-256 hash of serialized configuration |
| `History` | `RunHistory` | Generation statistics collected so far |
| `Metadata` | `ArtifactMetadata` | Common artifact header (versions, timestamps, env) |

**Validation rules**:
- `Population` must not be empty (unless representing an extinct population)
- Every genome in `Population` must have valid internal structure (nodes/connections)
- `ChampionGenome` must exist in `Population` (unless population is empty)
- `NextInnovationNumber` must be > max innovation number in any connection across all genomes
- `NextNodeId` must be > max node ID in any genome
- `NextSpeciesId` must be > max species ID in `Species`
- `ConfigurationHash` must match SHA-256 of serialized `Configuration`

---

### SpeciesCheckpoint

Species state captured for checkpoint persistence. Includes all metadata needed to restore species identity and stagnation tracking.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` | Stable species identifier |
| `RepresentativeIndex` | `int` | Index into checkpoint's Population list for the representative genome |
| `BestFitnessEver` | `double` | Highest fitness achieved by any member ever |
| `GenerationsSinceImprovement` | `int` | Consecutive generations without improvement |
| `MemberIndices` | `IReadOnlyList<int>` | Indices into checkpoint's Population list |
| `MemberFitnesses` | `IReadOnlyList<double>` | Fitness values corresponding to MemberIndices |

**Design note**: Species members reference genomes by index into the checkpoint's `Population` list rather than embedding duplicate genome copies. This reduces checkpoint size and ensures referential integrity.

---

### RngState

Internal state of `System.Random` (seeded variant, `Net5CompatSeedImpl`). Captured and restored via reflection.

| Field | Type | Description |
|-------|------|-------------|
| `SeedArray` | `int[]` | 56-element internal state array |
| `Inext` | `int` | Index pointer 1 |
| `Inextp` | `int` | Index pointer 2 |

**Validation rules**:
- `SeedArray` must have exactly 56 elements
- `Inext` must be in range [0, 55]
- `Inextp` must be in range [0, 55]

---

### ArtifactMetadata

Common header present in all serialized artifacts (checkpoints, exports, bundles).

| Field | Type | Description |
|-------|------|-------------|
| `SchemaVersion` | `string` | Semantic version (e.g., "1.0.0") |
| `LibraryVersion` | `string` | Assembly version of NeatSharp |
| `Seed` | `int` | Random seed used for the run |
| `ConfigurationHash` | `string` | SHA-256 hash of configuration |
| `CreatedAtUtc` | `string` | ISO 8601 UTC timestamp (e.g., "2026-02-15T14:30:00Z") |
| `Environment` | `EnvironmentInfo` | Runtime environment details |

---

### EnvironmentInfo

Runtime environment metadata for reproducibility diagnostics.

| Field | Type | Description |
|-------|------|-------------|
| `OsDescription` | `string` | `RuntimeInformation.OSDescription` |
| `RuntimeVersion` | `string` | `RuntimeInformation.FrameworkDescription` |
| `Architecture` | `string` | `RuntimeInformation.ProcessArchitecture` |

---

### SchemaVersion

Constants and utilities for schema version management.

| Constant | Value | Description |
|----------|-------|-------------|
| `Current` | `"1.0.0"` | Current schema version |
| `MinimumSupported` | `"1.0.0"` | Oldest loadable version |

**Methods**:
- `IsCompatible(string version)` → `bool`: returns true if the version can be loaded (with or without migration)
- `NeedsMigration(string version)` → `bool`: returns true if the version is older than current but still loadable
- `Parse(string version)` → `(int Major, int Minor, int Patch)`: parses semver string

---

### EvolutionRunOptions

Options for the new `RunAsync` overload that supports checkpoint/resume.

| Field | Type | Description |
|-------|------|-------------|
| `ResumeFrom` | `TrainingCheckpoint?` | Checkpoint to resume from (null for fresh run) |
| `OnCheckpoint` | `Func<TrainingCheckpoint, CancellationToken, Task>?` | Async callback invoked at each generation boundary with current state; supports stream-based I/O per FR-018 |

---

## JSON Serialization DTOs

All DTOs live in `NeatSharp.Serialization.Dto`. They are plain POCOs with public get/set properties and parameterless constructors for clean System.Text.Json deserialization.

### CheckpointDto

Top-level JSON document for a checkpoint.

```json
{
  "schemaVersion": "1.0.0",
  "metadata": { ... },
  "population": [ ... ],
  "species": [ ... ],
  "counters": {
    "nextInnovationNumber": 42,
    "nextNodeId": 15,
    "nextSpeciesId": 4
  },
  "champion": {
    "genomeIndex": 7,
    "fitness": 3.98,
    "generation": 12
  },
  "generation": 25,
  "seed": 12345,
  "rngState": {
    "seedArray": [1, 2, ...],
    "inext": 3,
    "inextp": 24
  },
  "configuration": { ... },
  "configurationHash": "a1b2c3...",
  "history": { ... }
}
```

### GenomeDto

```json
{
  "nodes": [
    { "id": 0, "type": "input", "activationFunction": "identity" },
    { "id": 3, "type": "hidden", "activationFunction": "sigmoid" },
    { "id": 2, "type": "output", "activationFunction": "sigmoid" }
  ],
  "connections": [
    { "innovationNumber": 0, "sourceNodeId": 0, "targetNodeId": 2, "weight": 0.75, "isEnabled": true },
    { "innovationNumber": 5, "sourceNodeId": 0, "targetNodeId": 3, "weight": -0.32, "isEnabled": true }
  ]
}
```

### SpeciesCheckpointDto

```json
{
  "id": 1,
  "representativeIndex": 0,
  "bestFitnessEver": 3.98,
  "generationsSinceImprovement": 2,
  "memberIndices": [0, 1, 5, 7],
  "memberFitnesses": [3.98, 3.21, 2.87, 3.45]
}
```

### ChampionExportDto

Top-level JSON document for champion export (interoperability format).

```json
{
  "schemaVersion": "1.0.0",
  "metadata": {
    "schemaVersion": "1.0.0",
    "libraryVersion": "1.0.0",
    "seed": 12345,
    "configurationHash": "a1b2c3...",
    "createdAtUtc": "2026-02-15T14:30:00Z",
    "environment": {
      "osDescription": "Microsoft Windows 11.0.26100",
      "runtimeVersion": ".NET 8.0.12",
      "architecture": "X64"
    }
  },
  "champion": {
    "fitness": 3.98,
    "generationFound": 12
  },
  "nodes": [
    { "id": 0, "type": "input", "activationFunction": "identity" },
    { "id": 1, "type": "bias", "activationFunction": "identity" },
    { "id": 2, "type": "output", "activationFunction": "sigmoid" },
    { "id": 3, "type": "hidden", "activationFunction": "sigmoid" }
  ],
  "edges": [
    { "source": 0, "target": 2, "weight": 0.75, "enabled": true },
    { "source": 1, "target": 2, "weight": -0.12, "enabled": true },
    { "source": 0, "target": 3, "weight": 0.44, "enabled": true },
    { "source": 3, "target": 2, "weight": 0.91, "enabled": true }
  ]
}
```

### DiagnosticsBundleDto

Top-level JSON document for diagnostics bundle.

```json
{
  "schemaVersion": "1.0.0",
  "metadata": { ... },
  "checkpoint": { ... },
  "configuration": { ... },
  "environment": {
    "osDescription": "Microsoft Windows 11.0.26100",
    "runtimeVersion": ".NET 8.0.12",
    "architecture": "X64"
  },
  "history": { ... }
}
```

### ArtifactMetadataDto

```json
{
  "schemaVersion": "1.0.0",
  "libraryVersion": "1.0.0.0",
  "seed": 12345,
  "configurationHash": "a1b2c3d4e5f6...",
  "createdAtUtc": "2026-02-15T14:30:00Z",
  "environment": {
    "osDescription": "Microsoft Windows 11.0.26100",
    "runtimeVersion": ".NET 8.0.12",
    "architecture": "X64"
  }
}
```

## Entity Relationships

```text
TrainingCheckpoint
├── ArtifactMetadata
│   └── EnvironmentInfo
├── Population: List<Genome>
│   └── Genome
│       ├── NodeGene[]
│       └── ConnectionGene[]
├── Species: List<SpeciesCheckpoint>
│   └── SpeciesCheckpoint (references genomes by index)
├── RngState
├── NeatSharpOptions (configuration snapshot)
│   ├── StoppingCriteria
│   ├── ComplexityLimits
│   ├── MutationOptions
│   ├── CrossoverOptions
│   ├── SpeciationOptions
│   ├── SelectionOptions
│   ├── ComplexityPenaltyOptions
│   └── EvaluationOptions
└── RunHistory
    └── GenerationStatistics[]
        ├── ComplexityStatistics
        └── TimingBreakdown

ChampionExport
├── ArtifactMetadata (full: per FR-013)
├── Champion (fitness, generationFound)
├── Nodes: List<ExportNode>
└── Edges: List<ExportEdge>

DiagnosticsBundle
├── ArtifactMetadata
├── TrainingCheckpoint (embedded)
├── NeatSharpOptions (configuration)
├── EnvironmentInfo
└── RunHistory
```

## State Transitions

### Checkpoint Lifecycle

```
[Running] --save at gen boundary--> [TrainingCheckpoint] --serialize--> [Stream/JSON]
[Stream/JSON] --deserialize--> [TrainingCheckpoint] --validate--> [Valid Checkpoint]
[Valid Checkpoint] --resume--> [Running from gen N]
```

### Schema Version Lifecycle

```
[Load artifact] --> [Read schemaVersion field]
  |-- matches current --> [Deserialize directly]
  |-- older, migration available --> [Migrate JSON] --> [Deserialize]
  |-- older, no migration --> [SchemaVersionException]
  |-- newer than current --> [SchemaVersionException]
```
