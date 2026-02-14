# Data Model: Genome Model + Innovation Tracking + Feed-Forward Phenotype

**Feature**: 002-genome-phenotype
**Date**: 2026-02-13

## Entity Overview

```text
Genome (genotype)
├── IReadOnlyList<NodeGene>
│   └── NodeGene (Id, NodeType, ActivationFunction)
└── IReadOnlyList<ConnectionGene>
    └── ConnectionGene (InnovationNumber, SourceNodeId, TargetNodeId, Weight, IsEnabled)

INetworkBuilder.Build(Genome) ──→ IGenome (FeedForwardNetwork)
                                   ├── Activate(inputs, outputs)
                                   ├── NodeCount
                                   └── ConnectionCount

IInnovationTracker
├── GetConnectionInnovation(source, target) → int
├── GetNodeSplitInnovation(connectionInnovation) → NodeSplitResult
└── NextGeneration()

IActivationFunctionRegistry
├── Get(name) → Func<double, double>
├── Register(name, function)
└── Contains(name)
```

---

## Core Data Types

### NodeType

Enum classifying node roles in a NEAT genome.

| Value | Description | FR |
|-------|-------------|-----|
| `Input` | Receives external input values | FR-001 |
| `Hidden` | Intermediate processing node | FR-001 |
| `Output` | Produces network output values | FR-001 |
| `Bias` | Always outputs 1.0 (learnable offset) | FR-001, Assumption |

**Namespace**: `NeatSharp.Genetics`

### NodeGene

Represents a single neuron in the genome. Immutable record.

| Field | Type | Default | Notes | FR |
|-------|------|---------|-------|-----|
| `Id` | `int` | (required) | Unique within a genome | FR-001 |
| `Type` | `NodeType` | (required) | Input, Hidden, Output, or Bias | FR-001 |
| `ActivationFunction` | `string` | `"sigmoid"` | Key into IActivationFunctionRegistry | FR-007, FR-009, Assumption |

**Namespace**: `NeatSharp.Genetics`
**Notes**: Sealed record provides value-based equality. Default activation is sigmoid per spec assumption. Serialization-ready (FR-012).

### ConnectionGene

Represents a directed weighted link between two nodes. Immutable record.

| Field | Type | Default | Notes | FR |
|-------|------|---------|-------|-----|
| `InnovationNumber` | `int` | (required) | Globally unique structural identifier | FR-002, FR-003 |
| `SourceNodeId` | `int` | (required) | Origin node ID | FR-002 |
| `TargetNodeId` | `int` | (required) | Destination node ID | FR-002 |
| `Weight` | `double` | (required) | Connection strength | FR-002 |
| `IsEnabled` | `bool` | (required) | Active/inactive flag | FR-002, FR-010 |

**Namespace**: `NeatSharp.Genetics`
**Notes**: Sealed record provides value-based equality. Disabled connections are excluded from phenotype evaluation (FR-010). Serialization-ready (FR-012).

### Genome

Complete neural network blueprint. Immutable after construction.

| Field | Type | Notes | FR |
|-------|------|-------|-----|
| `Nodes` | `IReadOnlyList<NodeGene>` | Defensively copied in constructor | FR-001 |
| `Connections` | `IReadOnlyList<ConnectionGene>` | Defensively copied in constructor | FR-001, FR-002 |
| `InputCount` | `int` | Computed: count of Input-type nodes | FR-011 |
| `OutputCount` | `int` | Computed: count of Output-type nodes | FR-011 |

**Namespace**: `NeatSharp.Genetics`
**Validation (constructor)**:
- All node IDs must be unique → `InvalidGenomeException`
- All connection source/target node IDs must exist in nodes → `InvalidGenomeException`
- At least one input node required → `InvalidGenomeException`
- At least one output node required → `InvalidGenomeException`

**Notes**: Sealed class (not record) to allow controlled constructor validation and defensive copying. Does NOT implement `IGenome` — the genotype is separate from the phenotype (see R-001). Serialization-ready (FR-012).

---

## Service Types

### IInnovationTracker / InnovationTracker

Assigns deterministic innovation IDs and node IDs for structural mutations.

