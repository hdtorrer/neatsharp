# Feature Specification: Hybrid Evaluation Scheduler (CPU + GPU Concurrent, Adaptive Partitioning)

**Feature Branch**: `007-hybrid-eval-scheduler`
**Created**: 2026-02-16
**Status**: Draft
**Input**: User description: "Evaluate a single generation using CPU and GPU simultaneously, with an adaptive scheduler that minimizes wall-clock time."

## Clarifications

### Session 2026-02-16

- Q: After GPU failure, how does GPU recovery work — no auto-recovery, periodic re-probe, or immediate re-probe every generation? → A: Periodic re-probe: attempt GPU health check every N generations (configurable).
- Q: What algorithm family should the adaptive split policy use to adjust the split ratio each generation? → A: PID controller targeting zero idle time difference between backends.
- Q: How does the hybrid evaluator integrate with the DI container? → A: Decorator pattern — implements `IBatchEvaluator`, wraps CPU and GPU backends, selected via configuration options.
- Q: What is the maximum acceptable overhead budget for the scheduler's own partitioning/dispatch/merge logic? → A: Max 5% of the slower backend's evaluation time.
- Q: Is the minimum population threshold for hybrid splitting hard-coded, configurable, or dynamic? → A: Configurable option with a sensible default (e.g., 50).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Concurrent CPU+GPU Evaluation with Static Split (Priority: P1)

A researcher training a NEAT population wants to use both CPU and GPU simultaneously to evaluate a generation faster than either backend alone. They configure the system to use hybrid evaluation with a fixed split ratio (e.g., 70% of genomes to GPU, 30% to CPU). When training runs, the system partitions the population, dispatches each partition to the appropriate backend concurrently, and merges the fitness results. The researcher observes reduced wall-clock time per generation compared to using either backend in isolation, especially for workloads where GPU transfer overhead previously caused GPU-only evaluation to underperform.

**Why this priority**: Concurrent CPU+GPU evaluation is the core value proposition. Without a working hybrid dispatch that splits and merges correctly, none of the higher-level policies (adaptive, cost-based) are meaningful. A static split with correct result merging is the minimum viable product.

**Independent Test**: Can be fully tested by running a known benchmark workload with hybrid evaluation enabled (static 70/30 split) and verifying that (a) all genomes receive correct fitness scores, (b) fitness results match what each backend would produce independently, and (c) wall-clock time is lower than using either backend alone on a transfer-dominated workload.

**Acceptance Scenarios**:

1. **Given** a CUDA-capable machine with hybrid evaluation enabled and a static split configured, **When** a generation of genomes is evaluated, **Then** genomes are partitioned across CPU and GPU, both backends execute concurrently, and all genomes receive fitness scores that match their respective backend's output.
2. **Given** a benchmark workload where GPU-only evaluation previously regressed due to transfer overhead, **When** hybrid evaluation is used with a static split, **Then** wall-clock time per generation is lower than both CPU-only and GPU-only evaluation.
3. **Given** a population of 1,000 genomes evaluated in hybrid mode, **When** results are merged, **Then** each fitness score is associated with the correct genome (no ID misalignment or swapped results).

---

### User Story 2 - Adaptive Partitioning That Converges to Optimal Split (Priority: P2)

A researcher does not want to manually tune the CPU/GPU split ratio. They enable adaptive partitioning, and the system automatically adjusts the split each generation using a PID controller that targets zero idle time difference between CPU and GPU backends. Over a few generations, the split converges to a stable ratio that minimizes wall-clock time for the given workload and hardware. The researcher can observe the convergence in scheduling metrics.

**Why this priority**: Adaptive partitioning is the primary differentiator of this feature over a naive manual split. It removes the need for users to benchmark and tune split ratios themselves, making hybrid evaluation practical for diverse hardware and workload combinations.

**Independent Test**: Can be tested by running a training session with adaptive partitioning enabled and observing the split ratio over 20+ generations. The ratio should stabilize (variance below a documented threshold) within a small number of generations. Throughput should be at or near the manually-tuned optimum.

**Acceptance Scenarios**:

