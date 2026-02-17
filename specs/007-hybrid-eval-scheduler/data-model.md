# Data Model: Hybrid Evaluation Scheduler

**Feature**: 007-hybrid-eval-scheduler | **Date**: 2026-02-16

## Entities

### HybridOptions

Configuration for hybrid CPU+GPU evaluation behavior.

| Field | Type | Default | Validation | Description |
|-------|------|---------|------------|-------------|
| EnableHybrid | `bool` | `true` | — | Whether to use hybrid evaluation. When false, delegates to the inner evaluator directly with zero overhead. |
| SplitPolicy | `SplitPolicyType` | `Adaptive` | Must be valid enum | Partitioning policy: `Static`, `CostBased`, or `Adaptive`. |
| StaticGpuFraction | `double` | `0.7` | [0.0, 1.0] | GPU fraction for static split policy. 0.0 = all CPU, 1.0 = all GPU. |
| MinPopulationForSplit | `int` | `50` | [2, 100_000] | Minimum population size for hybrid splitting. Below this, single-backend evaluation is used. |
| GpuReprobeInterval | `int` | `10` | [1, 1_000] | Generations between GPU availability re-probes after a failure. |
| Adaptive | `AdaptivePidOptions` | (see below) | Validated as sub-object | PID controller tuning parameters for adaptive policy. |
| CostModel | `CostModelOptions` | (see below) | Validated as sub-object | Cost model weights for cost-based policy. |

**Validation Rules**:
- `StaticGpuFraction` must be in [0.0, 1.0]
- `MinPopulationForSplit` must be >= 2 (need at least 1 genome per backend)
- `GpuReprobeInterval` must be >= 1
- Validated via `IValidateOptions<HybridOptions>` at startup

---

### AdaptivePidOptions

PID controller parameters for the adaptive split policy.

| Field | Type | Default | Validation | Description |
|-------|------|---------|------------|-------------|
| Kp | `double` | `0.5` | (0.0, 10.0] | Proportional gain. Controls response magnitude to current error. |
| Ki | `double` | `0.1` | [0.0, 10.0] | Integral gain. Eliminates steady-state offset. |
| Kd | `double` | `0.05` | [0.0, 10.0] | Derivative gain. Dampens oscillations. |
| InitialGpuFraction | `double` | `0.5` | [0.0, 1.0] | Starting GPU fraction before any measurements. |

**Validation Rules**:
- `Kp` must be > 0 (zero proportional gain means no response)
- `Ki` and `Kd` may be 0 (disable integral/derivative terms)
- `InitialGpuFraction` must be in [0.0, 1.0]

---

### CostModelOptions

Weights for the linear cost estimation model.

| Field | Type | Default | Validation | Description |
|-------|------|---------|------------|-------------|
| NodeWeight | `double` | `1.0` | [0.0, 1000.0] | Weight (alpha) for node count in cost formula. |
| ConnectionWeight | `double` | `1.0` | [0.0, 1000.0] | Weight (beta) for connection count in cost formula. |

**Cost formula**: `cost(genome) = NodeWeight * genome.NodeCount + ConnectionWeight * genome.ConnectionCount`

---

### SplitPolicyType (enum)

Selects the partitioning policy for hybrid evaluation.

| Value | Name | Description |
|-------|------|-------------|
| 0 | Static | Fixed split ratio from `StaticGpuFraction` option. |
| 1 | CostBased | Complexity-driven split using genome structural properties. |
| 2 | Adaptive | Throughput-driven PID controller targeting zero idle-time difference. |

---

### IPartitionPolicy

Abstraction for partitioning a population across CPU and GPU backends.

| Method | Signature | Description |
|--------|-----------|-------------|
| Partition | `PartitionResult Partition(IReadOnlyList<IGenome> genomes, int[] originalIndices)` | Assigns each genome to either CPU or GPU backend. Returns two lists with their original indices. |
| Update | `void Update(SchedulingMetrics metrics)` | Feeds back metrics from the completed generation to inform the next partition. |

