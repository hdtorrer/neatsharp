# Feature Specification: Training Runner + Evaluation Adapters + Reporting

**Feature Branch**: `004-training-runner`
**Created**: 2026-02-14
**Status**: Draft
**Input**: User description: "Provide the end-to-end training loop and the user-facing evaluation abstractions (fitness callback, episodes, batching), plus required observability."

## Clarifications

### Session 2026-02-14

- Q: What defines run-level stagnation for the stopping criterion in FR-003? → A: All species simultaneously stagnant (per-species counters)
- Q: How does async fitness evaluation (FR-009) interact with the sequential CPU evaluator (FR-013)? → A: Async with sequential await — genomes evaluated one at a time, but fitness functions may use async I/O
- Q: What cancellation responsiveness guarantee should SC-008 require? → A: At the next generation boundary — cancel checked between generations, worst-case wait is one generation cycle
- Q: What maximum generation limits should the runnable examples use for validation? → A: Different limits — 150 for XOR, 500 for function approximation

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Solve XOR with Simple Fitness Function (Priority: P1)

A library consumer wants to evolve a neural network that solves the XOR problem. They configure the library with population size, stopping criteria, and mutation rates, provide a fitness function that scores each genome on the four XOR input-output pairs, and launch training. The system initializes a random population, runs the evolution loop (evaluate, speciate, select, reproduce) generation by generation, and returns a champion genome that correctly maps all four XOR cases.

**Why this priority**: This is the most fundamental use case — the simplest end-to-end path through the entire training pipeline. If this works, the core loop (population initialization, evaluation, speciation, selection, reproduction, stopping) is proven correct. XOR is the canonical NEAT benchmark.

**Independent Test**: Can be fully tested by providing a fitness function that scores XOR correctness and verifying the returned champion produces correct outputs for all four input combinations.

**Acceptance Scenarios**:

1. **Given** a configured library with a seeded random generator and a fitness function that scores XOR correctness, **When** the consumer starts a training run, **Then** the system evolves a champion genome that produces correct XOR outputs (within a configurable error tolerance) and returns it as part of a complete result.
2. **Given** the same seed and configuration, **When** the consumer runs training twice, **Then** both runs produce identical results (same champion, same generation count, same fitness history).
3. **Given** a configured stopping criterion of a maximum generation count, **When** the fitness target is not reached within that limit, **Then** the system stops at the maximum generation and returns the best genome found so far.
4. **Given** a configured fitness target, **When** a genome meets or exceeds the target fitness, **Then** the system stops immediately and returns that genome as champion.

---

### User Story 2 - Monitor Training Progress and Analyze Results (Priority: P2)

A library consumer wants to observe what is happening during a training run — how fitness improves over generations, how many species exist, how large networks are growing, and where time is being spent. After training completes, they want a structured summary and full metrics history to analyze or visualize.

**Why this priority**: Without observability, consumers cannot debug failing runs, tune parameters, or understand algorithm behavior. This is essential for any practical use of the library.

**Independent Test**: Can be tested by running any training scenario and verifying that per-generation statistics are recorded and the final result contains a complete, accurate metrics history and human-readable summary.

**Acceptance Scenarios**:

1. **Given** a training run with metrics enabled, **When** each generation completes, **Then** the system records generation statistics including best fitness, average fitness, species count, species sizes, timing breakdown, and complexity statistics.
2. **Given** a completed training run, **When** the consumer examines the result, **Then** it contains a full metrics history (one entry per generation) and a human-readable summary identifying the champion, total generations, final species count, and seed used.
3. **Given** a training run with metrics disabled, **When** training completes, **Then** no per-generation statistics are collected and the result contains an empty metrics history, with no measurable overhead from the disabled metrics path.
4. **Given** a training run with structured logging configured, **When** key events occur (generation complete, new best fitness, species extinction, stagnation detected, run complete), **Then** structured log entries are emitted at appropriate severity levels.

---

### User Story 3 - Solve a Function Approximation Problem (Priority: P3)

A library consumer wants to evolve a neural network that approximates a continuous mathematical function (e.g., sine wave, quadratic). They provide a fitness function that measures approximation error across a set of sample points. This proves the library generalizes beyond discrete classification to continuous regression tasks.

**Why this priority**: Validating on a second canonical problem proves the training pipeline is not accidentally overfit to XOR's specific structure. Function approximation exercises continuous outputs, larger input spaces, and typically requires more complex networks.

**Independent Test**: Can be tested by providing a fitness function that measures mean-squared error against known function values and verifying the champion approximates the target function within an acceptable error bound.

**Acceptance Scenarios**:

1. **Given** a configured library with a fitness function measuring approximation quality on sample points, **When** the consumer starts a training run, **Then** the system evolves a champion whose outputs approximate the target function within a defined error tolerance.
2. **Given** a function approximation training run, **When** training completes, **Then** the result metrics show progressive fitness improvement across generations, demonstrating the system can optimize continuous objectives.

---

### User Story 4 - Train Using Environment-Based Evaluation (Priority: P4)

