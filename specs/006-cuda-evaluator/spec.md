# Feature Specification: CUDA Evaluator Backend + Auto GPU Use + Fallback

**Feature Branch**: `006-cuda-evaluator`
**Created**: 2026-02-15
**Status**: Draft
**Input**: User description: "Provide GPU-accelerated evaluation as a first-class capability, while preserving correctness via CPU fallback."

## Clarifications

### Session 2026-02-15

- Q: Which C#/.NET CUDA interop library should this feature use? → A: ILGPU — managed .NET GPU compiler; write kernels in C#, compiled to PTX at runtime.
- Q: Should the GPU evaluator use single precision (fp32) or double precision (fp64)? → A: fp32 (single precision) by default for maximum throughput; tolerance adjusted to ~1e-4.
- Q: When the GPU becomes unavailable mid-training, should the system automatically retry on CPU or report a hard error? → A: Automatic CPU fallback per-generation; detect GPU failure, switch to CPU for that generation, log a warning, and continue training.
- Q: Should CUDA/GPU support be distributed as a separate NuGet package or integrated into the main library? → A: Separate package (e.g., NeatSharp.Gpu); main library has no ILGPU dependency.
- Q: What minimum CUDA compute capability should the GPU evaluator require? → A: Compute capability 5.0 (Maxwell, ~2014); covers GTX 750 Ti and newer.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - GPU-Accelerated Batch Evaluation (Priority: P1)

A researcher training a NEAT population on a medium-to-large problem (e.g., hundreds of genomes, each evaluated across many input cases) wants evaluation to complete significantly faster than the CPU path. They configure the system to use GPU evaluation and run their training loop. The system evaluates the entire population on the GPU each generation, returning fitness scores that drive selection and reproduction. The researcher observes a substantial speedup without needing to change their problem definition or training configuration beyond enabling GPU evaluation.

**Why this priority**: GPU-accelerated batch evaluation is the core value proposition of this feature. Without it, none of the other stories (auto-detection, fallback, tolerance) are meaningful. A working GPU evaluator that delivers measurable throughput gains is the minimum viable product.

**Independent Test**: Can be fully tested by running a known benchmark workload (e.g., XOR or a multi-input function approximation problem with a fixed population) on a CUDA-capable machine and measuring throughput compared to the CPU baseline. Delivers the primary value: faster training cycles.

**Acceptance Scenarios**:

1. **Given** a CUDA-capable machine and a population of genomes configured for batch evaluation, **When** the user runs training with GPU evaluation enabled, **Then** all genomes in the population receive fitness scores each generation and training completes successfully.
2. **Given** a benchmark workload with a known CPU throughput baseline, **When** the same workload runs with GPU evaluation, **Then** throughput is at least 5x higher than the CPU baseline.
3. **Given** a population containing genomes of varying complexity (different node/connection counts), **When** GPU evaluation runs, **Then** all genomes are evaluated correctly regardless of their structural differences.

---

### User Story 2 - Automatic GPU Detection and Selection (Priority: P2)

A user installs the library on a machine that may or may not have a compatible GPU. They do not want to manually check hardware or conditionally configure their code. When they start training, the system automatically detects whether a compatible GPU is available. If one is found, evaluation uses the GPU. If not, evaluation falls back to the CPU path transparently. The user's code works identically on both machine types without any code changes.

**Why this priority**: Auto-detection removes friction and makes GPU acceleration accessible without requiring users to understand hardware details. It directly enables the acceptance criterion that "the same configuration can run on CPU-only and CUDA-capable machines without code changes."

**Independent Test**: Can be tested by running the same training configuration on two machines (one with GPU, one without) and verifying both complete successfully. On the GPU machine, verify GPU evaluation was selected. On the CPU-only machine, verify CPU fallback was used. In both cases, training produces valid results.

**Acceptance Scenarios**:

