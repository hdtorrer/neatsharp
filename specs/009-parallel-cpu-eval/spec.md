# Feature Specification: Parallel CPU Evaluation

**Feature Branch**: `009-parallel-cpu-eval`
**Created**: 2026-03-13
**Status**: Draft
**Input**: User description: "make the library be able to use multiple CPU cores"

## Clarifications

### Session 2026-03-13

- Q: Should async fitness functions use the same `MaxDegreeOfParallelism` or unbounded concurrency? → A: Bounded — async functions respect the same `MaxDegreeOfParallelism` as sync functions.
- Q: Should the parallel adapter guarantee thread-safe callback dispatch, or is it the caller's responsibility? → A: The parallel adapter guarantees thread-safe `setFitness` callback dispatch internally.
- Q: Where should `MaxDegreeOfParallelism` live — existing `EvaluationOptions` or a new class? → A: Add to existing `EvaluationOptions` (null = all cores, 1 = sequential).
- Q: Should the library auto-fallback to sequential for small populations? → A: No auto-fallback; user controls via `MaxDegreeOfParallelism`; document guidance only.
- Q: On cancellation, should already-completed fitness scores be preserved or discarded? → A: Preserve completed scores; only pending evaluations are cancelled.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Parallel Fitness Evaluation (Priority: P1)

A library consumer with a population of hundreds or thousands of genomes wants evaluations to run across all available CPU cores rather than sequentially on a single core, so that each generation completes significantly faster.

**Why this priority**: This is the core value proposition. Every NEAT run evaluates the full population every generation; making this parallel directly reduces wall-clock training time by a factor proportional to available cores.

**Independent Test**: Can be tested by running a population evaluation with a CPU-bound fitness function and measuring wall-clock time against the sequential baseline. Multiple CPU cores should show near-linear speedup.

**Acceptance Scenarios**:

1. **Given** a population of 500 genomes and a synchronous fitness function, **When** the library evaluates the population with parallel mode enabled on a machine with 8 cores, **Then** the evaluation completes in roughly 1/8th the time compared to single-core sequential evaluation.
2. **Given** a population of 500 genomes and parallel evaluation enabled, **When** evaluation completes, **Then** every genome receives the same fitness score it would have received under sequential evaluation (deterministic results for deterministic fitness functions).
3. **Given** parallel evaluation is enabled, **When** a user provides a fitness function that is not thread-safe, **Then** the library documents that thread safety is the caller's responsibility and does not add internal synchronization around the user-provided function.

---

### User Story 2 - Configurable Degree of Parallelism (Priority: P2)

A library consumer wants to control how many CPU cores are used for evaluation so they can reserve cores for other workloads or limit resource consumption in shared environments.

**Why this priority**: Sensible defaults (use all cores) satisfy most users, but power users and production deployments need control. This builds on P1 parallelism.

**Independent Test**: Can be tested by setting the max degree of parallelism to 2 on an 8-core machine and verifying that no more than 2 evaluations run concurrently.

**Acceptance Scenarios**:

1. **Given** the user configures a maximum degree of parallelism of 4, **When** evaluation runs on a machine with 16 cores, **Then** at most 4 genomes are evaluated concurrently.
2. **Given** the user does not configure a degree of parallelism, **When** evaluation runs, **Then** the library uses all available processor cores by default.
3. **Given** the user sets the degree of parallelism to 1, **When** evaluation runs, **Then** behavior is equivalent to sequential evaluation (opt-out path).

---

### User Story 3 - Parallel Evaluation with Error Resilience (Priority: P2)

A library consumer using parallel evaluation expects the same error-handling behavior as sequential evaluation: per-genome failures are accumulated and reported without aborting evaluation of other genomes.

**Why this priority**: Error resilience is already a core contract of the sequential path. Parallel evaluation must preserve this guarantee or it would be a regression.

**Independent Test**: Can be tested by providing a fitness function that throws for specific genomes, running in parallel, and verifying that non-failing genomes still receive their fitness scores and the aggregated error is reported.

**Acceptance Scenarios**:

1. **Given** a population of 100 genomes where 5 genomes throw exceptions during evaluation, **When** parallel evaluation completes, **Then** the remaining 95 genomes receive correct fitness scores and an aggregated error containing all 5 failures is reported.
2. **Given** parallel evaluation and the error mode set to "assign default fitness", **When** individual genomes fail, **Then** failed genomes receive the configured default fitness value and training continues.

---

### User Story 4 - Parallel Environment-Based Evaluation (Priority: P3)

A library consumer using episode-based evaluation (e.g., Cart-Pole simulation) wants multiple genomes to be evaluated in parallel, each running its own environment episodes concurrently.

**Why this priority**: Environment evaluations are typically the most CPU-intensive (many timesteps per genome). Parallelizing these yields the largest absolute speedup, but depends on the core parallel infrastructure from P1.

**Independent Test**: Can be tested by running a Cart-Pole-style environment evaluation with parallel mode and verifying that multiple environment episodes execute concurrently and produce correct fitness scores.

**Acceptance Scenarios**:

1. **Given** an environment evaluator and a population of 200 genomes, **When** parallel evaluation is enabled, **Then** multiple genomes run their environment episodes concurrently across available cores.
2. **Given** an environment evaluator with episodes that vary in duration, **When** parallel evaluation completes, **Then** dynamic scheduling ensures cores are not left idle waiting for long-running episodes.

---

### User Story 5 - Integration with Hybrid Evaluator (Priority: P3)

A library consumer using the hybrid (CPU + GPU) evaluator wants the CPU portion of hybrid evaluation to use multiple cores, so the CPU backend does not become a bottleneck relative to the GPU backend.

