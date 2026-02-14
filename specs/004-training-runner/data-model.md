# Data Model: Training Runner + Evaluation Adapters + Reporting

**Feature Branch**: `004-training-runner`
**Date**: 2026-02-14

## Entities

### New Types

#### IPopulationFactory

**Namespace**: `NeatSharp.Evolution`
**Purpose**: Creates the initial population of minimal-topology genomes for generation 0.
**Lifetime**: Scoped (one per evolution run)

| Method | Parameters | Returns | Notes |
|--------|-----------|---------|-------|
| `CreateInitialPopulation` | `int populationSize, int inputCount, int outputCount, Random random, IInnovationTracker tracker` | `IReadOnlyList<Genome>` | Creates N identical-topology genomes with randomized weights |

**Validation**: None at interface level — validated by caller (`NeatEvolver`) via options validation.

#### PopulationFactory

**Namespace**: `NeatSharp.Evolution`
**Purpose**: Default implementation of `IPopulationFactory`.
**Dependencies**: `IOptions<NeatSharpOptions>` (for weight min/max bounds)

**Genome structure created**:
- Nodes: `inputCount` Input nodes (IDs 0..I-1) + 1 Bias node (ID I) + `outputCount` Output nodes (IDs I+1..I+O)
- Connections: Fully connected (input+bias → output), innovation numbers assigned via tracker
- Weights: Uniform random in `[WeightMinValue, WeightMaxValue]`

**State transitions**: None (stateless factory).

#### NeatEvolver

**Namespace**: `NeatSharp.Evolution`
**Purpose**: Real implementation of `INeatEvolver`. Orchestrates the complete training loop.
**Lifetime**: Scoped (one per evolution run, matching `IInnovationTracker` and `ISpeciationStrategy`)

| Dependency | Type | Purpose |
|------------|------|---------|
| `IOptions<NeatSharpOptions>` | Options | Configuration for the run |
| `IPopulationFactory` | Scoped | Creates initial population |
| `INetworkBuilder` | Singleton | Converts Genome → IGenome for evaluation |
| `ISpeciationStrategy` | Scoped | Assigns genomes to species |
| `ReproductionOrchestrator` | Singleton | Produces next generation offspring |
| `IInnovationTracker` | Scoped | Tracks structural innovation IDs |
| `ILogger<NeatEvolver>` | Singleton | Structured logging |

**State during run** (not persisted — local to `RunAsync` call):
- `List<Genome> population` — current generation's genomes
- `double[] fitness` — fitness scores indexed by genome position
- `List<Species> species` — current species list (mutated by speciation)
- `Champion? champion` — running best genome/fitness/generation
- `List<GenerationStatistics> history` — per-generation metrics (empty if metrics disabled)
- `int generation` — current generation counter (0-indexed)
- `int seed` — resolved seed (from options or auto-generated)
- `Random random` — seeded PRNG instance

#### TrainingLog

**Namespace**: `NeatSharp.Evolution`
**Purpose**: Source-generated `[LoggerMessage]` methods for structured logging of training events.
**Type**: `static partial class`

| Event | Level | Parameters | Event ID |
|-------|-------|-----------|----------|
| GenerationCompleted | Information | generation, bestFitness, avgFitness, speciesCount | 1001 |
| NewBestFitness | Information | generation, fitness, previousBest | 1002 |
| SpeciesExtinct | Warning | speciesId, generation | 1003 |
| StagnationDetected | Warning | speciesId, generationsSinceImprovement | 1004 |
| RunCompleted | Information | totalGenerations, championFitness, championGeneration, wasCancelled | 1005 |
| EvaluationFailed | Warning | genomeIndex, exceptionMessage | 1006 |

### Modified Types

#### NeatSharpOptions

**Added properties**:

| Property | Type | Default | Validation | FR |
|----------|------|---------|------------|-----|
| `InputCount` | `int` | 2 | `[Range(1, 10_000)]` | FR-001 |
| `OutputCount` | `int` | 1 | `[Range(1, 10_000)]` | FR-001 |

#### ServiceCollectionExtensions

**Changes**:
- Replace `NeatEvolverStub` registration with `NeatEvolver` (scoped)
- Add `IPopulationFactory → PopulationFactory` registration (scoped)
- Remove `NeatEvolverStub` private class

### Modified Types

#### GenerationStatistics