A library consumer wants to evaluate genomes by running them through a multi-step episodic environment (e.g., a simple control task where the genome makes decisions at each time step and accumulates a score). They provide an environment evaluator that steps through episodes and the training system orchestrates evaluation for each genome.

**Why this priority**: Environment-based evaluation is the second major evaluation pattern (after simple fitness functions) and is essential for reinforcement-learning-style tasks. It validates that the evaluation adapter abstraction works correctly for stateful, multi-step scenarios.

**Independent Test**: Can be tested by providing a mock environment that runs a fixed number of steps, scores genome outputs, and verifying the training loop correctly feeds genomes through the environment and collects fitness scores.

**Acceptance Scenarios**:

1. **Given** an environment evaluator that scores genomes over multiple steps, **When** the consumer starts training, **Then** the system evaluates each genome by running it through the environment and uses the resulting score as the genome's fitness.
2. **Given** an environment evaluator, **When** training completes successfully, **Then** the champion's fitness reflects its actual performance in the environment.

---

### User Story 5 - Evaluate Candidates in Batch (Priority: P5)

A library consumer wants to score all genomes in a generation simultaneously rather than one at a time. This is important when evaluation benefits from batching (e.g., shared test data setup, external system calls, or future GPU evaluation). They provide a batch evaluator and the training system passes the full population for scoring.

**Why this priority**: Batch evaluation is the third evaluation pattern and enables optimization opportunities. It validates that the batch adapter works correctly and that the training loop can delegate population-level evaluation.

**Independent Test**: Can be tested by providing a batch evaluator that receives the full population, assigns scores, and verifying all genomes receive fitness values.

**Acceptance Scenarios**:

1. **Given** a batch evaluator, **When** a generation is evaluated, **Then** the system passes all genomes to the batch evaluator in a single call and assigns the returned fitness scores to the corresponding genomes.
2. **Given** a batch evaluator, **When** the evaluator returns scores for all genomes, **Then** the training loop proceeds with speciation and reproduction using those scores.

---

### Edge Cases

- What happens when all genomes in a generation receive zero fitness? The system must still proceed with selection and reproduction without crashing (degenerate fitness landscape).
- What happens when the population collapses to a single species? The system must continue normally — speciation and selection still apply within that species.
- What happens when a fitness evaluation throws an exception for a single genome? The system must assign a minimum fitness score (zero) to that genome and continue the run, logging the failure.
- What happens when stopping criteria are met on the very first generation (generation 0)? The system must return a valid result with a champion from the initial evaluated population.
- What happens when the user cancels mid-evaluation? The system must stop gracefully and return the best result from the last fully completed generation.
- What happens when the user cancels before any generation completes? The system must return a result indicating cancellation with no champion data.
- What happens when stagnation is detected across all species simultaneously? The system must preserve at least the fittest species to avoid total population extinction.

## Requirements *(mandatory)*

### Functional Requirements

#### Training Loop

- **FR-001**: System MUST initialize a random population of genomes at the configured population size, with the configured number of input and output nodes, using the provided or generated random seed.
- **FR-002**: System MUST execute a generation loop consisting of: evaluate all genomes, speciate the population, reproduce the next generation (including parent selection) — in that order, once per generation.
- **FR-003**: System MUST check all configured stopping criteria after each generation and terminate when any criterion is satisfied (maximum generations reached, fitness target met, or stagnation threshold exceeded). Run-level stagnation is defined as all species being simultaneously stagnant per their individual species-level stagnation counters.
- **FR-004**: System MUST support cancellation via a cancellation token, checked at generation boundaries (between generations). Upon cancellation, the system returns the best result from the last fully completed generation rather than throwing an exception. Worst-case cancellation latency is one full generation cycle.
- **FR-005**: System MUST produce identical results across runs when given the same seed and configuration (deterministic reproducibility).
- **FR-006**: System MUST call the innovation tracker's generation-advance signal between generations to maintain correct per-generation innovation caching.
- **FR-007**: System MUST enforce configured complexity limits during reproduction, preventing networks from exceeding maximum node or connection counts.

#### Evaluation Adapters

- **FR-008**: System MUST support evaluation via a simple fitness function that receives a single genome and returns a fitness score.
- **FR-009**: System MUST support asynchronous fitness evaluation, allowing the fitness function to perform I/O-bound or long-running work. The baseline CPU evaluator executes async evaluations sequentially (one genome at a time via `await`), preserving determinism while permitting async I/O within the fitness function.
- **FR-010**: System MUST support evaluation via an environment evaluator that runs a genome through multi-step episodes and returns a cumulative fitness score.
- **FR-011**: System MUST support evaluation via a batch evaluator that receives the entire population and returns fitness scores for all genomes in one call.
- **FR-012**: System MUST evaluate all genomes in the population each generation, assigning a fitness score to every genome before proceeding to speciation.

#### CPU Evaluator

- **FR-013**: System MUST provide a baseline CPU evaluation path that executes genome evaluations sequentially (one at a time), ensuring correctness and determinism.
- **FR-014**: CPU evaluation MUST work identically on Windows and Linux without platform-specific configuration.

#### Run Reporting