**Why this priority**: The hybrid evaluator already dispatches CPU and GPU batches concurrently, but the CPU batch itself evaluates sequentially. Multi-core CPU evaluation would improve overall hybrid throughput and better balance the two backends.

**Independent Test**: Can be tested by running a hybrid evaluation and verifying that the CPU-assigned genomes are evaluated in parallel within their batch.

**Acceptance Scenarios**:

1. **Given** hybrid evaluation splits 300 genomes into 100 CPU + 200 GPU, **When** the CPU batch evaluates, **Then** the 100 CPU genomes are evaluated in parallel across available cores.
2. **Given** the adaptive partition policy is active, **When** CPU parallelism reduces CPU latency, **Then** the partition policy naturally adjusts the split ratio to send more genomes to CPU (existing PID controller behavior).

---

### Edge Cases

- What happens when the population size is smaller than the configured degree of parallelism? The library should use only as many threads as there are genomes.
- What happens when all genomes fail during parallel evaluation? The aggregated error should contain all failures, and all genomes should receive the default fitness if error mode permits.
- What happens when the fitness function is extremely fast (microsecond-scale)? The library does not auto-fallback to sequential; the user controls this via `MaxDegreeOfParallelism = 1`. Documentation should provide guidance on when parallel evaluation is beneficial vs. overhead-dominated.
- What happens when the user cancels evaluation mid-generation? In-flight evaluations are cancelled cooperatively. Already-completed fitness scores are preserved; only pending evaluations are cancelled.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST support evaluating genomes across multiple CPU cores concurrently within a single generation.
- **FR-002**: The system MUST allow the user to configure the maximum number of CPU cores used for evaluation.
- **FR-003**: The system MUST default to using all available processor cores when no explicit degree of parallelism is configured.
- **FR-004**: The system MUST allow the user to disable parallel evaluation by setting the degree of parallelism to 1, reverting to sequential behavior.
- **FR-005**: The system MUST produce identical fitness results as sequential evaluation for deterministic fitness functions (order-independent correctness).
- **FR-006**: The system MUST accumulate per-genome evaluation errors without aborting the evaluation of other genomes, consistent with the existing sequential error-handling contract.
- **FR-007**: The system MUST support parallel evaluation for synchronous fitness functions.
- **FR-008**: The system MUST support parallel evaluation for asynchronous fitness functions, bounded by the same `MaxDegreeOfParallelism` setting as synchronous functions.
- **FR-009**: The system MUST support parallel evaluation for environment-based (episode) evaluators.
- **FR-010**: The system MUST propagate cancellation tokens to in-flight parallel evaluations so that cancellation is cooperative. Already-completed fitness scores MUST be preserved; only pending evaluations are cancelled.
- **FR-011**: The system MUST ensure that the fitness-reporting callback is safe to invoke from multiple threads concurrently; the parallel adapter owns this guarantee internally (callers need not synchronize the callback).
- **FR-012**: The system MUST integrate with the existing hybrid evaluator so that the CPU batch portion uses multi-core evaluation.
- **FR-013**: The system MUST document that user-provided fitness functions must be thread-safe when parallel evaluation is enabled.

### Key Entities

- **Parallelism Configuration**: A `MaxDegreeOfParallelism` property on the existing `EvaluationOptions` class (null = use all available cores, 1 = sequential). No separate enablement toggle; the degree of parallelism value controls both enablement and concurrency level.
- **Parallel Evaluation Adapter**: The internal component responsible for distributing genome evaluations across multiple cores and collecting results, maintaining the same interface contract as existing sequential adapters.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Population evaluation on a machine with N cores completes in no more than 2x the theoretical ideal speedup (wall-clock time <= 2 * sequential_time / N) for CPU-bound fitness functions with populations of 100+ genomes.
- **SC-002**: All existing tests pass without modification when parallel evaluation is enabled, confirming backward-compatible behavior.
- **SC-003**: For deterministic fitness functions, parallel and sequential evaluation produce identical fitness scores for every genome in the population.
- **SC-004**: When a configurable fraction of genomes fail during parallel evaluation, the remaining genomes still receive correct fitness scores and training continues (matching sequential error-handling behavior).
- **SC-005**: Users can configure and enable parallel evaluation with no more than 2 lines of configuration change from a sequential setup.

## Assumptions

- User-provided fitness functions are assumed to be thread-safe when the user enables parallel evaluation. The library will document this requirement but will not enforce it at runtime.
- The parallel adapter guarantees thread-safe dispatch of the `Action<int, double>` callback internally; callers do not need to synchronize the callback.
- The .NET thread pool is the underlying mechanism for parallelism; no custom thread management is introduced.
- Parallel evaluation applies to the CPU evaluation path only. GPU evaluation already achieves parallelism through ILGPU kernel execution.
- The existing evaluation strategy factory and adapter pattern is extended (not replaced) to support parallel variants.

## Scope Boundaries

### In Scope
- Multi-core parallel evaluation for all three CPU adapter types (sync, async, environment)
- Configurable degree of parallelism
- Thread-safe error accumulation
- Integration with hybrid evaluator's CPU batch
- Documentation of thread-safety requirements for user-provided functions

### Out of Scope
- Distributed evaluation across multiple machines or processes
- GPU-specific parallelism changes (already handled by ILGPU)
- Automatic thread-safety wrappers for user-provided fitness functions
- Parallelism within a single genome's evaluation (e.g., parallel network activation)

## Dependencies

- Depends on existing evaluation strategy and adapter infrastructure (Features 001/004)
- Depends on existing batch evaluator interface for hybrid integration (Features 006/007)
- No new external package dependencies required (built-in threading and task parallelism primitives are sufficient)