**Implementations**:
- `StaticPartitionPolicy`: Assigns first `(1 - gpuFraction) * count` genomes to CPU, rest to GPU. Deterministic, no state.
- `CostBasedPartitionPolicy`: Sorts genomes by `cost(g) = alpha * NodeCount + beta * ConnectionCount` descending. Assigns highest-cost genomes to GPU up to `StaticGpuFraction`. Update is no-op (stateless policy).
- `AdaptivePartitionPolicy`: Standalone `IPartitionPolicy` implementation. Maintains PID controller state. Adjusts GPU fraction each generation based on idle-time error signal. Internally performs a count-based split (same logic as static) using the PID-controlled GPU fraction — does not wrap another `IPartitionPolicy` instance.

---

### PartitionResult

Output of the partition step — two disjoint sets of genomes with their original indices.

| Field | Type | Description |
|-------|------|-------------|
| CpuGenomes | `IReadOnlyList<IGenome>` | Genomes assigned to CPU evaluation |
| CpuIndices | `int[]` | Original indices of CPU genomes in the input list |
| GpuGenomes | `IReadOnlyList<IGenome>` | Genomes assigned to GPU evaluation |
| GpuIndices | `int[]` | Original indices of GPU genomes in the input list |

**Invariants**:
- `CpuGenomes.Count + GpuGenomes.Count` == total input genome count
- `CpuIndices` and `GpuIndices` are disjoint and their union is [0, count)
- Each genome appears in exactly one partition

---

### SchedulingMetrics

Per-generation metrics capturing hybrid scheduler performance. Emitted after each generation completes.

| Field | Type | Description |
|-------|------|-------------|
| Generation | `int` | Generation number |
| CpuGenomeCount | `int` | Number of genomes evaluated by CPU backend |
| GpuGenomeCount | `int` | Number of genomes evaluated by GPU backend |
| CpuThroughput | `double` | CPU throughput in genomes/sec |
| GpuThroughput | `double` | GPU throughput in genomes/sec |
| CpuLatency | `TimeSpan` | Wall-clock time for CPU evaluation |
| GpuLatency | `TimeSpan` | Wall-clock time for GPU evaluation |
| SplitRatio | `double` | Current GPU fraction (0.0 = all CPU, 1.0 = all GPU) |
| ActivePolicy | `SplitPolicyType` | Which partitioning policy was used |
| FallbackEvent | `FallbackEventInfo?` | Non-null if a GPU fallback occurred this generation |
| SchedulerOverhead | `TimeSpan` | Time spent in partitioning + dispatch + merge (excludes backend evaluation) |

**State Transitions**: Immutable after creation. Created by `HybridBatchEvaluator` at the end of each evaluation.

---

### FallbackEventInfo

Details of a GPU fallback event within a generation.

| Field | Type | Description |
|-------|------|-------------|
| Timestamp | `DateTimeOffset` | When the fallback occurred |
| FailureReason | `string` | Exception message or error description |
| GenomesRerouted | `int` | Number of genomes rerouted from GPU to CPU |

---

### ISchedulingMetricsReporter

Interface for receiving per-generation scheduling metrics.

| Method | Signature | Description |
|--------|-----------|-------------|
| Report | `void Report(SchedulingMetrics metrics)` | Called once per generation with the completed metrics. |

**Default implementation**: `LoggingMetricsReporter` — logs metrics summary at `Information` level, fallback events at `Warning` level.

---

### HybridBatchEvaluator

The top-level hybrid evaluator implementing `IBatchEvaluator` as a decorator.

