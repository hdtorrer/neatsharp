# Data Model: Core Package, Public API & Reproducibility Baseline

**Feature**: 001-core-api-baseline
**Date**: 2026-02-12

## Entity Overview

```text
NeatSharpOptions ──────┐
├── StoppingCriteria   │
└── ComplexityLimits   │
                       ▼
              INeatEvolver.RunAsync(IEvaluationStrategy, CancellationToken)
                       │
                       ▼
              EvolutionResult
              ├── Champion
              │   └── IGenome (ref)
              ├── RunHistory
              │   └── GenerationStatistics[]
              │       └── ComplexityStatistics
              ├── PopulationSnapshot
              │   └── SpeciesSnapshot[]
              │       └── GenomeInfo[]
              ├── WasCancelled: bool
              └── Seed: int
```

---

## Configuration Entities

### NeatSharpOptions

The complete set of parameters governing an evolution run. Registered via the Options pattern and validated at DI startup.

| Field | Type | Default | Validation | FR |
|-------|------|---------|------------|-----|
| `PopulationSize` | `int` | `150` | `[Range(1, 100_000)]` | FR-001 |
| `Seed` | `int?` | `null` | None (null = auto-generate) | FR-001, FR-010 |
| `Stopping` | `StoppingCriteria` | `new()` | At least one criterion required | FR-001 |
| `Complexity` | `ComplexityLimits` | `new()` | Fields must be null or > 0 | FR-001 |
| `EnableMetrics` | `bool` | `true` | None | FR-001, FR-012, FR-013 |

**Namespace**: `NeatSharp.Configuration`
**Notes**: `Seed = null` triggers auto-generation via `Random.Shared.Next()` and recording in `EvolutionResult.Seed`. Logging is controlled by the host's `ILogger` configuration, not by an options toggle (see R-006).

### StoppingCriteria

Defines when evolution terminates. At least one criterion must be configured.

| Field | Type | Default | Validation | FR |
|-------|------|---------|------------|-----|
| `MaxGenerations` | `int?` | `null` | Must be > 0 if set | FR-001 |
| `FitnessTarget` | `double?` | `null` | Must be finite if set | FR-001 |
| `StagnationThreshold` | `int?` | `null` | Must be > 0 if set | FR-001 |

**Namespace**: `NeatSharp.Configuration`
**Validation rule**: `IValidateOptions<NeatSharpOptions>` checks that at least one of the three fields is non-null. Error message: `"At least one stopping criterion is required (MaxGenerations, FitnessTarget, or StagnationThreshold)."`
**State transitions**: N/A — immutable configuration.

### ComplexityLimits

Configurable bounds on network size to prevent unbounded growth.

| Field | Type | Default | Validation | FR |
|-------|------|---------|------------|-----|
| `MaxNodes` | `int?` | `null` | Must be > 0 if set | FR-001 |
| `MaxConnections` | `int?` | `null` | Must be > 0 if set | FR-001 |

**Namespace**: `NeatSharp.Configuration`
**Notes**: `null` means unbounded. Sensible defaults may be established in future features once the algorithm is implemented.

---

## Result Entities

### EvolutionResult

The complete output of an evolution run.

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `Champion` | `Champion` | Best genome found during the run | FR-006 |
| `Population` | `PopulationSnapshot` | Final population state | FR-007 |
| `History` | `RunHistory` | Generation-by-generation record | FR-008 |
| `Seed` | `int` | Seed used (auto-generated or provided) | FR-010 |
| `WasCancelled` | `bool` | `true` if run was cancelled via CancellationToken | FR-018 |

**Namespace**: `NeatSharp.Evolution`
**Notes**: Immutable record. Always returned (never null), even on cancellation.

### Champion

The highest-fitness genome produced by a run.

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `Genome` | `IGenome` | The best-performing genome | FR-006 |
| `Fitness` | `double` | Fitness score | FR-006 |
| `Generation` | `int` | Generation in which this champion was found | FR-006 |

**Namespace**: `NeatSharp.Reporting`
**Notes**: Immutable record.

### RunHistory

Chronological record of an evolution run.

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `Generations` | `IReadOnlyList<GenerationStatistics>` | Per-generation statistics | FR-008 |
| `TotalGenerations` | `int` | Number of generations completed | FR-008 |

**Namespace**: `NeatSharp.Reporting`
**Notes**: When `NeatSharpOptions.EnableMetrics` is `false`, `Generations` is an empty list and `TotalGenerations` still reflects the actual count.

### GenerationStatistics

Per-generation snapshot of population metrics.

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `Generation` | `int` | Zero-based generation index | FR-008, FR-012 |
| `BestFitness` | `double` | Highest fitness in this generation | FR-008, FR-012 |
| `AverageFitness` | `double` | Mean fitness across the population | FR-008, FR-012 |
| `SpeciesCount` | `int` | Number of species | FR-008, FR-012 |
| `Complexity` | `ComplexityStatistics` | Complexity measures | FR-008, FR-012 |
| `Timing` | `TimingBreakdown` | Per-phase timing measures | FR-008 |

**Namespace**: `NeatSharp.Reporting`

### ComplexityStatistics

Aggregate complexity measures for a generation.

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `AverageNodes` | `double` | Mean node count across genomes | FR-008, FR-012 |
| `AverageConnections` | `double` | Mean connection count across genomes | FR-008, FR-012 |

**Namespace**: `NeatSharp.Reporting`

### PopulationSnapshot

