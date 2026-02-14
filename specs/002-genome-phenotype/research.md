# Research: Genome Model + Innovation Tracking + Feed-Forward Phenotype

**Feature**: 002-genome-phenotype
**Date**: 2026-02-13

## R-001: Genotype vs Phenotype Separation (IGenome Compatibility)

**Decision**: `Genome` is the immutable genotype data class (does NOT implement `IGenome`). `FeedForwardNetwork` is the phenotype and implements `IGenome`. A builder (`INetworkBuilder`) converts genotype to phenotype.

**Rationale**: The existing `IGenome` interface (from Spec 001) combines data access (`NodeCount`, `ConnectionCount`) with activation (`Activate`). In NEAT, the genotype (genome data) and phenotype (executable network) are distinct concepts. The evolution engine internally works with `Genome` objects for mutation/crossover, then converts to `FeedForwardNetwork` (IGenome) for fitness evaluation. This separation follows SRP (Principle VI) and enables different phenotype implementations in the future (e.g., recurrent networks).

**Alternatives considered**:
- Make `Genome` implement `IGenome` directly, with lazy phenotype construction inside `Activate` — rejected: violates SRP (genome would be both data container and execution engine), makes mutation harder (immutable data + mutable cached state), and couples genotype to a specific evaluation strategy.
- Have `FeedForwardNetwork` wrap `Genome` and expose genotype data — rejected: leaks internal representation through IGenome; consumers of IGenome don't need genotype details (they just call Activate).

**Key details**:
- `Champion.Genome` (type `IGenome`) holds the phenotype. The evolution engine (future spec) will separately track the genotype data for serialization and reproduction.
- `FeedForwardNetwork.NodeCount` returns the number of reachable nodes in the evaluation graph. `FeedForwardNetwork.ConnectionCount` returns the number of enabled connections in the evaluation graph.
- For manual testing and construction, consumers create a `Genome` and use `INetworkBuilder.Build(genome)` to get an `IGenome`.

---

## R-002: Genome Immutability Pattern

**Decision**: `NodeGene` and `ConnectionGene` are sealed C# records. `Genome` is a sealed class with `IReadOnlyList<T>` properties, defensively copying input collections in the constructor.

**Rationale**: FR-001 mandates immutability — all properties read-only after construction. Records provide value-based equality (useful for testing and comparison). `Genome` as a sealed class (not a record) allows controlled constructor validation and defensive copying without the complexity of with-expression semantics on collections.

**Alternatives considered**:
- `Genome` as a record — rejected: records with collection properties have surprising equality semantics (reference equality on collections, not structural equality). A class with explicit constructor validation is clearer.
- `ImmutableArray<T>` for collections — rejected: introduces `System.Collections.Immutable` dependency (new NuGet package). `IReadOnlyList<T>` backed by an internal array copy is sufficient and dependency-free.
- Make `Genome` a readonly struct — rejected: large struct (two arrays) would cause copying overhead; reference semantics are appropriate.

**Key details**:
- `NodeGene`: `public sealed record NodeGene(int Id, NodeType Type, string ActivationFunction = "sigmoid")`
- `ConnectionGene`: `public sealed record ConnectionGene(int InnovationNumber, int SourceNodeId, int TargetNodeId, double Weight, bool IsEnabled)`
- `Genome` constructor: validates all node IDs are unique, validates all connection source/target nodes exist in the node list, requires at least one input node and one output node. Defensively copies both collections.
- All three types are serialization-ready (public properties, parameterized constructors) for FR-012.

---

## R-003: Topological Sort for Feed-Forward Evaluation

**Decision**: Use Kahn's algorithm for topological sort during phenotype construction. Pre-compute evaluation order at build time; reuse during activation.

**Rationale**: Kahn's algorithm detects cycles naturally (if not all nodes are processed, a cycle exists), satisfying FR-009. It runs in O(V+E) time. Pre-computing the evaluation order at build time means activation is a simple linear pass through the sorted nodes — no graph traversal during hot-path inference.

**Alternatives considered**:
- DFS-based topological sort — rejected: requires separate cycle detection pass or three-color scheme; Kahn's handles both in one pass.
- Sort at activation time — rejected: wasteful; the topology doesn't change between activations.
- Tarjan's strongly connected components — rejected: designed for SCC detection, more complex than needed; Kahn's is simpler for topological sort + cycle detection.

**Key details**:
- Build adjacency list from enabled connections only (disabled connections excluded per FR-010).
- Kahn's: initialize in-degree counts; enqueue nodes with in-degree 0 (input and bias nodes). Process queue, decrementing in-degree of successors. If unprocessed nodes remain after queue drains → cycle → throw `CycleDetectedException`.
- After topological sort, prune unreachable nodes: only include nodes on paths from input/bias nodes to output nodes.
- Store evaluation order as an array for cache-friendly linear iteration during activation.