1. **Given** a machine with a compatible GPU and the user has not explicitly disabled GPU evaluation, **When** training starts, **Then** the system automatically selects GPU evaluation and logs that GPU was detected and is in use.
2. **Given** a machine without a compatible GPU, **When** training starts, **Then** the system automatically falls back to CPU evaluation and logs an informational message explaining that no GPU was found.
3. **Given** a machine with a compatible GPU and the user has explicitly disabled GPU evaluation via configuration, **When** training starts, **Then** the system uses CPU evaluation regardless of GPU availability.
4. **Given** a machine where GPU hardware exists but required runtime dependencies are missing, **When** training starts, **Then** the system falls back to CPU evaluation and provides an actionable error message describing what is missing and how to resolve it (e.g., "CUDA toolkit version X.Y required; see [link] for installation instructions").

---

### User Story 3 - CPU/GPU Result Consistency (Priority: P3)

A user who has been developing and debugging their fitness function on a CPU-only laptop wants confidence that switching to GPU evaluation on a training server will not produce meaningfully different results. They run a canonical test problem on both CPU and GPU and compare fitness scores. While small floating-point differences are expected (and documented), the overall training outcome (champion fitness, convergence behavior) remains within an acceptable tolerance.

**Why this priority**: Without result consistency guarantees, users cannot trust GPU evaluation as a drop-in replacement. This story ensures the GPU path is not just fast but also correct. It has lower priority than the core GPU evaluator and auto-detection because those must exist before consistency can be validated.

**Independent Test**: Can be tested by running a canonical problem (e.g., XOR) with identical seeds on both CPU and GPU, then comparing per-genome fitness values. Individual values should be within a documented epsilon. Champion fitness at convergence should be within a documented delta. The test verifies correctness without requiring the full training infrastructure — just the evaluation step.

**Acceptance Scenarios**:

1. **Given** a set of genomes evaluated on both CPU and GPU with identical inputs, **When** comparing individual fitness scores, **Then** each pair of scores differs by no more than a documented epsilon tolerance.
2. **Given** a canonical benchmark problem run to completion on both CPU and GPU with the same seed, **When** comparing final champion fitness, **Then** the values are within a documented delta tolerance.
3. **Given** a canonical benchmark problem, **When** GPU evaluation is used, **Then** the problem still meets its published fitness threshold (pass/fail criteria may use separate GPU-specific thresholds if numeric differences require it).

---

### User Story 4 - Determinism Documentation and Best-Effort GPU Reproducibility (Priority: P4)

A researcher wants to reproduce experiments across runs. They understand that GPU floating-point operations may produce non-deterministic results due to parallel reduction ordering. The system provides a "best-effort deterministic" GPU mode that uses deterministic algorithms where available. The CPU path remains the canonical reference for full determinism. Documentation clearly explains what guarantees each mode provides.

**Why this priority**: Reproducibility is important for scientific work but is a secondary concern compared to correctness and performance. Full GPU determinism is a known hard problem; documenting the trade-offs is more valuable than attempting impossible guarantees.

**Independent Test**: Can be tested by running the same workload with the same seed in best-effort deterministic GPU mode multiple times and verifying that results are identical (or documenting which operations introduce non-determinism). Compare against the documented guarantees to verify the documentation is accurate.

**Acceptance Scenarios**:

1. **Given** GPU evaluation with best-effort deterministic mode enabled, **When** the same workload runs twice with the same seed, **Then** results are identical or differ only in documented, expected ways.
2. **Given** the project documentation, **When** a user reads the determinism section, **Then** they can understand what reproducibility guarantees the CPU path provides vs. the GPU path, and what steps to take for maximum reproducibility.

---

### User Story 5 - Benchmark Report and Performance Validation (Priority: P5)

A prospective user or contributor wants to understand the performance characteristics of GPU evaluation before adopting it. A benchmark report checked into the repository documents CPU vs. GPU throughput on a representative workload, including hardware configuration, population sizes tested, and speedup factors. This report serves as both validation evidence and a reference for users sizing their hardware.