Point-in-time view of the entire population at run completion.

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `Species` | `IReadOnlyList<SpeciesSnapshot>` | Species groupings | FR-007 |
| `TotalCount` | `int` | Total number of genomes | FR-007 |

**Namespace**: `NeatSharp.Reporting`

### SpeciesSnapshot

A single species within a population snapshot.

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `Id` | `int` | Species identifier | FR-007 |
| `Members` | `IReadOnlyList<GenomeInfo>` | Genomes in this species | FR-007 |

**Namespace**: `NeatSharp.Reporting`

### GenomeInfo

Lightweight summary of a genome within a snapshot (avoids exposing full genome internals).

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `Fitness` | `double` | Fitness score | FR-007 |
| `NodeCount` | `int` | Number of nodes | FR-007 |
| `ConnectionCount` | `int` | Number of connections | FR-007 |

**Namespace**: `NeatSharp.Reporting`

### TimingBreakdown

Aggregate timing measures for a generation.

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `Evaluation` | `TimeSpan` | Time spent evaluating genomes | FR-008 |
| `Reproduction` | `TimeSpan` | Time spent on selection, crossover, and mutation | FR-008 |
| `Speciation` | `TimeSpan` | Time spent on speciation | FR-008 |

**Namespace**: `NeatSharp.Reporting`
**Notes**: Values are `TimeSpan.Zero` until the algorithm implementation populates them.

---

## Contract Entities (Interfaces)

### IGenome

Represents a genome (neural network) that can be activated to produce outputs from inputs.

| Member | Signature | Description | FR |
|--------|-----------|-------------|-----|
| `Activate` | `void Activate(ReadOnlySpan<double> inputs, Span<double> outputs)` | Feed-forward activation | FR-003, FR-004 |
| `NodeCount` | `int { get; }` | Number of nodes | FR-008 |
| `ConnectionCount` | `int { get; }` | Number of connections | FR-008 |

**Namespace**: `NeatSharp.Genetics`
**Notes**: The `Activate` method uses span-based signatures for performance (constitution CUDA interop guidelines recommend span-based APIs). An `IReadOnlyList<double>` convenience overload may be added as an extension method.

### IEvaluationStrategy

Internal abstraction consumed by the evolution engine. Users create instances via `EvaluationStrategy` factory methods.

| Member | Signature | Description | FR |
|--------|-----------|-------------|-----|
| `EvaluateAsync` | `Task EvaluatePopulationAsync(IReadOnlyList<IGenome> genomes, Action<int, double> setFitness, CancellationToken cancellationToken)` | Evaluate all genomes in a population | FR-003, FR-004, FR-005 |

**Namespace**: `NeatSharp.Evaluation`
**Notes**: The `setFitness` callback allows the strategy to report scores incrementally. This unified signature supports simple (iterate and score), environment (iterate and run episodes), and batch (score all at once) patterns.

### IEnvironmentEvaluator

User-facing interface for episode-based evaluation (FR-004).

| Member | Signature | Description | FR |
|--------|-----------|-------------|-----|
| `EvaluateAsync` | `Task<double> EvaluateAsync(IGenome genome, CancellationToken cancellationToken)` | Run a genome through an environment and return fitness | FR-004 |

**Namespace**: `NeatSharp.Evaluation`

### IBatchEvaluator

User-facing interface for batch evaluation (FR-005).

| Member | Signature | Description | FR |
|--------|-----------|-------------|-----|
| `EvaluateAsync` | `Task EvaluateAsync(IReadOnlyList<IGenome> genomes, Action<int, double> setFitness, CancellationToken cancellationToken)` | Score multiple genomes in one call | FR-005 |

**Namespace**: `NeatSharp.Evaluation`
**Notes**: Uses the same `setFitness` callback pattern as `IEvaluationStrategy` to avoid allocating a results list.

### IRunReporter

Produces human-readable text summaries of evolution results.

| Member | Signature | Description | FR |
|--------|-----------|-------------|-----|
| `GenerateSummary` | `string GenerateSummary(EvolutionResult result)` | Produce a text summary of the run | FR-019 |

**Namespace**: `NeatSharp.Reporting`
**Notes**: A default implementation is registered by `AddNeatSharp()`. Consumers can replace it with a custom implementation via DI. The default summary includes champion fitness, generation count, seed used, species count, and cancellation status.

---

## Exceptions

### NeatSharpException

Base exception for all library-specific error conditions.

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `Message` | `string` | Actionable error description | FR-014 |
| `InnerException` | `Exception?` | Wrapped original exception | Edge case: fitness function throws |

**Namespace**: `NeatSharp.Exceptions`
**Notes**: Inherits from `Exception`. Used to wrap user fitness function exceptions (edge case from spec). Subclasses will be added in future features (e.g., `NeatSharpGpuException` for CUDA errors).

---

## Relationship Summary

```text
NeatSharpOptions 1──1 StoppingCriteria
NeatSharpOptions 1──1 ComplexityLimits
EvolutionResult  1──1 Champion
EvolutionResult  1──1 RunHistory
EvolutionResult  1──1 PopulationSnapshot
RunHistory       1──* GenerationStatistics
GenerationStatistics 1──1 ComplexityStatistics
GenerationStatistics 1──1 TimingBreakdown
PopulationSnapshot 1──* SpeciesSnapshot
SpeciesSnapshot  1──* GenomeInfo
Champion         *──1 IGenome
```

All result entities are immutable (records or read-only properties). Configuration entities are mutable during setup but effectively frozen once the Options system validates them.
