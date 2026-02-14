# Feature Specification: Genome Model + Innovation Tracking + Feed-Forward Phenotype

**Feature Branch**: `002-genome-phenotype`
**Created**: 2026-02-13
**Status**: Draft
**Input**: User description: "Genome Model + Innovation Tracking + Feed-Forward Phenotype — NEAT correctness depends on stable genome representation, historical markings (innovation numbers), and deterministic phenotype construction."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Build and Run a Genome (Priority: P1)

A library consumer constructs a genome by defining node genes (input, hidden, output, bias) and connection genes (with weights, enabled flags, and innovation identifiers), then converts that genome into an executable feed-forward network and runs inference on a set of numeric inputs to obtain outputs.

**Why this priority**: This is the foundational capability of the feature. Without the ability to represent a genome and execute it, no other functionality (innovation tracking, activation extensibility, validation) has value. Every downstream NEAT operation — mutation, crossover, fitness evaluation — depends on a working genome that can be activated.

**Independent Test**: Can be fully tested by constructing a minimal genome (e.g., 2 inputs, 1 output, direct connections with known weights), building a phenotype, passing known inputs, and verifying the outputs match hand-calculated expected values within a small epsilon.

**Acceptance Scenarios**:

1. **Given** a genome with 2 input nodes, 1 bias node, 1 output node, and 3 enabled connections with known weights, **When** the genome is converted to a feed-forward network and activated with inputs [1.0, 0.5], **Then** the output matches the expected hand-calculated value within epsilon (1e-10).
2. **Given** a genome with input, hidden, and output nodes forming a valid feed-forward topology, **When** the genome is converted and activated, **Then** signals propagate through hidden layers in correct topological order and produce the expected output.
3. **Given** a genome with a connection whose enabled flag is set to false, **When** the genome is converted to a feed-forward network, **Then** the disabled connection does not contribute to the output.

---

### User Story 2 - Deterministic Innovation Tracking (Priority: P2)

A library consumer (or the internal evolution engine) requests innovation identifiers for new structural mutations (adding a node or adding a connection). The innovation tracker assigns identifiers deterministically so that identical structural changes occurring independently in the same generation receive the same innovation identifier, enabling correct crossover alignment in later NEAT phases.

**Why this priority**: Innovation numbers are the mechanism that makes NEAT's crossover operator work correctly. Without stable, deterministic innovation IDs, genomes cannot be aligned during crossover, breaking the core NEAT algorithm. This is the second priority because it is required for algorithm correctness but not for basic genome execution.

**Independent Test**: Can be fully tested by requesting innovation IDs for the same structural change multiple times within a generation and verifying the same ID is returned, then advancing the generation and verifying that new structural changes receive new IDs.

**Acceptance Scenarios**:

1. **Given** a fresh innovation tracker, **When** a new connection between node A and node B is registered, **Then** a unique innovation identifier is assigned and returned.
2. **Given** an innovation tracker where a connection between node A and node B was already registered in the current generation, **When** the same structural change (A to B) is requested again, **Then** the same innovation identifier is returned (deterministic deduplication).
3. **Given** an innovation tracker with existing records, **When** `NextGeneration()` is called and a novel structural change is registered, **Then** a new, higher innovation identifier is assigned and the previous generation's deduplication cache is cleared.
4. **Given** two independent callers requesting innovation IDs for adding a node that splits the same connection in the same generation, **When** both requests are processed, **Then** both receive identical innovation identifiers for the resulting sub-connections and identical node IDs for the new hidden node.

---

### User Story 3 - Extensible Activation Functions (Priority: P3)

A library consumer selects from a set of built-in activation functions (e.g., sigmoid, tanh, ReLU, step, identity) for nodes in their genome, or registers a custom activation function with the activation registry to support domain-specific needs.

**Why this priority**: Activation function extensibility is important for flexibility but has a sensible default (sigmoid). A working system can exist with a single hard-coded activation, making this lower priority than genome execution and innovation tracking. However, it is still part of core phenotype construction.

