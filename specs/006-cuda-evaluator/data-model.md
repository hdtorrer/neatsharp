# Data Model: CUDA Evaluator Backend

**Feature**: 006-cuda-evaluator | **Date**: 2026-02-15

## Entities

### GpuDeviceInfo

Represents a detected GPU device and its compatibility status.

| Field | Type | Description |
|-------|------|-------------|
| DeviceName | `string` | Human-readable GPU name (e.g., "NVIDIA GeForce RTX 4090") |
| ComputeCapability | `Version` | CUDA compute capability (e.g., 8.9) |
| MemoryBytes | `long` | Total GPU device memory in bytes |
| IsCompatible | `bool` | Whether the device meets minimum requirements (CC >= 5.0) |
| DiagnosticMessage | `string?` | Null if compatible; actionable message if incompatible |

**Validation Rules**:
- `ComputeCapability` must be >= 5.0 for `IsCompatible` to be true
- `DiagnosticMessage` must be non-null when `IsCompatible` is false
- `MemoryBytes` must be > 0

**State Transitions**: Immutable after creation. Cached by `GpuDeviceDetector`.

---

### GpuOptions

Configuration for GPU evaluation behavior.

| Field | Type | Default | Validation | Description |
|-------|------|---------|------------|-------------|
| EnableGpu | `bool` | `true` | — | Whether to attempt GPU evaluation. When false, forces CPU-only regardless of hardware. |
| MinComputeCapability | `int` | `50` | [20, 100] | Minimum CUDA CC as integer (5.0 → 50). Devices below this are rejected. |
| BestEffortDeterministic | `bool` | `false` | — | When true, uses deterministic algorithms where available (may reduce throughput). |
| MaxPopulationSize | `int?` | `null` | [1, 1_000_000] if set | Maximum population size for GPU buffer preallocation. Null = auto-size from first batch. |

**Validation Rules**:
- `MinComputeCapability` must be in range [20, 100] (CC 2.0 to 10.0)
- `MaxPopulationSize` must be >= 1 if set
- Validated via `IValidateOptions<GpuOptions>` at startup

---

### GpuFeedForwardNetwork

Wraps a CPU phenotype with GPU-ready flat topology data. Implements `IGenome`.

| Field | Type | Visibility | Description |
|-------|------|------------|-------------|
| _cpuNetwork | `IGenome` | private | CPU-fallback phenotype from `FeedForwardNetworkBuilder` |
| NodeCount | `int` | public (IGenome) | Total reachable node count |
| ConnectionCount | `int` | public (IGenome) | Total reachable connection count |
| InputIndices | `int[]` | internal | Buffer indices for input nodes (in genome declaration order) |
| BiasIndices | `int[]` | internal | Buffer indices for bias nodes |
| OutputIndices | `int[]` | internal | Buffer indices for output nodes |
| NodeActivationTypes | `int[]` | internal | Activation function type per node (see `GpuActivationFunction` enum) |
| EvalOrder | `GpuEvalNode[]` | internal | Topologically sorted hidden+output nodes with incoming connections |

**`GpuEvalNode` (internal record struct)**:
| Field | Type | Description |
|-------|------|-------------|
| BufferIndex | `int` | Index in the activation buffer |
| IncomingSources | `int[]` | Source node buffer indices for incoming connections |
| IncomingWeights | `float[]` | Weights for incoming connections (fp32) |
| ActivationType | `int` | GPU activation function enum value |

**Validation Rules**:
- `_cpuNetwork` must not be null
- `InputIndices.Length` must equal the genome's declared input count
- `OutputIndices.Length` must equal the genome's declared output count
- All `ActivationType` values must be valid `GpuActivationFunction` enum values (0-4)
- All `IncomingSources` indices must be valid buffer indices (< NodeCount)

**State Transitions**: Immutable after construction. Created once per genome per generation by `GpuNetworkBuilder`.

---

### GpuPopulationData

Flattened batch topology buffers for an entire population, ready for GPU upload.