| Member | Signature | Description | FR |
|--------|-----------|-------------|-----|
| `GetConnectionInnovation` | `int GetConnectionInnovation(int sourceNodeId, int targetNodeId)` | Get or assign innovation ID for a new connection | FR-003, FR-004 |
| `GetNodeSplitInnovation` | `NodeSplitResult GetNodeSplitInnovation(int connectionInnovation)` | Get or assign IDs for splitting a connection | FR-004 |
| `NextGeneration` | `void NextGeneration()` | Clear deduplication caches; advance generation | FR-016 |

**Namespace**: `NeatSharp.Genetics`
**DI Lifetime**: Scoped (one per evolution run)

**Internal state**:
- `_nextInnovationNumber` (`int`): monotonically increasing counter for innovation IDs
- `_nextNodeId` (`int`): monotonically increasing counter for node IDs
- `_connectionCache` (`Dictionary<(int, int), int>`): per-generation connection deduplication
- `_nodeSplitCache` (`Dictionary<int, NodeSplitResult>`): per-generation node-split deduplication

**Constructor**: `InnovationTracker(int startInnovationNumber = 0, int startNodeId = 0)` — initializes counters. Parameterless constructor defaults to 0 for both.

### NodeSplitResult

Return type for node-split innovation tracking. Value type (zero allocation).

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `NewNodeId` | `int` | ID for the new hidden node | FR-004 |
| `IncomingConnectionInnovation` | `int` | Innovation ID for source→newNode connection | FR-004 |
| `OutgoingConnectionInnovation` | `int` | Innovation ID for newNode→target connection | FR-004 |

**Namespace**: `NeatSharp.Genetics`
**Notes**: `readonly record struct` — value semantics, no heap allocation.

### IActivationFunctionRegistry / ActivationFunctionRegistry

Registry mapping names to activation function implementations.

| Member | Signature | Description | FR |
|--------|-----------|-------------|-----|
| `Get` | `Func<double, double> Get(string name)` | Retrieve function by name (case-insensitive) | FR-007 |
| `Register` | `void Register(string name, Func<double, double> function)` | Add custom function | FR-008 |
| `Contains` | `bool Contains(string name)` | Check if name is registered (case-insensitive) | FR-007 |

**Namespace**: `NeatSharp.Genetics`
**DI Lifetime**: Singleton
**Notes**: Case-insensitive name lookup (`StringComparer.OrdinalIgnoreCase`). Pre-populated with 5 built-in functions. `Get` throws `ArgumentException` if name not found. `Register` throws `ArgumentException` if name already exists.

### ActivationFunctions

Static class with built-in activation function name constants and implementations.

| Member | Type | Value/Formula | FR |
|--------|------|---------------|-----|
| `Sigmoid` (const) | `string` | `"sigmoid"` | FR-007 |
| `Tanh` (const) | `string` | `"tanh"` | FR-007 |
| `ReLU` (const) | `string` | `"relu"` | FR-007 |
| `Step` (const) | `string` | `"step"` | FR-007 |
| `Identity` (const) | `string` | `"identity"` | FR-007 |
| `SigmoidFunction` | `static double(double x)` | `1.0 / (1.0 + Math.Exp(-x))` | FR-007 |
| `TanhFunction` | `static double(double x)` | `Math.Tanh(x)` | FR-007 |
| `ReLUFunction` | `static double(double x)` | `Math.Max(0.0, x)` | FR-007 |
| `StepFunction` | `static double(double x)` | `x > 0.0 ? 1.0 : 0.0` | FR-007 |
| `IdentityFunction` | `static double(double x)` | `x` | FR-007 |

**Namespace**: `NeatSharp.Genetics`
**Notes**: Constants prevent magic strings when constructing `NodeGene` instances. Functions are used by `ActivationFunctionRegistry` to populate built-ins.

### INetworkBuilder / FeedForwardNetworkBuilder

Converts genotype (Genome) to phenotype (IGenome).

| Member | Signature | Description | FR |
|--------|-----------|-------------|-----|
| `Build` | `IGenome Build(Genome genome)` | Convert genome to executable network | FR-005 |