**Independent Test**: Can be fully tested by constructing a genome whose hidden node uses a specific activation function (e.g., tanh), building the phenotype, running inference, and verifying that the output reflects the chosen activation function's transformation rather than the default.

**Acceptance Scenarios**:

1. **Given** a genome with a hidden node configured to use the tanh activation, **When** the phenotype is built and activated, **Then** the hidden node applies tanh to its weighted sum before passing the signal onward.
2. **Given** a custom activation function registered in the activation registry, **When** a genome references that custom activation for a node, **Then** the phenotype correctly applies the custom function during inference.
3. **Given** a genome that does not specify an activation function for a node, **When** the phenotype is built, **Then** a sensible default activation (sigmoid) is applied.

---

### User Story 4 - Feed-Forward Graph Validation (Priority: P4)

A library consumer or the internal system attempts to convert a genome into a feed-forward phenotype. If the genome's connection topology contains cycles (which are invalid for feed-forward evaluation), the system detects and reports this clearly rather than producing incorrect results or hanging.

**Why this priority**: Validation prevents silent correctness bugs. While a well-formed NEAT process should not produce cycles in feed-forward mode, validation provides a safety net during development, testing, and when consumers construct genomes manually.

**Independent Test**: Can be fully tested by constructing a genome with a deliberate cycle (e.g., node A connects to node B, node B connects back to node A) and verifying that phenotype construction produces a clear error.

**Acceptance Scenarios**:

1. **Given** a genome whose connections form a cycle, **When** phenotype construction is attempted, **Then** the system reports an error indicating the genome contains a cycle and cannot be evaluated as feed-forward.
2. **Given** a genome with a valid acyclic topology, **When** phenotype construction is attempted, **Then** the phenotype is built successfully with no errors.

---

### User Story 5 - Champion Network Inference (Priority: P5)

After an evolution run completes, a library consumer retrieves the champion genome from the results and executes inference on new inputs. The champion network operates identically to any other phenotype — the consumer can activate it repeatedly with different inputs for real-time use or batch evaluation.

**Why this priority**: This is the ultimate consumer-facing use case — using the result of evolution. It depends on all prior stories being functional and is essentially a composition of Story 1 applied to the evolution result's champion genome.

**Independent Test**: Can be fully tested by constructing a known genome (simulating a champion), building its phenotype, and running inference across multiple input sets, verifying outputs are consistent and correct.

**Acceptance Scenarios**:

1. **Given** a champion genome obtained from evolution results, **When** the consumer builds a phenotype and activates it with a set of inputs, **Then** the outputs are identical to what the same genome would produce if constructed manually with the same topology and weights.
2. **Given** a champion phenotype, **When** it is activated multiple times with the same inputs, **Then** the outputs are identical each time (deterministic execution).

---

### Edge Cases

- What happens when a genome has zero connections? The phenotype should produce output values equal to `activation(0.0)` for each output node (e.g., sigmoid(0.0) ≈ 0.5 with the default activation), since no signal propagates and output nodes still apply their activation function to a zero weighted sum.
- What happens when a genome has only disabled connections? The phenotype should behave identically to a genome with zero connections.
- What happens when a genome references a node ID that does not exist in the node gene list? The system should throw a specific validation exception (e.g., `InvalidGenomeException`).
- What happens when a consumer provides inputs of the wrong dimension (count mismatch with input nodes)? The system should throw a specific exception (e.g., `InputDimensionMismatchException`).
- What happens when the innovation tracker is queried for the same structural change across different generations? Calling `NextGeneration()` clears the deduplication cache; the same structural change in a new generation receives a new innovation ID unless it was already tracked in that generation.
- What happens when a genome has multiple disconnected subgraphs (not all nodes are reachable from inputs)? Unreachable nodes should not affect output; the phenotype only evaluates nodes on paths from inputs to outputs.