**Namespace**: `NeatSharp.Reporting`
**Purpose**: Per-generation metrics snapshot. Modified to add species sizes per FR-015.

| Added Property | Type | Purpose | FR |
|---------------|------|---------|-----|
| `SpeciesSizes` | `IReadOnlyList<int>` | Member count per species, ordered by species ID | FR-015 |

### Existing Types (Consumed, Not Modified)

| Type | Role in Training Loop |
|------|----------------------|
| `Genome` | Genotype — subject of evolution operations |
| `IGenome` / `FeedForwardNetwork` | Phenotype — used for fitness evaluation |
| `Species` | Species tracking — members, stagnation, representatives |
| `IEvaluationStrategy` | Evaluates population fitness |
| `ISpeciationStrategy` | Assigns genomes to species |
| `ReproductionOrchestrator` | Produces offspring via crossover + mutation |
| `ReproductionAllocator` | Allocates offspring counts per species |
| `IInnovationTracker` | Assigns innovation numbers for structural mutations |
| `INetworkBuilder` | Converts Genome → IGenome |
| `Champion` | Record: best genome, fitness, generation found |
| `RunHistory` | Record: list of generation statistics + total count |
| `EvolutionResult` | Record: champion, population, history, seed, cancelled |
| `PopulationSnapshot` | Record: species list snapshot at run end |
| `SpeciesSnapshot` | Record: single species members snapshot |
| `GenomeInfo` | Record: single genome's fitness and complexity |
| `IRunReporter` | Generates human-readable summary from result |

## Relationships

```text
NeatEvolver ──uses──► IPopulationFactory (creates initial population)
NeatEvolver ──uses──► INetworkBuilder (genome → phenotype conversion)
NeatEvolver ──uses──► IEvaluationStrategy (fitness evaluation)
NeatEvolver ──uses──► ISpeciationStrategy (species assignment)
NeatEvolver ──uses──► ReproductionOrchestrator (offspring production)
NeatEvolver ──uses──► IInnovationTracker (generation advance signal)
NeatEvolver ──uses──► ILogger<NeatEvolver> (structured logging)
NeatEvolver ──produces──► EvolutionResult

PopulationFactory ──uses──► IInnovationTracker (initial connection innovation numbers)
PopulationFactory ──creates──► Genome (minimal-topology genomes)

EvolutionResult ──contains──► Champion
EvolutionResult ──contains──► PopulationSnapshot
EvolutionResult ──contains──► RunHistory
RunHistory ──contains──► GenerationStatistics[]
PopulationSnapshot ──contains──► SpeciesSnapshot[]
SpeciesSnapshot ──contains──► GenomeInfo[]
```

## Evolution Loop State Machine

```text
┌─────────────────┐
│  Initialize      │ Create population, resolve seed, set generation=0
└────────┬────────┘
         ▼
┌─────────────────┐
│  Check Cancel    │◄─────────────────────────────────────────────┐
│  (generation     │ If cancelled: return result (WasCancelled)   │
│   boundary)      │                                              │
└────────┬────────┘                                               │
         ▼                                                        │
┌─────────────────┐                                               │
│  Evaluate        │ Build phenotypes → evaluate → assign fitness │
│                  │ Track champion. Handle errors (FR-021).      │
└────────┬────────┘                                               │
         ▼                                                        │
┌─────────────────┐                                               │
│  Speciate        │ Assign genomes to species. Update stagnation.│
└────────┬────────┘                                               │
         ▼                                                        │
┌─────────────────┐                                               │
│  Collect Metrics │ If EnableMetrics: record GenerationStatistics │
│  + Log Events    │ Log generation completed, stagnation, etc.   │
└────────┬────────┘                                               │
         ▼                                                        │
┌─────────────────┐                                               │
│  Check Stopping  │ MaxGenerations? FitnessTarget? All-stagnant? │
│  Criteria        │ If any met: return result (WasCancelled=false│
└────────┬────────┘                                               │
         ▼                                                        │
┌─────────────────┐                                               │
│  Reproduce       │ Allocate offspring → crossover/mutate        │
│                  │ Enforce complexity limits (FR-007)            │
└────────┬────────┘                                               │
         ▼                                                        │
┌─────────────────┐                                               │
│  Advance         │ tracker.NextGeneration()                     │
│  Generation      │ generation++; population = offspring         │
└────────┬────────┘                                               │
         └───────────────────────────────────────────────────────►┘
```