| Field | Type | Visibility | Description |
|-------|------|------------|-------------|
| _cpuEvaluator | `IBatchEvaluator` | private | CPU evaluation backend |
| _gpuEvaluator | `IBatchEvaluator` | private | GPU evaluation backend |
| _partitionPolicy | `IPartitionPolicy` | private | Active partitioning policy |
| _metricsReporter | `ISchedulingMetricsReporter` | private | Metrics emission target |
| _options | `HybridOptions` | private | Configuration |
| _logger | `ILogger<HybridBatchEvaluator>` | private | Structured logging |
| _gpuAvailable | `bool` | private | Whether GPU is currently usable |
| _generationsSinceGpuFailure | `int` | private | Counter for re-probe scheduling |
| _generation | `int` | private | Current generation counter |

**Key behavior**:
- Implements `IBatchEvaluator.EvaluateAsync()`
- Below `MinPopulationForSplit`: delegates to single backend
- Above threshold: partitions, dispatches concurrently, merges via callbacks
- On GPU failure: reroutes to CPU, tracks failure state, schedules re-probe
- Emits `SchedulingMetrics` after each evaluation

---

### PidControllerState

Internal state for the adaptive PID controller.

| Field | Type | Description |
|-------|------|-------------|
| GpuFraction | `double` | Current GPU fraction (the control variable) |
| Integral | `double` | Accumulated integral term |
| PreviousError | `double` | Error from last generation (for derivative) |
| IsInitialized | `bool` | Whether at least one measurement has been taken |

**State Transitions**: Updated after each generation's metrics are processed. Reset when GPU fails (re-initialize from defaults on recovery).

---

## Relationships

```text
HybridOptions ─────configured via────→ AddNeatSharpHybrid()
                                            │
                                            ├──→ HybridBatchEvaluator ──implements──→ IBatchEvaluator
                                            │         │
                                            │         ├── wraps ──→ IBatchEvaluator (CPU)
                                            │         ├── wraps ──→ IBatchEvaluator (GPU)
                                            │         ├── uses ──→ IPartitionPolicy
                                            │         ├── emits ──→ SchedulingMetrics
                                            │         └── reports to ──→ ISchedulingMetricsReporter
                                            │
                                            ├──→ IPartitionPolicy (selected by SplitPolicyType)
                                            │         ├── StaticPartitionPolicy
                                            │         ├── CostBasedPartitionPolicy
                                            │         └── AdaptivePartitionPolicy
                                            │                   └── contains ──→ PidControllerState
                                            │
                                            └──→ ISchedulingMetricsReporter
                                                      └── LoggingMetricsReporter (default)
```

## Data Flow (per generation)

```text
1. NeatEvolver calls HybridBatchEvaluator.EvaluateAsync(genomes[], setFitness, ct)
   │
   ├── Check: genomes.Count < MinPopulationForSplit?
   │     └── YES: delegate to single backend (CPU or GPU based on throughput history)
   │
   ├── Check: GPU available? (not failed, or reprobe due)
   │     └── NO + reprobe due: attempt GPU re-initialization
   │
   ├── Partition: IPartitionPolicy.Partition(genomes) → PartitionResult
   │     ├── CPU partition: [cpuGenomes, cpuIndices]
   │     └── GPU partition: [gpuGenomes, gpuIndices]
   │
   ├── Dispatch concurrently:
   │     ├── Task cpuTask = _cpuEvaluator.EvaluateAsync(cpuGenomes, remappedSetFitness, ct)
   │     └── Task gpuTask = _gpuEvaluator.EvaluateAsync(gpuGenomes, remappedSetFitness, ct)
   │
   ├── await Task.WhenAll(cpuTask, gpuTask)
   │     └── On GPU failure:
   │           ├── Log warning (FR-008)
   │           ├── Reroute GPU genomes to CPU (FR-007)
   │           ├── Mark GPU unavailable
   │           └── Reset reprobe counter
   │
   ├── Compute SchedulingMetrics
   │     ├── Genome counts, throughput, latency per backend
   │     ├── Split ratio, overhead timing
   │     └── FallbackEvent if GPU failed
   │
   ├── IPartitionPolicy.Update(metrics)  ← feeds adaptive PID
   │
   └── ISchedulingMetricsReporter.Report(metrics)
```