1. **Given** hybrid evaluation with adaptive partitioning enabled, **When** training runs for 20 generations, **Then** the GPU/CPU split ratio stabilizes within the first 10 generations (measured as less than 5 percentage points of variance over 5 consecutive generations).
2. **Given** a workload with known optimal split ratio (determined by manual benchmarking), **When** adaptive partitioning runs, **Then** the converged ratio is within 10 percentage points of the manually-determined optimum.
3. **Given** a hardware change mid-session (e.g., GPU thermal throttling reduces GPU throughput), **When** adaptive partitioning detects the throughput shift, **Then** the split ratio adjusts within 3-5 generations to compensate.

---

### User Story 3 - GPU Failure Transparent Fallback (Priority: P3)

A researcher is running a long training session in hybrid mode when the GPU becomes unavailable (driver crash, out-of-memory, device reset). The system detects the failure, immediately routes all remaining genomes in the current generation to CPU evaluation, logs a warning with details about the failure, and continues training. No fitness results are lost. Subsequent generations continue with CPU-only evaluation. The system periodically re-probes GPU availability every N generations (configurable) and automatically resumes hybrid evaluation if the GPU recovers.

**Why this priority**: Reliability is critical for long training runs. Users should not lose hours of training progress because of a transient GPU issue. This story ensures hybrid mode is strictly safer than GPU-only mode.

**Independent Test**: Can be tested by simulating a GPU failure (e.g., injecting an error into the GPU backend) during a training run and verifying that the system continues with CPU-only evaluation, logs the failure event, and produces valid training results.

**Acceptance Scenarios**:

1. **Given** hybrid evaluation is running and the GPU backend fails mid-generation, **When** the failure is detected, **Then** all genomes not yet evaluated are routed to CPU, the generation completes successfully, and a warning is logged describing the failure.
2. **Given** a GPU failure has occurred, **When** subsequent generations are evaluated, **Then** the system continues with CPU-only evaluation without user intervention.
3. **Given** a GPU failure during evaluation, **When** the generation completes, **Then** no genomes are missing fitness scores and no fitness scores are duplicated or swapped.

---

### User Story 4 - Scheduling Metrics and Observability (Priority: P4)

A researcher or operator wants to understand how the hybrid scheduler is performing. They can access per-generation metrics showing: how many genomes were evaluated by each backend, throughput (genomes per second) for each backend, average latency per backend, the current split ratio, and any fallback events. These metrics enable informed decisions about configuration tuning and hardware provisioning.

**Why this priority**: Observability is essential for users to trust the scheduler, diagnose performance issues, and validate that adaptive partitioning is working as expected. Without metrics, the scheduler is a black box.

**Independent Test**: Can be tested by running a training session with hybrid evaluation and verifying that all documented metrics are emitted each generation. Metrics should be accessible programmatically and via the training log.

**Acceptance Scenarios**:

1. **Given** hybrid evaluation completes a generation, **When** metrics are queried, **Then** the following are available: genome count per backend, throughput per backend (genomes/sec), latency per backend, current split ratio, and fallback event count.
2. **Given** a GPU fallback event occurs, **When** metrics are queried, **Then** the fallback event is recorded with a timestamp, failure reason, and the number of genomes rerouted to CPU.
3. **Given** adaptive partitioning is active, **When** metrics are queried over multiple generations, **Then** the split ratio history is available showing convergence behavior.

---

### User Story 5 - Cost-Based Partitioning Using Genome Complexity (Priority: P5)

A researcher working with a population that has high structural diversity (some genomes have 5 nodes, others have 500) wants the scheduler to account for genome complexity when partitioning. Simple genomes are routed to CPU (where per-genome overhead is lower), while complex genomes are batched to GPU (where parallel evaluation of large networks is efficient). This complexity-aware split improves throughput beyond what a uniform random split achieves.

**Why this priority**: Cost-based partitioning is an optimization that builds on the static and adaptive split foundations. It delivers the most value for populations with high structural diversity, which is a common characteristic of mature NEAT populations.

**Independent Test**: Can be tested by constructing a population with bimodal complexity distribution (half simple, half complex genomes) and comparing throughput with uniform vs. cost-based partitioning. Cost-based partitioning should achieve higher throughput.

