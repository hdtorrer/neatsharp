# Tasks: Genome Model + Innovation Tracking + Feed-Forward Phenotype

**Input**: Design documents from `/specs/002-genome-phenotype/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included per TDD mandate (Constitution Principle VII). Red-Green-Refactor: write tests FIRST, verify they FAIL, then implement.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: No new project or dependency setup required. This feature extends the existing NeatSharp codebase from Spec 001 with new types in existing namespaces (`NeatSharp.Genetics`, `NeatSharp.Exceptions`). All dependencies (Microsoft.Extensions.DI, Options, Logging) are already present.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core data types, exception types, and the Genome class that ALL user stories depend on. These are simple immutable records, enums, and exception classes with no business logic beyond constructor validation.

**CRITICAL**: No user story work can begin until this phase is complete.

### Tests for Foundational Types

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T001 [P] Write NodeGene record tests in tests/NeatSharp.Tests/Genetics/NodeGeneTests.cs — test construction with all parameters, default ActivationFunction ("sigmoid"), value-based equality between identical records, and inequality when any property differs. Reference contract: specs/002-genome-phenotype/contracts/NodeGene.cs
- [X] T002 [P] Write ConnectionGene record tests in tests/NeatSharp.Tests/Genetics/ConnectionGeneTests.cs — test construction with all five parameters (InnovationNumber, SourceNodeId, TargetNodeId, Weight, IsEnabled), value-based equality, and inequality. Reference contract: specs/002-genome-phenotype/contracts/ConnectionGene.cs
- [X] T003 [P] Write Genome validation tests in tests/NeatSharp.Tests/Genetics/GenomeTests.cs — test: (a) valid construction with mixed node types, (b) InputCount/OutputCount computed properties, (c) defensive copying (mutating original list doesn't affect genome), (d) InvalidGenomeException for duplicate node IDs, (e) InvalidGenomeException for connection referencing nonexistent node, (f) InvalidGenomeException for missing input nodes, (g) InvalidGenomeException for missing output nodes. Reference contract: specs/002-genome-phenotype/contracts/Genome.cs
- [X] T004 [P] Write CycleDetectedException tests in tests/NeatSharp.Tests/Exceptions/CycleDetectedExceptionTests.cs — test both constructors: (string message) and (string message, Exception innerException). Verify message is preserved and base type is NeatSharpException. Reference contract: specs/002-genome-phenotype/contracts/CycleDetectedException.cs
- [X] T005 [P] Write InputDimensionMismatchException tests in tests/NeatSharp.Tests/Exceptions/InputDimensionMismatchExceptionTests.cs — test three constructors: (int expected, int actual) with auto-generated message, (string message), (string message, Exception innerException). Verify Expected and Actual properties. Verify base type is ArgumentException (not NeatSharpException). Reference contract: specs/002-genome-phenotype/contracts/InputDimensionMismatchException.cs
- [X] T006 [P] Write InvalidGenomeException tests in tests/NeatSharp.Tests/Exceptions/InvalidGenomeExceptionTests.cs — test both constructors: (string message) and (string message, Exception innerException). Verify message and base type. Reference contract: specs/002-genome-phenotype/contracts/InvalidGenomeException.cs

### Implementation for Foundational Types

- [X] T007 [P] Implement NodeType enum (Input, Hidden, Output, Bias) in src/NeatSharp/Genetics/NodeType.cs per contract specs/002-genome-phenotype/contracts/NodeType.cs
- [X] T008 [P] Implement NodeGene sealed record (Id, Type, ActivationFunction = "sigmoid") in src/NeatSharp/Genetics/NodeGene.cs per contract specs/002-genome-phenotype/contracts/NodeGene.cs
- [X] T009 [P] Implement ConnectionGene sealed record (InnovationNumber, SourceNodeId, TargetNodeId, Weight, IsEnabled) in src/NeatSharp/Genetics/ConnectionGene.cs per contract specs/002-genome-phenotype/contracts/ConnectionGene.cs
- [X] T010 [P] Implement NodeSplitResult readonly record struct (NewNodeId, IncomingConnectionInnovation, OutgoingConnectionInnovation) in src/NeatSharp/Genetics/NodeSplitResult.cs per contract specs/002-genome-phenotype/contracts/NodeSplitResult.cs
- [X] T011 [P] Implement CycleDetectedException sealed class extending NeatSharpException in src/NeatSharp/Exceptions/CycleDetectedException.cs per contract specs/002-genome-phenotype/contracts/CycleDetectedException.cs
- [X] T012 [P] Implement InputDimensionMismatchException sealed class extending ArgumentException with Expected/Actual properties in src/NeatSharp/Exceptions/InputDimensionMismatchException.cs per contract specs/002-genome-phenotype/contracts/InputDimensionMismatchException.cs
- [X] T013 [P] Implement InvalidGenomeException sealed class extending NeatSharpException in src/NeatSharp/Exceptions/InvalidGenomeException.cs per contract specs/002-genome-phenotype/contracts/InvalidGenomeException.cs
- [X] T014 Implement Genome sealed class with constructor validation in src/NeatSharp/Genetics/Genome.cs — validate: unique node IDs, valid connection source/target references, at least one input node, at least one output node. Defensively copy input collections. Expose Nodes, Connections (IReadOnlyList), InputCount, OutputCount (computed). Throw InvalidGenomeException on validation failure. Per contract specs/002-genome-phenotype/contracts/Genome.cs and research R-002

**Checkpoint**: Foundation ready — all data types, exceptions, and the Genome class pass their tests. User story implementation can now begin.

---

## Phase 3: User Story 1 — Build and Run a Genome (Priority: P1) MVP

**Goal**: Construct a genome from node/connection genes, convert it to a feed-forward phenotype via INetworkBuilder.Build(), and run inference via IGenome.Activate() with correct results matching hand-calculated values.

**Independent Test**: Construct a genome with 2 inputs, 1 bias, 1 output, and 3 enabled connections with known weights. Build phenotype. Activate with inputs [1.0, 0.5]. Verify output matches sigmoid(1.0×0.5 + 0.5×0.8 + 1.0×(−0.3)) = sigmoid(0.6) ≈ 0.6457 within 1e-10.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T015 [P] [US1] Write ActivationFunctions static class tests in tests/NeatSharp.Tests/Genetics/ActivationFunctionsTests.cs — test each of the 5 built-in functions (SigmoidFunction, TanhFunction, ReLUFunction, StepFunction, IdentityFunction) with known input/output pairs: sigmoid(0)=0.5, tanh(0)=0, relu(-1)=0, relu(1)=1, step(-0.1)=0, step(0.1)=1, identity(42)=42. Verify string constants match expected values ("sigmoid", "tanh", "relu", "step", "identity")
- [X] T016 [P] [US1] Write ActivationFunctionRegistry tests in tests/NeatSharp.Tests/Genetics/ActivationFunctionRegistryTests.cs — test: (a) Get() returns correct function for each built-in name, (b) case-insensitive lookup ("Sigmoid" vs "sigmoid"), (c) Contains() returns true for built-ins and false for unknowns, (d) Get() throws ArgumentException with helpful message for unknown name, (e) Register() throws ArgumentException for duplicate name, (f) Register() succeeds for novel name and Get() retrieves it
- [X] T017 [P] [US1] Write NetworkBuilder tests in tests/NeatSharp.Tests/Genetics/NetworkBuilderTests.cs — test: (a) Build() returns IGenome for valid acyclic genome, (b) NodeCount returns reachable node count, (c) ConnectionCount returns enabled reachable connection count, (d) disabled connections excluded (ConnectionCount doesn't count them), (e) unreachable nodes pruned (hidden node with no path to output excluded from NodeCount), (f) zero-connection genome builds successfully (outputs = activation(0.0))
- [X] T018 [P] [US1] Write FeedForwardNetwork activation tests in tests/NeatSharp.Tests/Genetics/FeedForwardNetworkTests.cs — test: (a) simple 2-input, 1-output network with known weights produces expected sigmoid output within 1e-10, (b) hidden layer with default sigmoid activation produces correct output, (c) disabled connection doesn't contribute to output, (d) bias node contributes constant 1.0 × weight, (e) InputDimensionMismatchException when input span too short/long, (f) ArgumentException when output span wrong size

### Implementation for User Story 1

- [X] T019 [P] [US1] Implement ActivationFunctions static class in src/NeatSharp/Genetics/ActivationFunctions.cs — 5 string constants (Sigmoid="sigmoid", Tanh="tanh", ReLU="relu", Step="step", Identity="identity") and 5 static methods: SigmoidFunction(x)=1/(1+e^(-x)), TanhFunction(x)=Math.Tanh(x), ReLUFunction(x)=Math.Max(0,x), StepFunction(x)=x>0?1:0, IdentityFunction(x)=x. Per contract specs/002-genome-phenotype/contracts/ActivationFunctions.cs
- [X] T020 [P] [US1] Implement IActivationFunctionRegistry interface in src/NeatSharp/Genetics/IActivationFunctionRegistry.cs — Get(string name) returns Func<double,double>, Register(string name, Func<double,double> function), Contains(string name) returns bool. Per contract specs/002-genome-phenotype/contracts/IActivationFunctionRegistry.cs
- [X] T021 [US1] Implement ActivationFunctionRegistry class in src/NeatSharp/Genetics/ActivationFunctionRegistry.cs — Dictionary<string, Func<double,double>> with StringComparer.OrdinalIgnoreCase. Constructor pre-populates 5 built-in functions from ActivationFunctions class. Get() throws ArgumentException if not found (message lists available functions). Register() throws ArgumentException if already registered. Per research R-006
- [X] T022 [P] [US1] Implement INetworkBuilder interface in src/NeatSharp/Genetics/INetworkBuilder.cs — single method: IGenome Build(Genome genome). Per contract specs/002-genome-phenotype/contracts/INetworkBuilder.cs
- [X] T023 [US1] Implement FeedForwardNetwork internal sealed class in src/NeatSharp/Genetics/FeedForwardNetwork.cs — implements IGenome. Constructor takes pre-computed evaluation data (node order, connection mappings, activation functions, input/output indices, buffer size). NodeCount and ConnectionCount are read-only properties. Activate(): validate input/output spans, set input values, set bias to 1.0, evaluate hidden/output nodes in topological order (weighted sum → activation function), copy outputs. Pre-allocated double[] buffer reused across activations. Throw InputDimensionMismatchException for input mismatch, ArgumentException for output mismatch. Per research R-007 and data-model FeedForwardNetwork section
- [X] T024 [US1] Implement FeedForwardNetworkBuilder class in src/NeatSharp/Genetics/FeedForwardNetworkBuilder.cs — implements INetworkBuilder. Constructor injects IActivationFunctionRegistry. Build() algorithm: (1) filter to enabled connections, (2) build adjacency graph, (3) forward BFS from input+bias nodes, (4) backward BFS from output nodes, (5) reachable = forward ∩ backward, (6) Kahn's algorithm topological sort on reachable subgraph — if unprocessed nodes remain throw CycleDetectedException, (7) pre-compute evaluation order and connection mappings, (8) return new FeedForwardNetwork. Per research R-003, R-004, and data-model INetworkBuilder section
- [X] T025 [US1] Update DI registration in src/NeatSharp/Extensions/ServiceCollectionExtensions.cs — register IActivationFunctionRegistry → ActivationFunctionRegistry as singleton, INetworkBuilder → FeedForwardNetworkBuilder as singleton (depends on IActivationFunctionRegistry via constructor injection). Keep existing registrations unchanged. Per research R-009

**Checkpoint**: User Story 1 fully functional. Genome → phenotype → correct inference. All US1 tests pass.

---

## Phase 4: User Story 2 — Deterministic Innovation Tracking (Priority: P2)

**Goal**: Assign deterministic, monotonically increasing innovation IDs for structural mutations (new connections, node splits). Same structural change within a generation receives the same ID. NextGeneration() clears dedup cache while preserving counters.

**Independent Test**: Request innovation IDs for connection (0→5) twice in same generation — verify same ID. Request (1→5) — verify different, higher ID. Call NextGeneration(). Request (0→5) again — verify new, higher ID (cache was cleared).

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T026 [P] [US2] Write InnovationTracker tests in tests/NeatSharp.Tests/Genetics/InnovationTrackerTests.cs — test: (a) GetConnectionInnovation assigns new ID for novel connection, (b) same connection in same generation returns same ID, (c) different connections get different IDs, (d) IDs are monotonically increasing, (e) GetNodeSplitInnovation assigns NewNodeId + two connection innovations, (f) same split in same generation returns identical NodeSplitResult, (g) NextGeneration() clears dedup caches, (h) counters preserved across NextGeneration(), (i) constructor with custom start values, (j) parameterless constructor starts at 0. Per spec acceptance scenarios for US2

### Implementation for User Story 2

- [X] T027 [P] [US2] Implement IInnovationTracker interface in src/NeatSharp/Genetics/IInnovationTracker.cs — GetConnectionInnovation(int sourceNodeId, int targetNodeId) returns int, GetNodeSplitInnovation(int connectionInnovation) returns NodeSplitResult, NextGeneration() returns void. Per contract specs/002-genome-phenotype/contracts/IInnovationTracker.cs
- [X] T028 [US2] Implement InnovationTracker class in src/NeatSharp/Genetics/InnovationTracker.cs — two Dictionary caches: connection cache keyed by (int sourceNodeId, int targetNodeId) → int innovationNumber, node split cache keyed by int connectionInnovation → NodeSplitResult. Two counters: _nextInnovationNumber, _nextNodeId. Constructor(int startInnovationNumber = 0, int startNodeId = 0). GetConnectionInnovation: lookup-or-assign from connection cache. GetNodeSplitInnovation: lookup-or-assign from split cache (assigns 1 new node ID + 2 new innovation IDs). NextGeneration(): clears both caches, preserves counters. Per research R-005
- [X] T029 [US2] Update DI registration in src/NeatSharp/Extensions/ServiceCollectionExtensions.cs — register IInnovationTracker → InnovationTracker as scoped (one per evolution run). Parameterless constructor (starts at 0). Per research R-009

**Checkpoint**: User Story 2 fully functional. Innovation tracking assigns deterministic, deduplicated IDs per generation.

---

## Phase 5: User Story 3 — Extensible Activation Functions (Priority: P3)

**Goal**: Verify custom activation functions can be registered and correctly applied during phenotype evaluation, and that default activation (sigmoid) is used when none specified.

**Independent Test**: Register a custom "leaky_relu" function, build a genome with a hidden node using "leaky_relu", activate with negative inputs, verify the hidden node applies leaky_relu (not sigmoid).

### Implementation for User Story 3

- [X] T030 [US3] Add end-to-end custom activation function tests to tests/NeatSharp.Tests/Genetics/CustomActivationIntegrationTests.cs — test: (a) register custom activation (e.g., leaky_relu: x > 0 ? x : 0.01 * x), (b) build genome with hidden node using custom activation, (c) build phenotype via INetworkBuilder, (d) activate and verify hidden node applies custom function (not default sigmoid), (e) verify default sigmoid is applied when no ActivationFunction specified on NodeGene, (f) verify ArgumentException when genome references unregistered activation function name

**Checkpoint**: User Story 3 validated. Custom activation functions work end-to-end through phenotype evaluation.

---

## Phase 6: User Story 4 — Feed-Forward Graph Validation (Priority: P4)

**Goal**: Verify cycle detection rejects cyclic topologies with CycleDetectedException, and edge cases (disconnected subgraphs, all-disabled connections, zero connections) are handled correctly.

**Independent Test**: Construct genome with cycle (A→B→C→A), attempt Build(), verify CycleDetectedException thrown.

### Implementation for User Story 4

- [X] T031 [US4] Add cycle detection and edge case tests to tests/NeatSharp.Tests/Genetics/NetworkBuilderTests.cs — test: (a) simple cycle (hidden A → hidden B → hidden A) throws CycleDetectedException, (b) longer cycle (A→B→C→A) throws CycleDetectedException, (c) cycle only among hidden nodes (inputs connected, cycle among hiddens, output reachable) throws CycleDetectedException, (d) disabled connection that would create cycle does NOT throw (disabled connections excluded), (e) disconnected subgraph — unreachable hidden nodes don't affect output, (f) genome with only disabled connections — output = activation(0.0), (g) genome with zero connections — output = activation(0.0), (h) valid acyclic genome with deep hidden layers builds successfully

**Checkpoint**: User Story 4 validated. Cycles detected and reported clearly. Edge cases handled correctly.

---

## Phase 7: User Story 5 — Champion Network Inference (Priority: P5)

**Goal**: Verify a genome (simulating a champion from evolution results) produces deterministic, correct outputs across repeated activations with different and identical inputs.

**Independent Test**: Build a known genome, activate 1000 times with same inputs, verify identical outputs every time. Activate with different input sets, verify correct results.

### Implementation for User Story 5

- [X] T032 [US5] Add deterministic repeated inference tests to tests/NeatSharp.Tests/Genetics/FeedForwardNetworkTests.cs — test: (a) activate same network 1000 times with identical inputs [1.0, 0.5] — verify outputs are bit-identical every iteration, (b) activate with different input sets ([0,0], [1,1], [0.5, 0.5]) — verify each produces correct hand-calculated result, (c) interleave different inputs and verify no cross-contamination from buffer reuse, (d) verify NodeCount and ConnectionCount remain consistent across activations

**Checkpoint**: User Story 5 validated. Champion network produces deterministic outputs across repeated activations.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: DI integration verification, quickstart validation, and cross-target testing.

- [X] T033 [P] Update ServiceCollectionExtensionsTests in tests/NeatSharp.Tests/Extensions/ServiceCollectionExtensionsTests.cs — add tests verifying: (a) IActivationFunctionRegistry resolves as singleton with 5 built-in functions, (b) INetworkBuilder resolves as singleton and can build a simple genome, (c) IInnovationTracker resolves as scoped, (d) existing registrations (IRunReporter, INeatEvolver, NeatSharpOptions) still resolve correctly
- [X] T034 Run quickstart.md validation — verify all 7 code samples from specs/002-genome-phenotype/quickstart.md compile and produce expected outputs (simple genome inference, hidden layers, disabled connections, innovation tracking, custom activation, error handling, champion inference)
- [X] T035 Verify all tests pass across both net8.0 and net9.0 target frameworks using `dotnet test`
- [X] T036 [P] Write serialization round-trip tests in tests/NeatSharp.Tests/Genetics/SerializationReadinessTests.cs — test: (a) NodeGene round-trips through System.Text.Json with default options (all properties preserved including Id, Type, ActivationFunction), (b) ConnectionGene round-trips (InnovationNumber, SourceNodeId, TargetNodeId, Weight, IsEnabled), (c) Genome round-trips (Nodes and Connections collections fully preserved, InputCount/OutputCount correct after deserialization), (d) edge case: Genome with mixed node types and disabled connections survives round-trip. Validates FR-012 and SC-005.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No work required — existing project structure sufficient
- **Foundational (Phase 2)**: No dependencies — can start immediately. **BLOCKS all user stories**
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion — **MVP target**
- **User Story 2 (Phase 4)**: Depends on Phase 2 completion — independent of US1
- **User Story 3 (Phase 5)**: Depends on US1 completion (requires working INetworkBuilder and registry)
- **User Story 4 (Phase 6)**: Depends on US1 completion (requires working INetworkBuilder)
- **User Story 5 (Phase 7)**: Depends on US1 completion (requires working FeedForwardNetwork)
- **Polish (Phase 8)**: Depends on US1 + US2 completion (DI registration tests need all services)

### User Story Dependencies

```text
Phase 2 (Foundational)
├──→ US1 (P1): Build and Run ──→ US3 (P3): Custom Activation
│                              ──→ US4 (P4): Validation
│                              ──→ US5 (P5): Champion Inference
├──→ US2 (P2): Innovation Tracking (independent of US1)
└──→ Phase 8 (Polish): after US1 + US2
```

- **US1 and US2 can run in parallel** after Phase 2 completes
- **US3, US4, US5 can run in parallel** after US1 completes
- **Phase 8** runs after US1 and US2 are both complete

### Within Each User Story

1. Tests written FIRST and verified to FAIL
2. Models/data types before services
3. Interfaces before implementations
4. Implementations before DI registration
5. Story complete and all tests pass before moving to next priority

### Parallel Opportunities

- **Phase 2 Tests**: T001–T006 all in different files — run in parallel
- **Phase 2 Implementations**: T007–T013 all in different files — run in parallel (T014 depends on T007–T010, T013)
- **US1 Tests**: T015–T018 all in different files — run in parallel
- **US1 Implementations**: T019, T020, T022 are independent — run in parallel. T021 depends on T019+T020. T023 depends on T021. T024 depends on T021+T022+T023.
- **US2 Tests + Interface**: T026 and T027 can run in parallel
- **US3, US4, US5**: All can run in parallel after US1 completes

---

## Parallel Example: User Story 1

```text
# Step 1: Launch all US1 tests in parallel (all will fail — TDD red phase):
Task: T015 "ActivationFunctions tests in tests/NeatSharp.Tests/Genetics/ActivationFunctionsTests.cs"
Task: T016 "ActivationFunctionRegistry tests in tests/NeatSharp.Tests/Genetics/ActivationFunctionRegistryTests.cs"
Task: T017 "NetworkBuilder tests in tests/NeatSharp.Tests/Genetics/NetworkBuilderTests.cs"
Task: T018 "FeedForwardNetwork tests in tests/NeatSharp.Tests/Genetics/FeedForwardNetworkTests.cs"

