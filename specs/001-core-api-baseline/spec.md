# Feature Specification: Core Package, Public API & Reproducibility Baseline

**Feature Branch**: `001-core-api-baseline`
**Created**: 2026-02-12
**Status**: Draft
**Input**: Establish the minimal public surface area and baseline project structure for an OSS NEAT library with deterministic runs (CPU) and clear extension points.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - First Evolution Run (Priority: P1)

A developer discovers the NeatSharp library and wants to solve a simple problem (e.g., XOR classification) using neuroevolution. They install the library, write a fitness function that scores candidate solutions, configure basic run parameters, start the evolution, and receive the best-performing candidate — all with minimal code and no prior NEAT expertise.

**Why this priority**: This is the foundational developer experience. If a developer cannot go from zero to a working evolution run quickly and intuitively, no other feature matters. This validates the entire public API surface.

**Independent Test**: Can be fully tested by installing the library, writing a trivial fitness function, running evolution, and verifying that a champion result is returned with a valid fitness score.

**Acceptance Scenarios**:

1. **Given** a new project with the library installed, **When** the developer defines a fitness function and starts an evolution run with default settings, **Then** the run completes and returns a champion with a fitness score.
2. **Given** a new project with the library installed, **When** the developer configures population size, seed, and stopping criteria, **Then** these settings are respected during the run.
3. **Given** a new project with the library installed, **When** the developer follows the quickstart documentation, **Then** they have a working evolution running in under 10 minutes of elapsed time (including reading docs and writing code).

---

### User Story 2 - Reproducible Runs (Priority: P2)

A researcher or developer needs to reproduce an evolution experiment. They run the same configuration with the same seed on the same machine (CPU evaluation) and expect identical results — same champion, same fitness history, same population progression — every time.

**Why this priority**: Reproducibility is a core promise of the library and a baseline requirement for scientific and engineering use. Without determinism guarantees, the library cannot be trusted for research or production experimentation.

**Independent Test**: Can be tested by executing two runs with identical configuration and seed on CPU, then comparing the champion structure and run history for exact equality.

**Acceptance Scenarios**:

1. **Given** a completed evolution run with a specific seed on CPU, **When** the same run is executed again with the same seed and configuration on the same machine, **Then** the champion and run history are identical.
2. **Given** a completed evolution run, **When** the developer inspects the run history, **Then** it contains generation-by-generation fitness statistics and population snapshots sufficient to verify reproducibility.
3. **Given** a run configuration, **When** no seed is explicitly provided, **Then** the system generates a seed and records it in the run results so the run can be reproduced later.

---

### User Story 3 - Environment-Based Evaluation (Priority: P3)

A game developer wants to evolve agents that interact with a simulated environment over multiple steps (episodes). Instead of a single fitness score function, they need to run each candidate through episodes where the candidate observes state and produces actions, and fitness is determined by cumulative episode performance.

**Why this priority**: Many real-world NEAT applications (game AI, robotics, control problems) require sequential decision-making evaluation. This is the most common advanced evaluation pattern and a key differentiator for the library.

**Independent Test**: Can be tested by implementing a simple multi-step environment (e.g., pole balancing), running evolution with it, and verifying that the champion improves over generations.

**Acceptance Scenarios**:

1. **Given** an environment that runs candidates through episodes, **When** evolution is started with this environment, **Then** candidates are evaluated through the episode loop and fitness is derived from episode outcomes.
2. **Given** a batch of candidates to evaluate, **When** the evaluation system processes them, **Then** all candidates receive fitness scores and the system supports scoring multiple candidates in a single call.

---

### User Story 4 - Run Monitoring (Priority: P4)

A developer running a long evolution wants visibility into progress. They configure logging and metrics hooks to observe generation-by-generation statistics (best fitness, average fitness, species count, complexity) without modifying the evolution logic.

**Why this priority**: Observability is essential for tuning and debugging evolution runs but is not required for basic functionality. It builds on the core run infrastructure.

**Independent Test**: Can be tested by configuring a logging hook, running evolution, and verifying that structured log events and metrics are emitted for each generation.

**Acceptance Scenarios**:

1. **Given** a run configured with logging enabled, **When** evolution progresses through generations, **Then** structured log events are emitted with generation statistics.
2. **Given** a run configured with metrics reporting, **When** evolution completes, **Then** metrics (best fitness, average fitness, species count, complexity measures) are available for each generation.
3. **Given** a run with reporting disabled, **When** evolution runs, **Then** no logging or metrics overhead is incurred.

---

### Edge Cases

- What happens when the developer provides an invalid configuration (e.g., population size of zero, negative seed)?
- When the fitness function throws an error during evaluation, the run MUST abort immediately. The exception MUST be wrapped in a `NeatSharpException` and propagated to the caller. Users who need fault tolerance must handle exceptions within their own fitness function.
- When the stopping criteria are met on the very first generation, the run MUST complete normally and return the champion from generation 0. There is no minimum generation count.
- When a run is configured without any stopping criteria, configuration validation (FR-014) MUST reject the configuration with an actionable error: "At least one stopping criterion is required."
- `EvolutionResult` is an immutable record and does not implement `IDisposable`. The champion and all result data remain valid indefinitely after the run completes. No disposal concern exists.
- Determinism is scoped to same-machine, same-configuration, same-library-version runs (see Assumptions). Changes that alter deterministic output for a given seed are documented in release notes but are not treated as semver-breaking changes under the pre-v1.0 release policy.

## Clarifications

### Session 2026-02-12