---

## R-004: Cycle Detection and Reachability Analysis

**Decision**: Cycle detection via Kahn's algorithm (see R-003). Reachability analysis via forward BFS from inputs + backward BFS from outputs; intersection gives the set of evaluable nodes.

**Rationale**: Kahn's algorithm naturally detects cycles. Reachability analysis ensures unreachable nodes don't appear in the phenotype (spec edge case: "Unreachable nodes should not affect output; the phenotype only evaluates nodes on paths from inputs to outputs").

**Alternatives considered**:
- Skip reachability analysis, include all nodes — rejected: unreachable hidden nodes would waste computation and inflate NodeCount.
- Use DFS for reachability — viable but BFS is equivalent in complexity and is slightly more natural for a "signal propagation" mental model.

**Key details**:
- Forward reachability: BFS from all input + bias nodes following enabled connections → set F.
- Backward reachability: BFS from all output nodes following enabled connections in reverse direction → set B.
- Reachable nodes = F ∩ B. Input, bias, and output nodes are always included in their respective reachability sets.
- Topological sort operates on the reachable subgraph only.
- Zero-connection genome: output nodes are reachable but have no incoming signals → weighted sum is 0.0 → output is `activation(0.0)` (e.g., sigmoid(0) = 0.5 for default sigmoid activation). The spec edge case says "default output values (zero)" which applies with identity activation; with sigmoid the mathematically correct result is 0.5.

---

## R-005: Innovation Tracking Data Structures

**Decision**: Innovation tracker uses two `Dictionary<TKey, TValue>` caches (one for connections, one for node splits) that are cleared each generation. Monotonically increasing counters for innovation IDs and node IDs, tracked separately.

**Rationale**: FR-003 requires globally unique, monotonically increasing innovation IDs. FR-004 requires deterministic deduplication per generation. Dictionary lookup provides O(1) amortized deduplication. Clearing caches per generation (FR-016, `NextGeneration()`) ensures cross-generation mutations get fresh IDs while same-generation identical mutations are deduplicated.

**Alternatives considered**:
- ConcurrentDictionary — rejected: spec explicitly states single-threaded (Assumptions section). Standard Dictionary is faster and simpler.
- Persistent ledger (never clear) — rejected: FR-016 explicitly requires generation-level cache clearing. Cross-generation deduplication is not required per spec.
- Global static tracker — rejected: violates DI practices; must be injectable and scoped per evolution run.

**Key details**:
- Connection cache key: `(int sourceNodeId, int targetNodeId)` → value: `int innovationNumber`.
- Node split cache key: `int connectionInnovationNumber` → value: `NodeSplitResult(int newNodeId, int incomingInnovation, int outgoingInnovation)`.
- `NextGeneration()`: clears both caches, preserves counters.
- Constructor accepts `(int startInnovationNumber = 0, int startNodeId = 0)` for initialization. Parameterless constructor defaults to 0 for both.
- `NodeSplitResult` is a `readonly record struct` (value type, zero allocation).
- Innovation IDs and node IDs use separate counters since they represent different concepts in NEAT.

---

## R-006: Activation Function Registry Design

**Decision**: `IActivationFunctionRegistry` interface with a dictionary-backed implementation. Five built-in functions defined in a static `ActivationFunctions` class. Registry pre-populated with built-ins during construction.

**Rationale**: FR-007 requires built-in support for sigmoid, tanh, ReLU, step, identity. FR-008 requires consumer-extensible registration. A dictionary keyed by string name is simple and sufficient. String keys enable serialization (genomes store function names, not delegates).

**Alternatives considered**:
- Enum-based activation functions — rejected: not extensible for custom functions (FR-008). Adding a custom function would require modifying the enum.
- Delegate-only (no registry, pass functions directly) — rejected: genomes reference activations by name (for serialization and reproducibility); a registry provides the name-to-function mapping.
- Interface per activation function (e.g., `ISigmoid : IActivationFunction`) — rejected: over-engineered for a simple `double → double` mapping; delegates are sufficient.

**Key details**:
- Built-in functions: `sigmoid(x) = 1/(1+e^(-x))`, `tanh(x) = Math.Tanh(x)`, `relu(x) = max(0, x)`, `step(x) = x > 0 ? 1 : 0`, `identity(x) = x`.
- Function signature: `Func<double, double>`.
- Registry keys are case-insensitive strings (using `StringComparer.OrdinalIgnoreCase`) to prevent "Sigmoid" vs "sigmoid" lookup failures.
- `Register` throws `ArgumentException` if name is already registered (prevents silent overwrites of built-ins).
- `Get` throws `ArgumentException` if name is not found (with actionable message listing available functions).
- Registry is singleton (populated at DI configuration time, read-only during evolution).