**Why this priority**: The benchmark report is a deliverable that validates the performance claims and helps users make informed decisions. It depends on all other stories being complete.

**Independent Test**: Can be tested by running the benchmark suite on reference hardware and comparing results against the published report. The report itself can be reviewed for completeness and clarity.

**Acceptance Scenarios**:

1. **Given** the benchmark report in the repository, **When** a user reads it, **Then** they can determine the expected speedup for their population size and problem complexity.
2. **Given** a CUDA-capable machine matching the report's reference hardware, **When** the benchmark suite is executed, **Then** results are within reasonable variance of the published numbers.

---

### Edge Cases

- What happens when the GPU runs out of memory during evaluation of a large population? The system should report a clear error indicating the population size exceeds GPU memory capacity, and suggest reducing population size or falling back to CPU evaluation.
- What happens when a genome has zero connections (empty network)? GPU evaluation should handle degenerate genomes gracefully, returning valid (likely zero or default) outputs without crashing.
- What happens when the GPU becomes unavailable mid-training (e.g., driver crash, thermal throttle, device reset)? The system detects the failure, automatically falls back to CPU evaluation for that generation, logs a warning describing the GPU failure and that CPU fallback is in use, and continues training. This ensures training is not lost due to transient GPU issues.
- What happens when the population contains a mix of genome topologies with vastly different sizes (1 node vs. 500 nodes)? The GPU evaluator should handle heterogeneous populations without correctness issues; throughput may decrease proportionally to maximum genome complexity in the batch but MUST NOT fall below the CPU baseline for the same population.
- What happens when the user's system has a GPU that supports an older or unsupported compute capability? The system should detect incompatible hardware and provide an actionable message stating the minimum required compute capability.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a GPU-accelerated evaluation path that accepts an entire population and returns fitness scores for all genomes via the existing batch evaluation contract.
- **FR-002**: System MUST automatically detect GPU availability at startup and select GPU evaluation when a compatible device is present, unless explicitly disabled by the user.
- **FR-003**: System MUST fall back to CPU evaluation transparently when no compatible GPU is detected, with no code changes required by the user.
- **FR-004**: System MUST provide actionable diagnostic messages when GPU hardware is present but required runtime dependencies are missing, including version requirements and installation guidance.
- **FR-005**: System MUST ensure that CPU and GPU evaluation produce fitness scores within a documented epsilon tolerance for identical inputs and genome structures.
- **FR-006**: System MUST support a best-effort deterministic GPU mode that uses deterministic algorithms where available, documented with clear guarantees and limitations.
- **FR-007**: System MUST allow users to explicitly disable GPU evaluation via configuration, forcing CPU-only execution regardless of hardware availability.
- **FR-008**: System MUST log which evaluation backend (CPU or GPU) was selected at startup, including device name and capability information when GPU is used.
- **FR-009**: System MUST handle GPU memory exhaustion gracefully, reporting a clear error rather than crashing or producing corrupt results.
- **FR-010**: System MUST correctly evaluate populations containing genomes of varying structural complexity (different node and connection counts) on the GPU.
- **FR-011**: System MUST handle degenerate genomes (e.g., zero connections, disconnected nodes) on the GPU without errors.
- **FR-012**: System MUST include a benchmark suite that measures CPU vs. GPU throughput on a representative workload and produces a report suitable for inclusion in the repository.
- **FR-013**: System MUST document GPU prerequisites (minimum compute capability, driver version, runtime dependencies) and troubleshooting steps.
- **FR-014**: System MUST detect and report incompatible GPU hardware (unsupported compute capability) with an actionable error message stating the minimum requirements.
- **FR-015**: System MUST detect GPU evaluation failure during training (e.g., driver crash, device reset, thermal throttle) and automatically fall back to CPU evaluation for the affected generation, logging a warning and continuing training without data loss.