## Clarifications

### Session 2026-02-13

- Q: How should the public API report errors (cycle detection, input dimension mismatch, invalid node reference)? → A: Throw specific exception types (e.g., `CycleDetectedException`, `InputDimensionMismatchException`, `InvalidGenomeException`).
- Q: Should Genome be immutable or mutable after construction? → A: Immutable — all properties read-only after construction; mutations (future specs) produce new genome instances.
- Q: Should the innovation tracker assign deterministic node IDs (not just connection innovation IDs) during node-split mutations? → A: Yes — the tracker assigns deterministic IDs for both the new hidden node and the two new connections, ensuring independent splits of the same connection produce identical structure.
- Q: How should the innovation tracker transition between generations? → A: Explicit method call — consumer or evolution engine calls `NextGeneration()` to clear the current generation's deduplication cache and advance.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST represent genomes as an immutable collection of node genes, where each node gene has a unique identifier and a type (input, hidden, output, or bias). Genomes MUST be fully read-only after construction; any mutation operation (future specs) MUST produce a new genome instance.
- **FR-002**: System MUST represent connections as connection genes, where each connection gene references a source node, a target node, a numeric weight, an enabled/disabled flag, and an innovation identifier.
- **FR-003**: System MUST provide an innovation tracking service that assigns globally unique, monotonically increasing innovation identifiers to new structural changes.
- **FR-004**: Innovation tracking MUST be deterministic: the same structural change (same source node and target node for a new connection, or same connection being split for a new node) registered within the same generation MUST receive the same innovation identifier. For node-split mutations, the tracker MUST also assign a deterministic node ID for the new hidden node, ensuring independent splits of the same connection produce structurally identical results.
- **FR-016**: The innovation tracker MUST expose an explicit `NextGeneration()` method that clears the current generation's deduplication cache while preserving the global ID counter, enabling the caller to control generation boundaries.
- **FR-005**: System MUST convert a genome into an executable feed-forward network (phenotype) that evaluates nodes in topological order from inputs to outputs.
- **FR-006**: System MUST support activation of the phenotype by accepting a set of numeric inputs and producing a set of numeric outputs.
- **FR-007**: System MUST provide a registry of activation functions with built-in support for at minimum: sigmoid, tanh, ReLU, step, and identity.
- **FR-008**: System MUST allow consumers to register custom activation functions in the activation registry.
- **FR-017**: Retrieving an unregistered activation function name from the registry MUST throw `ArgumentException` with a message that includes the requested name and lists the available registered function names, enabling actionable diagnosis.
- **FR-009**: System MUST detect cycles in a genome's connection topology during phenotype construction and throw a specific exception (e.g., `CycleDetectedException`) when a cycle is found.
- **FR-010**: Disabled connections MUST be excluded from phenotype evaluation — they must not contribute to signal propagation.
- **FR-011**: System MUST validate that inputs provided to a phenotype match the expected number of input nodes, throwing a specific exception (e.g., `InputDimensionMismatchException`) on mismatch.
- **FR-018**: System MUST validate that the output span provided to phenotype activation matches the expected number of output nodes, throwing `ArgumentException` on mismatch.
- **FR-015**: All validation and error conditions in the public API MUST be reported by throwing specific, descriptive exception types (not generic exceptions or result types). Each distinct error category MUST have its own exception type to enable precise consumer handling. Exception types for invalid arguments (e.g., `InputDimensionMismatchException`) MUST extend `ArgumentException` per the constitution's error handling rules, while other library error conditions (e.g., `CycleDetectedException`, `InvalidGenomeException`) MUST extend `NeatSharpException`.
- **FR-012**: System MUST provide serialization-ready data transfer objects for genomes, node genes, and connection genes to support future persistence (actual serialization implementation deferred to Spec 05). "Serialization-ready" means all public data types (NodeGene, ConnectionGene, Genome) MUST round-trip through `System.Text.Json` serialization/deserialization with default `JsonSerializerOptions` — all public properties preserved, immutable collections deserialized correctly, no custom converters required.
- **FR-013**: System MUST allow a champion genome (obtained from evolution results) to be converted to a phenotype and activated for inference identically to any other genome.
- **FR-014**: Phenotype execution MUST be deterministic — the same genome activated with the same inputs MUST always produce the same outputs.