| Field | Type | Description |
|-------|------|-------------|
| PopulationSize | `int` | Number of genomes in the batch |
| TotalNodes | `int` | Sum of all genomes' node counts |
| TotalEvalNodes | `int` | Sum of all genomes' eval order lengths |
| TotalIncoming | `int` | Sum of all incoming connections across all eval nodes |
| TotalInputs | `int` | Sum of all genomes' input counts |
| TotalOutputs | `int` | Sum of all genomes' output counts |
| GenomeNodeOffsets | `int[]` | `[populationSize]` Start index per genome in node arrays |
| GenomeNodeCounts | `int[]` | `[populationSize]` Node count per genome |
| GenomeEvalOrderOffsets | `int[]` | `[populationSize]` Start index per genome in eval order |
| GenomeEvalOrderCounts | `int[]` | `[populationSize]` Eval order length per genome |
| GenomeInputOffsets | `int[]` | `[populationSize]` Start index per genome in input arrays |
| GenomeInputCounts | `int[]` | `[populationSize]` Input count per genome |
| GenomeOutputOffsets | `int[]` | `[populationSize]` Start index per genome in output arrays |
| GenomeOutputCounts | `int[]` | `[populationSize]` Output count per genome |
| NodeActivationTypes | `int[]` | `[totalNodes]` Activation type per node |
| EvalNodeIndices | `int[]` | `[totalEvalNodes]` Node buffer index in eval order |
| EvalNodeConnOffsets | `int[]` | `[totalEvalNodes]` Start index into incoming arrays |
| EvalNodeConnCounts | `int[]` | `[totalEvalNodes]` Incoming connection count per eval node |
| IncomingSourceIndices | `int[]` | `[totalIncoming]` Source node buffer index |
| IncomingWeights | `float[]` | `[totalIncoming]` Connection weights (fp32) |
| InputNodeIndices | `int[]` | `[totalInputs]` Buffer indices for all input nodes |
| OutputNodeIndices | `int[]` | `[totalOutputs]` Buffer indices for all output nodes |

**Construction**: Built from `IReadOnlyList<GpuFeedForwardNetwork>` by iterating each genome and flattening into contiguous arrays with offset tracking.

**Validation Rules**:
- All offset arrays must be monotonically increasing
- All buffer indices must be within bounds
- `PopulationSize` must be > 0

---

### GpuActivationFunction (enum)

Maps activation function names to GPU-compatible integer codes.

| Value | Name | CPU Equivalent | GPU Formula (fp32) |
|-------|------|---------------|---------------------|
| 0 | Sigmoid | `ActivationFunctions.Sigmoid` | `1.0f / (1.0f + XMath.Exp(-4.9f * x))` |
| 1 | Tanh | `ActivationFunctions.Tanh` | `XMath.Tanh(x)` |
| 2 | ReLU | `ActivationFunctions.ReLU` | `XMath.Max(0.0f, x)` |
| 3 | Step | `ActivationFunctions.Step` | `x > 0.0f ? 1.0f : 0.0f` |
| 4 | Identity | `ActivationFunctions.Identity` | `x` |

**Mapping**: `GpuNetworkBuilder` maps `NodeGene.ActivationFunction` string names to enum values using a case-insensitive dictionary. Unknown activation functions throw `GpuEvaluationException` at build time.

---

### GpuEvaluationException

GPU-specific evaluation failure. Extends `NeatSharpException`.

| Field | Type | Description |
|-------|------|-------------|
| Message | `string` | Actionable error description |
| InnerException | `Exception?` | Original ILGPU/CUDA exception |

**Subclasses**:
- `GpuOutOfMemoryException`: GPU memory exhausted. Message includes population size, estimated memory, and available GPU memory.
- `GpuDeviceException`: Device incompatible or unavailable. Message includes device name, compute capability, minimum required CC, and installation guidance.

---

## Relationships

```text
GpuOptions ─────configured via────→ AddNeatSharpGpu()
                                        │
                                        ├──→ GpuDeviceDetector ──detects──→ GpuDeviceInfo
                                        │
                                        ├──→ GpuNetworkBuilder ──decorates──→ FeedForwardNetworkBuilder
                                        │         │
                                        │         └──builds──→ GpuFeedForwardNetwork
                                        │                         │
                                        │                         ├── wraps ──→ IGenome (CPU fallback)
                                        │                         └── carries ──→ GpuEvalNode[]
                                        │
                                        └──→ GpuBatchEvaluator ──implements──→ IBatchEvaluator
                                                  │
                                                  ├── uses ──→ IGpuFitnessFunction (user-provided)
                                                  ├── builds ──→ GpuPopulationData (from GpuFeedForwardNetwork[])
                                                  ├── manages ──→ ILGPU Accelerator + MemoryBuffers
                                                  └── dispatches ──→ ForwardPropagationKernel
```

## Data Flow (per generation)

```text
1. NeatEvolver calls GpuNetworkBuilder.Build(genome) for each genome
   ├── Inner FeedForwardNetworkBuilder builds CPU phenotype (IGenome)
   ├── GpuNetworkBuilder extracts flat topology from Genome
   └── Returns GpuFeedForwardNetwork (wraps both)

2. NeatEvolver calls GpuBatchEvaluator.EvaluateAsync(genomes[])
   ├── Type-check: all genomes are GpuFeedForwardNetwork? (else CPU fallback)
   ├── Build GpuPopulationData (flatten all topologies into contiguous arrays)
   ├── Upload to GPU: topology arrays + IGpuFitnessFunction.InputCases
   ├── Launch ForwardPropagationKernel (1 thread per genome, all test cases)
   ├── Synchronize + download output buffer
   ├── For each genome: call IGpuFitnessFunction.ComputeFitness(outputs)
   ├── Call setFitness(index, fitness) for each genome
   └── On GPU failure: log warning, fall back to CPU for entire generation
```