**Acceptance Scenarios**:

1. **Given** a population with bimodal complexity (50% genomes with <10 nodes, 50% with >100 nodes) and cost-based partitioning enabled, **When** the population is evaluated, **Then** simple genomes are preferentially routed to CPU and complex genomes to GPU.
2. **Given** a population with bimodal complexity, **When** comparing cost-based partitioning to uniform random partitioning, **Then** cost-based achieves at least 10% higher throughput.
3. **Given** cost-based partitioning is enabled, **When** genome complexity is estimated, **Then** the estimate uses structural properties (node count, connection count) without requiring a full evaluation of the genome.

---

### Edge Cases

- What happens when the population is too small to benefit from splitting (e.g., <10 genomes)? The system should detect this and evaluate the entire population on whichever single backend is faster, avoiding the overhead of partitioning and synchronization.
- What happens when all genomes have identical complexity? Cost-based partitioning should degrade gracefully to a uniform split without errors or performance regression.
- What happens when the GPU partition completes much faster than the CPU partition (or vice versa)? The system should not block the faster backend while waiting for the slower one. Future generations' adaptive split should adjust to rebalance.
- What happens when the GPU fails during the very first generation (before any throughput history exists)? The system should fall back to CPU-only and initialize adaptive history from CPU-only measurements.
- What happens when hybrid evaluation is configured but no GPU is available at startup? The system should detect this on the first evaluation attempt (not at DI registration time), log a warning before returning the first generation's results, and operate in CPU-only mode without error.
- What happens when the user configures a static split of 100% GPU / 0% CPU (or vice versa)? The system should accept this as a valid configuration and behave identically to single-backend evaluation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a hybrid evaluation mode that partitions a population across CPU and GPU backends and evaluates both partitions concurrently within a single generation.
- **FR-002**: System MUST support a static split policy where the user specifies a fixed percentage of genomes to route to GPU vs. CPU.
- **FR-003**: System MUST support a cost-based split policy that uses genome structural properties (node count, connection count) to estimate evaluation cost and assign genomes to the backend best suited for their complexity.
- **FR-004**: System MUST support an adaptive split policy that uses a PID controller to adjust the split ratio each generation. The PID controller targets zero idle time difference between CPU and GPU backends (i.e., both backends finish simultaneously). PID gains (Kp, Ki, Kd) MUST be configurable with sensible defaults (Kp=0.5, Ki=0.1, Kd=0.05 per R-001).
- **FR-005**: System MUST ensure that merged fitness results align exactly with the correct genome identifiers, with no misalignment, duplication, or omission regardless of which backend evaluated each genome.
- **FR-006**: System MUST execute CPU and GPU evaluation concurrently (not sequentially) to achieve wall-clock time savings.
- **FR-007**: System MUST detect GPU backend failure during evaluation and transparently reroute unevaluated genomes to CPU, completing the generation without data loss.
- **FR-008**: System MUST log a warning when GPU failure triggers fallback, including the failure reason and the number of genomes rerouted.
- **FR-009**: System MUST continue with CPU-only evaluation for subsequent generations after a GPU failure. The system MUST periodically re-probe GPU availability every N generations (configurable, default: 10 generations per R-005) and resume hybrid evaluation automatically if the GPU recovers.
- **FR-010**: System MUST emit per-generation scheduling metrics: genome count per backend, throughput per backend, latency per backend, current split ratio, and fallback event details.
- **FR-011**: System MUST allow the user to select the partitioning policy (static, cost-based, or adaptive) via configuration.
- **FR-012**: System MUST allow the user to disable hybrid evaluation entirely, reverting to single-backend behavior with no performance overhead.
- **FR-013**: System MUST preserve CPU-only evaluation determinism when hybrid mode is disabled.
- **FR-014**: System MUST handle populations below a configurable minimum threshold (default: 50 genomes) by evaluating on a single backend without partitioning overhead.
- **FR-015**: System MUST validate hybrid configuration at startup (e.g., split percentages sum to 100%, selected policy is recognized) and report clear errors for invalid settings.
- **FR-016**: Adaptive split policy MUST converge to a stable ratio (less than 5 percentage points of variance over 5 consecutive generations) within 10 generations for a steady-state workload (per SC-002).
- **FR-017**: System MUST accept a static split of 0% or 100% for either backend and behave identically to single-backend evaluation.
- **FR-018**: The hybrid scheduler's own overhead (partitioning, dispatch, synchronization, result merging) MUST NOT exceed 5% of the slower backend's evaluation time for populations of 200+ genomes.