- **FR-015**: System MUST record per-generation statistics when metrics are enabled: generation number, best fitness, average fitness, species count, species sizes, timing breakdown (evaluation time, reproduction time, speciation time), and complexity statistics (average nodes, average connections).
- **FR-016**: System MUST return a complete result upon run completion containing: the champion genome (highest fitness found during the entire run, not just the final generation), the final population snapshot, the full generation-by-generation metrics history, the seed used, and whether the run was cancelled.
- **FR-017**: System MUST generate a human-readable summary of the run result via the run reporter, including champion fitness, generation the champion was found, total generations, final species count, and seed.
- **FR-018**: System MUST emit structured log entries for key training events: generation completed, new best fitness discovered, species gone extinct, stagnation detected, and run completed.
- **FR-019**: System MUST allow metrics collection and structured logging to be independently disabled, with zero measurable overhead when disabled.
- **FR-020**: System MUST track and report which generation the champion was first discovered, not just the final generation.

#### Error Handling

- **FR-021**: System MUST handle individual genome evaluation failures gracefully by assigning a minimum fitness score (zero) to the failed genome and continuing the run.
- **FR-022**: System MUST preserve at least one species when stagnation penalties would otherwise eliminate the entire population.

#### Runnable Examples

- **FR-023**: System MUST include a runnable XOR example that demonstrates configuring the library, providing a fitness function, running training to completion, and displaying the result.
- **FR-024**: System MUST include a runnable function approximation example (or equivalent second canonical problem) that demonstrates the library solving a different class of problem with continuous outputs.
- **FR-025**: Both examples MUST succeed with fixed seeds and output champion fitness meeting defined thresholds, serving as integration-level validation. The XOR example MUST complete within 150 generations and the function approximation example within 500 generations.

### Key Entities

- **Training Run**: A complete evolution process from population initialization through termination. Characterized by its configuration, seed, evaluation strategy, and resulting output. Exactly one run per call to the evolution entry point.
- **Generation**: A single cycle within a training run: evaluate, speciate, select, reproduce. Each generation produces statistics and advances the population. Generations are zero-indexed.
- **Champion**: The single highest-fitness genome discovered at any point during the run. Tracks the genome, its fitness score, and the generation it was first found.
- **Generation Statistics**: A snapshot of metrics for one generation: fitness distribution, species demographics, network complexity averages, and phase timing. Collected only when metrics are enabled.
- **Run Result**: The complete output of a training run: champion, final population, metrics history, seed, and cancellation status. Immutable once created.

## Assumptions

- The existing evaluation strategy factory (`EvaluationStrategy.FromFunction`, `FromEnvironment`, `FromBatch`) provides the adapter layer; this spec defines how the training loop consumes those adapters.
- The existing mutation, crossover, speciation, and selection components (Spec 003) are correct and ready for integration into the training loop.
- The existing reporting data structures (`GenerationStatistics`, `RunHistory`, `EvolutionResult`, `Champion`, etc.) define the shape of metrics output; this spec defines when and how they are populated.
- Population initialization creates minimal-topology genomes (input nodes connected directly to output nodes) as is standard in NEAT.
- The "function approximation" example may be any suitable continuous problem (sine approximation, polynomial fitting, etc.) — the specific function is an implementation choice.
- "Zero measurable overhead" for disabled metrics means no per-genome or per-generation allocations; the toggle is checked before any collection work begins.
- Stagnation detection uses the existing `StagnationThreshold` in `SelectionOptions`, counting generations without fitness improvement within a species. Run-level stagnation (as a stopping criterion) is triggered only when all species are simultaneously stagnant.

## Non-Goals

- **Persistence and checkpointing**: Saving and restoring training runs mid-execution is deferred to Spec 05.
- **GPU/CUDA evaluation**: Hardware-accelerated evaluation is deferred to Spec 06.
- **Parallel CPU evaluation**: Multi-threaded genome evaluation within a generation is out of scope for this spec; the baseline CPU path is sequential and deterministic.
- **Visualization or UI**: No graphical dashboards or real-time visualization — reporting outputs structured data that consumers can visualize externally.
- **Network topology export**: Serializing genome structures to standard neural network formats is not in scope.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Library solves the XOR problem (all four input-output cases correct within error tolerance) using a seeded configuration, within 150 generations.
- **SC-002**: Library solves a second canonical problem (function approximation or equivalent) using a seeded configuration, within 500 generations, demonstrating generality beyond XOR.
- **SC-003**: Two identical training runs with the same seed and configuration produce byte-identical results (champion genome, fitness history, generation count).
- **SC-004**: Per-generation statistics accurately reflect the actual population state (fitness values, species counts, complexity measures match independently computed values).
- **SC-005**: Training with metrics disabled completes without allocating per-generation statistics objects.
- **SC-006**: Structured log output covers all key events (generation completion, new champion, stagnation, species extinction, run completion) and can be captured by any standard logging provider.
- **SC-007**: Both runnable examples execute to completion on Windows and Linux, producing correct results with fixed seeds.
- **SC-008**: Cancelling a training run mid-execution returns a valid partial result by the next generation boundary (worst-case one generation cycle) after cancellation is requested, without data corruption.