### Key Entities

- **GPU Device**: Represents a detected GPU, including its name, compute capability, available memory, and compatibility status. Used during auto-detection to determine whether GPU evaluation is viable.
- **Evaluation Backend**: The selected evaluation mode (CPU or GPU) for a training run. Determined at startup via auto-detection or explicit configuration. Governs how population fitness is computed each generation.
- **Tolerance Policy**: Defines the acceptable numeric difference between CPU and GPU evaluation results. Includes per-genome epsilon (~1e-4 for fp32 individual score tolerance) and benchmark delta (aggregate outcome tolerance). GPU evaluation uses single-precision (fp32) by default for maximum throughput. Used to validate GPU correctness against the CPU reference.
- **Benchmark Report**: A versioned artifact documenting CPU vs. GPU performance on reference workloads. Includes hardware configuration, population sizes, throughput measurements, and speedup factors.

## Assumptions

- Single-GPU execution only; multi-GPU and distributed execution are explicitly out of scope.
- GPU acceleration applies only to the population evaluation phase (fitness scoring). Mutation, crossover, speciation, and selection remain CPU-only.
- The CUDA programming model is the target GPU platform, using **ILGPU** as the managed .NET GPU interop library. ILGPU compiles C# kernel code to PTX at runtime, eliminating the need for a native CUDA SDK at build time (only the CUDA runtime/driver is required on the target machine). Other GPU platforms (e.g., ROCm, Metal, Vulkan compute) are not in scope.
- The minimum supported CUDA compute capability is **5.0 (Maxwell architecture, ~2014 onward)**, covering GTX 750 Ti and all newer NVIDIA GPUs. This is a firm requirement, not an implementation-time decision.
- Population sizes of 150-10,000 genomes are the target range for demonstrating GPU speedup. Very small populations (< 50) may not benefit from GPU acceleration due to kernel launch overhead.
- The benchmark workload will use a multi-input function approximation problem representative of typical NEAT use cases, not an artificially GPU-friendly workload.
- GPU memory requirements scale with population size and maximum genome complexity. The system will document estimated memory requirements to help users right-size their hardware.
- GPU support is distributed as a separate project/package (e.g., `NeatSharp.Gpu`) with its own ILGPU dependencies. The core `NeatSharp` library has no GPU dependencies. The GPU package registers its evaluator implementation against the core library's evaluation abstractions.

## Non-Goals

- Multi-GPU support or distributed execution across multiple machines.
- GPU-accelerated mutation, crossover, speciation, or selection operators.
- Support for non-CUDA GPU platforms (ROCm, Metal, Vulkan compute, OpenCL).
- Real-time GPU monitoring or adaptive batch sizing based on GPU utilization.
- Training-level determinism guarantees on GPU (only best-effort per-evaluation determinism is in scope).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: GPU evaluation achieves at least 5x throughput improvement over CPU evaluation on a representative benchmark workload with medium-scale population (500-2,000 genomes).
- **SC-002**: The same user training configuration runs successfully on both CPU-only and CUDA-capable machines without any code changes.
- **SC-003**: Per-genome fitness scores from GPU evaluation differ from CPU evaluation by no more than the documented epsilon tolerance (target: 1e-4 for single-precision fp32 GPU operations).
- **SC-004**: Canonical benchmark problems (e.g., XOR) meet their published fitness thresholds when evaluated on GPU, confirming functional correctness.
- **SC-005**: On a machine without a compatible GPU, the system starts and runs training within 1 second of the CPU-only startup time (negligible overhead from GPU detection).
- **SC-006**: When GPU dependencies are missing, the diagnostic message contains enough information for a user to resolve the issue without external support (includes version requirements and installation link).
- **SC-007**: A benchmark report is checked into the repository documenting CPU vs. GPU performance across at least 3 population sizes with reproducible methodology.