### Key Entities

- **Hybrid Evaluation Backend**: The top-level evaluator that implements `IBatchEvaluator` as a decorator, wrapping CPU and GPU backends. Partitions the population, dispatches evaluation concurrently, and merges results. Configured with a partitioning policy. Registered in DI via configuration options; users select hybrid mode through the same options pattern used by existing evaluators.
- **Partitioning Policy**: Determines how genomes are assigned to CPU vs. GPU each generation. Three variants: static (fixed ratio), cost-based (complexity-driven), and adaptive (throughput-driven). Interchangeable via configuration.
- **Scheduling Metrics**: Per-generation data capturing backend performance. Includes genome count, throughput, latency, split ratio, and fallback events. Used by the adaptive policy for decision-making and by operators for observability.
- **Genome Complexity Estimate**: A lightweight structural measure (derived from node count, connection count) used by the cost-based policy to classify genomes for routing. Does not require evaluating the genome.

## Assumptions

- The existing CPU evaluation path (`IEvaluationStrategy` / `IBatchEvaluator` adapters) and GPU evaluation path (`GpuBatchEvaluator` in `NeatSharp.Gpu`) are both stable and can be wrapped without modification to their public contracts. The hybrid evaluator implements `IBatchEvaluator` as a decorator, making it a drop-in replacement selectable via DI configuration options.
- The fitness callback pattern (`setFitness(index, score)`) supports concurrent invocation from multiple threads, as both backends may call it simultaneously.
- GPU and CPU evaluation of the same genome may produce slightly different fitness values (fp32 vs. fp64 precision). This is an accepted trade-off documented in the 006-cuda-evaluator spec. The hybrid scheduler does not introduce additional numerical divergence.
- Single-GPU only; multi-GPU scheduling is not in scope.
- The adaptive policy's throughput measurement is based on wall-clock time observed within the scheduler, not external profiling tools.
- Population sizes of 50-10,000 are the target range. Below 50 genomes, hybrid overhead may negate the benefits of splitting.
- The cost-based complexity estimate uses only genome structural properties available without phenotype evaluation (node count and connection count). More sophisticated cost models (e.g., activation function cost) are a potential future enhancement.

## Non-Goals

- Device-resident population mirror (keeping genomes in GPU memory across generations).
- Kernel-level optimizations within the GPU evaluator itself (those belong to 006-cuda-evaluator).
- Multi-GPU or distributed evaluation across multiple machines.
- GPU-accelerated mutation, crossover, speciation, or selection operators.
- Dynamic work stealing between backends mid-evaluation (the partition is fixed for each generation).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a reference workload where GPU-only evaluation previously regressed due to transfer overhead, hybrid evaluation achieves lower wall-clock time per generation than both CPU-only and GPU-only evaluation.
- **SC-002**: Adaptive partitioning converges to a stable split ratio (less than 5 percentage points of variance over 5 consecutive generations) within 10 generations on a steady-state workload.
- **SC-003**: CPU-only evaluation produces identical results (bit-for-bit deterministic) whether hybrid mode is enabled or disabled, confirming no side effects from the hybrid infrastructure.
- **SC-004**: When the GPU backend fails mid-run, training continues to completion with CPU-only evaluation, losing zero genomes' fitness scores from the affected generation.
- **SC-005**: Per-generation scheduling metrics are available for all generations, enabling operators to diagnose throughput imbalances and verify adaptive convergence.
- **SC-006**: Cost-based partitioning achieves at least 10% higher throughput than uniform random partitioning on populations with high structural diversity (bimodal complexity distribution).
- **SC-007**: A benchmark report documents hybrid vs. CPU-only vs. GPU-only performance across at least 3 population sizes and 2 workload profiles (transfer-dominated and compute-dominated), with reproducible methodology.