### Key Entities

- **Node Gene**: Represents a single neuron in the network. Identified by a unique ID. Has a type (input, hidden, output, bias) and an associated activation function.
- **Connection Gene**: Represents a directed link between two nodes. Carries a numeric weight, an enabled/disabled flag, and an innovation identifier that tracks its structural origin.
- **Genome**: A complete neural network blueprint composed of an immutable, ordered collection of node genes and connection genes. Fully read-only after construction — mutation operations produce new genome instances. Serves as the genotype in the NEAT algorithm.
- **Innovation Tracker**: A service that maintains a ledger of structural changes (new connections, node splits) and assigns deterministic innovation identifiers — including deterministic node IDs for new hidden nodes created by node-split mutations. Operates per-generation to ensure identical mutations receive identical IDs.
- **Phenotype (Feed-Forward Network)**: An executable representation of a genome that evaluates signals from input nodes through hidden nodes to output nodes in topological order. Constructed from a genome and used for inference.
- **Activation Function**: A mathematical function applied to a node's weighted input sum to produce its output value. Common examples include sigmoid, tanh, and ReLU. Stored in a registry that supports both built-in and custom functions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A minimal genome (2 inputs, 1 output, direct connections) can be constructed, converted to a phenotype, and activated to produce correct outputs — verified against hand-calculated expected values within epsilon (1e-10).
- **SC-002**: Innovation identifiers assigned for the same structural change within a single generation are identical in 100% of test cases, confirming deterministic behavior.
- **SC-003**: A genome with hidden layers and multiple activation functions produces correct outputs when compared against independently calculated expected values (within epsilon).
- **SC-004**: Phenotype construction rejects 100% of cyclic genome topologies with a clear, descriptive error.
- **SC-005**: All genome-related data structures are serialization-ready — they can be round-tripped through serialization/deserialization without data loss (actual persistence is Spec 05; this confirms structural readiness).
- **SC-006**: A champion genome can be loaded and used for repeated inference, producing identical outputs for identical inputs across 1,000 consecutive activations.
- **SC-007**: Built-in activation functions (sigmoid, tanh, ReLU, step, identity) are available out of the box, and at least one custom activation function can be registered and used successfully.

## Assumptions

- **Bias node behavior**: Bias nodes always output a constant value of 1.0, providing a learnable offset when connected to other nodes.
- **Default activation function**: When no activation function is specified for a node, sigmoid is used as the default (industry standard for NEAT).
- **Output node activation**: Output nodes also apply an activation function (sigmoid by default), consistent with standard NEAT implementations.
- **Input and bias node activation**: Activation functions are applied only to hidden and output nodes during phenotype evaluation. The `ActivationFunction` property on input and bias node genes is accepted at construction time but ignored during evaluation — input nodes pass through their provided values directly, and bias nodes always output a constant 1.0.
- **Innovation tracker scope**: The innovation tracker operates within the context of a single evolution run. Cross-run innovation ID continuity is not required for this spec.
- **Feed-forward only**: This spec covers only feed-forward (acyclic) network topologies. Recurrent network support is explicitly excluded.
- **Thread safety**: The innovation tracker is assumed to operate in a single-threaded context within a generation. Thread-safe concurrent access is not required for this spec.

## Non-Goals

- Speciation, mutation, crossover, and selection algorithms (covered in separate specs).
- GPU/CUDA acceleration for phenotype evaluation.
- Persistent serialization to disk or database (Spec 05).
- Recurrent (cyclic) network evaluation.
- Multi-threaded or concurrent innovation tracking.