---

## R-007: Feed-Forward Network Activation Performance

**Decision**: `FeedForwardNetwork` pre-allocates a single `double[]` activation buffer. Activation writes directly into the buffer in topological order. No allocations during `Activate()`.

**Rationale**: FR-014 requires deterministic execution. Constitution Principle III demands performance awareness. Phenotype activation is the hottest path in NEAT (called once per genome per generation). Pre-allocation and buffer reuse eliminate GC pressure.

**Alternatives considered**:
- `stackalloc` activation buffer — rejected: genome size varies; stack overflow risk for large genomes. Cannot use stackalloc in a reusable method without size bounds.
- Allocate new array each activation — rejected: GC pressure in the hot path; unnecessary since buffer can be reused across activations.
- `ArrayPool<double>` rental — rejected: adds complexity (rent/return lifecycle); a single pre-allocated array per network instance is simpler and equally performant.

**Key details**:
- Activation buffer size = total number of reachable nodes.
- Input nodes: buffer values set from `ReadOnlySpan<double> inputs`.
- Bias nodes: buffer values set to 1.0.
- Hidden/output nodes: computed in topological order as `activation_function(Σ(source_activation × weight))`.
- Output values: copied from buffer to `Span<double> outputs`.
- Buffer is reused across activations (overwritten each time).
- `FeedForwardNetwork` is NOT thread-safe due to the shared mutable buffer. Thread safety is out of scope (Assumptions section). Each genome gets its own network instance.

---

## R-008: Exception Hierarchy Design

**Decision**: Three specific exception types, all deriving from `NeatSharpException`: `CycleDetectedException`, `InputDimensionMismatchException`, `InvalidGenomeException`.

**Rationale**: FR-015 mandates specific, descriptive exception types for each error category. The constitution requires custom exceptions rooted at `NeatSharpException`. Specific types enable precise `catch` blocks in consumer code.

**Alternatives considered**:
- Result types (e.g., `Result<IGenome, BuildError>`) — rejected: spec and constitution explicitly require exception-based error reporting for validation errors. Result types are reserved for "expected failure paths" (constitution Error Handling section); invalid genomes and cycles are validation errors, not expected runtime conditions.
- Single `GenomeException` with an error code enum — rejected: FR-015 says "each distinct error category MUST have its own exception type."

**Key details**:
- `CycleDetectedException`: thrown by `INetworkBuilder.Build()` when a cycle is detected in the enabled connection topology. Message includes the genome's context.
- `InputDimensionMismatchException`: thrown by `FeedForwardNetwork.Activate()` when input span length doesn't match. Carries `Expected` and `Actual` properties for actionable diagnostics. Output buffer mismatch throws `ArgumentException` (consistent with existing `IGenome` interface contract).
- `InvalidGenomeException`: thrown by `Genome` constructor when structural integrity is violated (duplicate node IDs, dangling connection references, missing input/output nodes). Message includes specific violation details.
- All three have dual constructors: `(string message)` and `(string message, Exception innerException)`.
- `InputDimensionMismatchException` additionally has `(int expected, int actual)` constructor that auto-generates the message.

---

## R-009: DI Service Registration Updates

**Decision**: Extend `AddNeatSharp()` to register `IActivationFunctionRegistry` (singleton), `IInnovationTracker` (scoped), and `INetworkBuilder` (singleton). No separate `AddNeatSharpGenetics()` method.

**Rationale**: Constitution DI Practices require all services registered via `AddNeatSharp()`. Service lifetimes follow the constitution: singleton for stateless services and caches, scoped for per-run services.

**Alternatives considered**:
- Separate `AddNeatSharpGenetics()` extension — rejected: fragments the registration; a single `AddNeatSharp()` call should configure everything.
- Register InnovationTracker as singleton — rejected: innovation tracking state is per-evolution-run (caches are per-generation within a run); scoped is correct.
- Defer DI registration to evolution spec — rejected: consumers should be able to resolve INetworkBuilder and IActivationFunctionRegistry independently of the evolution engine.

**Key details**:
- `IActivationFunctionRegistry` → singleton (`ActivationFunctionRegistry`). Pre-populated with 5 built-in functions. Consumer can call `Register()` after `AddNeatSharp()` but before `BuildServiceProvider()`.
- `IInnovationTracker` → scoped (`InnovationTracker`). Parameterless constructor (starts at 0, 0). Evolution engine (future spec) may reconfigure with a factory that provides proper starting values.
- `INetworkBuilder` → singleton (`FeedForwardNetworkBuilder`). Depends on `IActivationFunctionRegistry` via constructor injection.
- Existing registrations (IRunReporter, INeatEvolver, NeatSharpOptions) are unchanged.