**Namespace**: `NeatSharp.Genetics`
**DI Lifetime**: Singleton
**Dependencies**: `IActivationFunctionRegistry` (constructor injection)

**Build algorithm**:
1. Filter connections to enabled only (FR-010)
2. Build adjacency graph from enabled connections
3. Compute reachable nodes: forward BFS from inputs/bias ∩ backward BFS from outputs
4. Topological sort via Kahn's algorithm on reachable subgraph
5. If unprocessed nodes remain → throw `CycleDetectedException` (FR-009)
6. Pre-compute evaluation order and connection mappings
7. Return FeedForwardNetwork (internal class implementing IGenome)

### FeedForwardNetwork (internal)

Executable feed-forward neural network. Implements `IGenome`.

| Member | Signature | Description | FR |
|--------|-----------|-------------|-----|
| `NodeCount` | `int { get; }` | Reachable node count | Spec 001 IGenome |
| `ConnectionCount` | `int { get; }` | Enabled, reachable connection count | Spec 001 IGenome |
| `Activate` | `void Activate(ReadOnlySpan<double>, Span<double>)` | Feed-forward inference | FR-005, FR-006 |

**Namespace**: `NeatSharp.Genetics`
**Visibility**: `internal sealed`

**Activation algorithm**:
1. Validate input span length matches `InputCount` → throw `InputDimensionMismatchException` if mismatch (FR-011)
2. Validate output span length matches `OutputCount` → throw `ArgumentException` if mismatch
3. Set input node activations from `ReadOnlySpan<double> inputs`
4. Set bias node activations to `1.0`
5. For each node in topological order (hidden, then output):
   - Compute weighted sum: `Σ(source_activation × weight)` over incoming enabled connections
   - Apply activation function from registry
   - Store result in pre-allocated buffer
6. Copy output node activations to `Span<double> outputs`

**Notes**: Pre-allocated `double[]` activation buffer, reused across activations. NOT thread-safe (shared mutable buffer). Deterministic (FR-014).

---

## Exceptions

### CycleDetectedException

Thrown when phenotype construction encounters a cycle in the genome's enabled connection topology.

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `Message` | `string` | Describes the cycle detection | FR-009, FR-015 |

**Namespace**: `NeatSharp.Exceptions`
**Base class**: `NeatSharpException`

### InputDimensionMismatchException

Thrown when activation input count doesn't match the network's expected input count.

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `Message` | `string` | Expected vs actual counts | FR-011, FR-015 |
| `Expected` | `int` | Expected number of inputs | FR-011 |
| `Actual` | `int` | Provided number of inputs | FR-011 |

**Namespace**: `NeatSharp.Exceptions`
**Base class**: `NeatSharpException`

### InvalidGenomeException

Thrown when genome construction encounters structural integrity violations.

| Field | Type | Description | FR |
|-------|------|-------------|-----|
| `Message` | `string` | Describes the structural violation | FR-015, Edge case |

**Namespace**: `NeatSharp.Exceptions`
**Base class**: `NeatSharpException`

---

## Relationship Summary

```text
Genome           1──* NodeGene
Genome           1──* ConnectionGene
NodeGene         *──1 NodeType (enum value)
NodeGene         *──1 ActivationFunctions (name reference → registry lookup)
ConnectionGene   *──1 NodeGene (SourceNodeId foreign key)
ConnectionGene   *──1 NodeGene (TargetNodeId foreign key)
INetworkBuilder  →   Genome → IGenome (FeedForwardNetwork)
INetworkBuilder  →   IActivationFunctionRegistry (dependency)
InnovationTracker →  NodeSplitResult (returned value)
CycleDetectedException       ──▷ NeatSharpException
InputDimensionMismatchException ──▷ NeatSharpException
InvalidGenomeException       ──▷ NeatSharpException
```

All data types (NodeGene, ConnectionGene, Genome, NodeSplitResult) are immutable. Service types (InnovationTracker) maintain state per evolution run (scoped lifetime). ActivationFunctionRegistry is populated at startup and read-only during evolution (singleton lifetime). FeedForwardNetwork maintains a mutable activation buffer but its topology is immutable after construction.