# Step 2: Launch independent implementations in parallel:
Task: T019 "ActivationFunctions static class in src/NeatSharp/Genetics/ActivationFunctions.cs"
Task: T020 "IActivationFunctionRegistry interface in src/NeatSharp/Genetics/IActivationFunctionRegistry.cs"
Task: T022 "INetworkBuilder interface in src/NeatSharp/Genetics/INetworkBuilder.cs"

# Step 3: Sequential implementations (each depends on previous):
Task: T021 "ActivationFunctionRegistry in src/NeatSharp/Genetics/ActivationFunctionRegistry.cs"
Task: T023 "FeedForwardNetwork in src/NeatSharp/Genetics/FeedForwardNetwork.cs"
Task: T024 "FeedForwardNetworkBuilder in src/NeatSharp/Genetics/FeedForwardNetworkBuilder.cs"
Task: T025 "DI registration in src/NeatSharp/Extensions/ServiceCollectionExtensions.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 2: Foundational (data types + exceptions + Genome)
2. Complete Phase 3: User Story 1 (activation functions + registry + network builder + phenotype)
3. **STOP and VALIDATE**: All US1 tests pass. Quickstart samples 1–3 work correctly.
4. This is the minimum viable product — genomes can be built and evaluated.

### Incremental Delivery

1. Phase 2 → Foundation ready
2. US1 → Genome execution works → **MVP!**
3. US2 → Innovation tracking works (can proceed in parallel with US1)
4. US3 → Custom activations verified end-to-end
5. US4 → Validation edge cases covered
6. US5 → Deterministic inference guaranteed
7. Phase 8 → DI integration verified, quickstart validated, cross-target confirmed

### Parallel Team Strategy

With multiple developers after Phase 2 completes:

- **Developer A**: US1 (Build and Run) — critical path to MVP
- **Developer B**: US2 (Innovation Tracking) — independent of US1
- After US1 completes, US3/US4/US5 can be split across developers

---

## Notes

- [P] tasks = different files, no dependencies — safe to parallelize
- [Story] label maps task to specific user story for traceability
- Each user story is independently testable after its dependencies are met
- All contracts are in specs/002-genome-phenotype/contracts/ — use as implementation reference
- Research decisions (R-001 through R-009) in specs/002-genome-phenotype/research.md inform implementation choices
- FeedForwardNetwork is `internal sealed` — tests access it through INetworkBuilder.Build() returning IGenome
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