- Q: Which .NET versions does FR-017 target? → A: .NET 8 (LTS) + .NET 9 (Current)
- Q: What is the canonical public API term for evolved solutions? → A: Genome (matches NEAT literature and constitution); "candidate" is informal prose only
- Q: What happens when the fitness function throws during evaluation? → A: Abort the run; wrap the exception in NeatSharpException and propagate immediately
- Q: Should the evolution entry point support cancellation? → A: Yes, accept CancellationToken; on cancellation return best genome so far with a cancellation flag
- Q: What happens when no stopping criteria are configured? → A: Reject at validation with actionable error ("At least one stopping criterion is required")

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Library MUST provide a configuration object that accepts at minimum: random seed, population size, stopping criteria, complexity limits, and reporting toggles.
- **FR-002**: Library MUST provide a single entry point to start a NEAT evolution run that accepts an evaluation strategy and an optional `CancellationToken`. Run configuration is provided via `NeatSharpOptions` through the DI Options pattern.
- **FR-003**: Library MUST accept a simple fitness callback that takes a genome and returns a numeric fitness score.
- **FR-004**: Library MUST support an episode/environment evaluation model where candidates interact with an environment over multiple steps and fitness is derived from episode outcomes.
- **FR-005**: Library MUST support batch evaluation of multiple candidates in a single call for evaluation strategies that benefit from bulk processing.
- **FR-006**: Library MUST return a champion result representing the highest-fitness genome found during the run.
- **FR-007**: Library MUST return a population snapshot representing the state of the population at the end of the run.
- **FR-008**: Library MUST record and return a run history containing generation-by-generation statistics (best fitness, average fitness, species count, complexity measures, timing breakdown).
- **FR-009**: Library MUST produce identical champion and run history when the same seed and configuration are used on the same machine with CPU-based evaluation.
- **FR-010**: Library MUST auto-generate and record a random seed when none is explicitly provided, enabling later reproduction.
- **FR-011**: Library MUST emit structured log events during evolution via `ILogger` when the host's logging configuration enables the relevant log level.
- **FR-012**: Library MUST emit metrics (best fitness, average fitness, species count, complexity) per generation when metrics reporting is enabled.
- **FR-013**: Library MUST NOT incur logging or metrics overhead when reporting is disabled.
- **FR-014**: Library MUST validate configuration at run start and report clear, actionable errors for invalid inputs (e.g., non-positive population size, contradictory stopping criteria).
- **FR-015**: Library MUST build and pass all tests on both Windows and Linux.
- **FR-016**: Library MUST be packageable for distribution without requiring consumers to install additional native dependencies for CPU-only usage.
- **FR-017**: Library MUST target .NET 8 (LTS) and .NET 9 (Current) via multi-targeting.
- **FR-018**: Library MUST accept an optional `CancellationToken` on the evolution entry point. On cancellation, the run MUST return the best genome found so far along with a flag indicating the run was cancelled, rather than throwing.
- **FR-019**: Library MUST provide an `IRunReporter` contract that accepts an `EvolutionResult` and produces a human-readable text summary of the run, including champion fitness, generation count, seed, and whether the run was cancelled.

### Key Entities

- **NeatSharpOptions**: The complete set of parameters governing an evolution run — seed, population size, stopping criteria (generation limit, fitness target, stagnation threshold), complexity limits, and reporting toggles. Configured via the DI Options pattern.
- **Champion**: The best-performing genome produced by a run, including its fitness score and the generation it was found in.
- **PopulationSnapshot**: A point-in-time view of the entire population, including species groupings and per-individual fitness scores.
- **RunHistory**: The complete chronological record of an evolution run, including per-generation statistics, the final champion, and the seed used.
- **EvaluationStrategy**: The evaluation abstraction for scoring genomes. Users create instances via `EvaluationStrategy` factory methods (`FromFunction`, `FromEnvironment`, `FromBatch`). Underlying interfaces: `IEvaluationStrategy` (internal engine contract), `IEnvironmentEvaluator` (episode-based), `IBatchEvaluator` (bulk scoring).
- **RunReporter**: The contract for producing human-readable summaries of evolution run results. Registered in DI; consumers can replace the default implementation with custom formatters.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can go from zero (no project, no prior experience with the library) to a running NEAT evolution producing a champion in under 10 minutes using only the quickstart documentation.
- **SC-002**: Two consecutive runs on the same machine with identical configuration and seed produce byte-identical champion structures and run histories when using CPU evaluation.
- **SC-003**: The library builds and all tests pass on both Windows and Linux in continuous integration.
- **SC-004**: A complete evolution run (configure, execute, retrieve champion) requires fewer than 20 lines of user-written code.
- **SC-005**: Structured logs and metrics, when enabled, contain sufficient information to reconstruct generation-by-generation progress without access to the original run object.

## Assumptions

- "Two most recent stable runtime versions" refers to .NET 8 (LTS) and .NET 9 (Current) as of February 2026.
- CPU determinism is scoped to same-machine, same-configuration, same-library-version runs. Cross-machine or cross-version determinism is not guaranteed.
- GPU evaluation is out of scope for this spec. Determinism documentation will note that GPU runs are not guaranteed to be bitwise reproducible.
- The NEAT algorithm implementation itself (speciation, crossover, mutation, innovation tracking) is out of scope — only the API contracts and extension points are defined here.
- "Complexity limits" refers to configurable bounds on network size (nodes, connections) to prevent unbounded growth. Defaults are unbounded (`null`); sensible numeric defaults will be established when the algorithm is implemented.
- Batch evaluation is an optional optimization path — the simple fitness callback is the default and required evaluation strategy.
