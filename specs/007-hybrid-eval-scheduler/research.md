# Research: Hybrid Evaluation Scheduler

**Feature**: 007-hybrid-eval-scheduler | **Date**: 2026-02-16

## R-001: PID Controller Design for Adaptive Split Policy

**Decision**: Use a discrete PID controller where the process variable is the normalized idle-time difference between backends, the setpoint is 0 (both finish simultaneously), and the control output is a delta applied to the GPU fraction. Default gains: Kp=0.5, Ki=0.1, Kd=0.05.

**Rationale**: The system samples once per generation (discrete-time). The error signal is:

```
error = (gpuIdleTime - cpuIdleTime) / max(cpuTime, gpuTime)
```

Normalized to [-1, 1]. Positive error means GPU finished first (GPU has spare capacity — send more genomes to GPU). Negative error means CPU finished first (GPU is the bottleneck — send fewer genomes to GPU). The control output adjusts the GPU fraction for the next generation:

```
delta = Kp * error + Ki * integral + Kd * (error - prevError)
gpuFraction = clamp(gpuFraction + delta, 0.0, 1.0)
```

Default gains target convergence within 5-10 generations (SC-002 requires stable within 10):
- **Kp = 0.5**: A fully one-sided imbalance adjusts by 50% in one step — aggressive enough to converge quickly, damped enough to avoid severe overshoot. Follows Ziegler-Nichols heuristic of starting at ~0.5 of estimated critical gain.
- **Ki = 0.1**: Small integral term eliminates persistent steady-state offset (e.g., when workload characteristics drift as genomes evolve).
- **Kd = 0.05**: Minimal derivative gain dampens oscillations without amplifying measurement noise.

**Anti-windup**: Output clamping with conditional integration — when the GPU fraction would be clamped at 0.0 or 1.0 AND the integral term and error have the same sign, stop accumulating the integral. This prevents the integral from winding up when the output is saturated.

**Alternatives Considered**:
- **Exponential moving average (no PID)**: Simpler but lacks derivative damping and integral correction, leading to slower convergence or persistent offset.
- **Back-calculation anti-windup**: More complex, uses a back-calculation coefficient Kb. Overkill for a discrete system with one update per generation.
- **Model predictive control**: Too complex; the system model is not well-characterized enough to justify the implementation cost.

---

## R-002: Cost-Based Partitioning Heuristic

**Decision**: Use a weighted linear cost model: `cost(g) = alpha * nodeCount + beta * connectionCount` with configurable alpha and beta (defaults: alpha=1.0, beta=1.0).

**Rationale**: For feed-forward NEAT networks, the dominant cost factors are:
- **Node count**: Each hidden/output node requires an activation function evaluation per test case (e.g., `exp()` for sigmoid).
- **Connection count**: Each enabled connection requires one multiply-accumulate (MAC) operation per test case.

Both `NodeCount` and `ConnectionCount` are already exposed on `IGenome` (no additional genome introspection needed). Equal default weights provide a reasonable starting point for typical NEAT populations.

**Partitioning logic**: Sort genomes by cost descending. Assign the highest-cost genomes to GPU (benefits from parallelism on large networks) and lowest-cost genomes to CPU (lower per-genome overhead for small networks). The split point is determined by the target GPU fraction from the active policy.

**Alternatives Considered**:
- **`cost = connectionCount` only**: Underestimates cost of deep networks with many activation evaluations but few connections.
- **`cost = nodeCount * connectionCount`**: Overestimates cost for genomes with many disconnected nodes.
- **Profiled cost model (measure actual wall-clock per genome)**: Most accurate but adds measurement overhead and defeats the purpose of predicting cost before evaluation.

---

## R-003: GPU Failure Detection and Recovery

**Decision**: Catch all exceptions from the GPU backend (except `OperationCanceledException`), dispose the ILGPU context, fall back to CPU, and flag GPU as unavailable for N generations with periodic re-probe.

**Rationale**: CUDA errors fall into two categories:
- **Non-sticky errors** (e.g., OOM): CUDA context remains valid. Subsequent operations may succeed.
- **Sticky errors** (e.g., kernel launch failure, illegal memory access): CUDA context is permanently corrupted. All subsequent calls return the same error.

Since sticky errors cannot be recovered without destroying the context, the safest strategy is to always dispose and recreate. The existing `GpuBatchEvaluator` uses lazy initialization (`_gpuInitialized` flag), so resetting this flag after a failure triggers fresh initialization on the next probe.

**Recovery flow**:
1. GPU backend's `EvaluateAsync` throws → catch in hybrid evaluator
2. Log warning with failure details and genome count rerouted
3. Reroute all GPU-partition genomes to CPU backend for current generation
4. Dispose ILGPU accelerator/context, mark GPU unavailable, reset generation counter
5. Subsequent generations use CPU-only evaluation
6. Every N generations (configurable, default 10), attempt re-probe:
   - Request the GPU evaluator to attempt re-initialization
   - If successful, resume hybrid mode
   - If probe fails, log and continue CPU-only

**Alternatives Considered**:
- **Retry on same context**: Only works for non-sticky errors. Not safe for kernel failures.
- **Separate GPU process**: Adds IPC/serialization complexity for marginal benefit.
- **Immediate re-probe every generation**: Wasteful if failure is persistent; adds overhead to every CPU-only generation.

---

## R-004: Concurrent Dispatch Pattern

**Decision**: Use `Task.WhenAll` with index-remapped `setFitness` callbacks for lock-free result merging.

**Rationale**: The `IBatchEvaluator` contract uses `Action<int, double> setFitness` with the genome's index in the original list. Each backend receives a remapped callback that translates local partition indices back to global indices. Since each backend writes to disjoint index ranges, no locking is needed.

```csharp
// CPU backend gets: setFitness(cpuIndices[localIndex], score)
// GPU backend gets: setFitness(gpuIndices[localIndex], score)
Task cpuTask = _cpuBackend.EvaluateAsync(cpuGenomes, cpuSetFitness, ct);
Task gpuTask = _gpuBackend.EvaluateAsync(gpuGenomes, gpuSetFitness, ct);
await Task.WhenAll(cpuTask, gpuTask).ConfigureAwait(false);
```

`Task.WhenAll` is preferred because it waits for both tasks to complete (or fail) rather than short-circuiting on the first failure, ensuring CPU results are not lost when GPU fails. Individual task status (`gpuTask.IsFaulted`) is inspected to handle GPU-only failure.

**Alternatives Considered**:
- **Sequential dispatch**: Defeats the purpose of hybrid evaluation.
- **`Parallel.ForEachAsync`**: Not applicable — exactly two coarse-grained tasks, not a fine-grained parallel loop.
- **Custom `WhenAllOrFirstCancellation`**: Unnecessary — `CancellationToken` is passed to both backends, and `Task.WhenAll` propagates cancellation.

---

## R-005: Default GPU Re-Probe Interval

**Decision**: Default re-probe interval = 10 generations (configurable via `HybridOptions.GpuReprobeInterval`).

**Rationale**: A NEAT generation typically takes 0.1-10 seconds. At 10 generations, the re-probe happens every ~1-100 seconds of wall-clock time:
- Responsive enough to detect GPU recovery from transient failures (NVIDIA TDR typically recovers within 2-10 seconds).
- Low enough overhead for persistent failures (one Context creation attempt every ~10-100 seconds).
- Aligns with Kubernetes liveness probe defaults (10-second intervals with failure thresholds).

The interval is configurable per FR-009, so users can tune for their generation duration.

**Alternatives Considered**:
- **5 generations**: More responsive but potentially wasteful for very fast workloads (~500ms between probes).
- **20 generations**: Too slow for long-generation workloads (could mean 3+ minutes without GPU).
- **Wall-clock-based interval**: More predictable but requires timer dependency. Generation-based is simpler and aligns with the spec's framing.
- **Exponential backoff**: Worth considering for v2, but a fixed interval is simpler and predictable for v1.

---

## R-006: Minimum Population Threshold for Hybrid Splitting

**Decision**: Default minimum threshold = 50 genomes (configurable via `HybridOptions.MinPopulationForSplit`). Below this threshold, evaluate on whichever single backend is faster based on historical throughput (default to CPU if no history).

**Rationale**: The overhead of hybrid splitting includes:
- Partitioning genomes into two lists
- Dispatching two async tasks and awaiting both
- Index remapping for the setFitness callback
- For GPU: population data flattening and GPU memory transfer

For small populations (<50), this overhead can exceed the time saved by parallelism. The spec explicitly calls out 50 as the suggested default (FR-014, Assumptions section).

**Single-backend selection**: When below threshold, use the backend with higher historical throughput (genomes/sec from the most recent hybrid generation). If no history exists (first generation or never ran in hybrid mode), default to CPU to avoid GPU initialization overhead for trivially small populations.

---

## R-007: Scheduling Metrics Design

**Decision**: A `SchedulingMetrics` record emitted per generation, containing all observability data required by FR-010.

**Metrics structure**:
```
SchedulingMetrics:
  Generation: int
  CpuGenomeCount: int
  GpuGenomeCount: int
  CpuThroughput: double (genomes/sec)
  GpuThroughput: double (genomes/sec)
  CpuLatency: TimeSpan
  GpuLatency: TimeSpan
  SplitRatio: double (GPU fraction, 0.0-1.0)
  ActivePolicy: SplitPolicyType (enum: Static|CostBased|Adaptive)
  FallbackEvent: FallbackEvent? (nullable)
  SchedulerOverhead: TimeSpan
```

**FallbackEvent structure**:
```
FallbackEvent:
  Timestamp: DateTimeOffset
  FailureReason: string
  GenomesRerouted: int
```

**Emission mechanism**: The hybrid evaluator exposes metrics via an `ISchedulingMetricsReporter` interface (injectable). The adaptive policy reads metrics for its PID calculations. External consumers (logging, monitoring) can also inject a reporter.

**Rationale**: This design separates metrics production (in the hybrid evaluator) from consumption (PID controller, logging, user code). The metrics record is immutable after creation, making it safe to pass across threads.

---

## R-008: Integration with Existing DI Registration

**Decision**: New `AddNeatSharpHybrid()` extension method on `IServiceCollection`, following the same pattern as `AddNeatSharpGpu()`. Registers `HybridBatchEvaluator` as the `IBatchEvaluator` implementation, wrapping both CPU and GPU backends.

**Registration flow**:
```csharp
services.AddNeatSharp(options => { ... });          // Core services + CPU evaluator
services.AddNeatSharpGpu(gpu => { ... });           // GPU services + GpuBatchEvaluator
services.AddNeatSharpHybrid(hybrid => { ... });     // Hybrid scheduler wrapping both
```

`AddNeatSharpHybrid()` will:
1. Register `HybridOptions` with validation
2. Resolve existing `IBatchEvaluator` registration (GPU or CPU) as the GPU backend
3. Create a CPU evaluation strategy adapter as the CPU backend
4. Replace the `IBatchEvaluator` registration with `HybridBatchEvaluator`
5. Register partitioning policy implementations
6. Register `ISchedulingMetricsReporter`

**Key DI challenge**: The hybrid evaluator needs both a CPU and GPU `IBatchEvaluator`. Since the GPU evaluator is already registered as `IBatchEvaluator`, the hybrid registration must capture it before replacing. This can be done via a factory delegate that resolves the inner evaluator before wrapping.

**Alternatives Considered**:
- **Keyed services**: .NET 8 supports keyed DI (`[FromKeyedServices("gpu")]`). Clean but requires .NET 8+. Since we multi-target .NET 8+, this is viable. However, keyed services are less discoverable and require consumer awareness. Rejected for simplicity.
- **Named registrations via wrapper types**: Register `CpuBatchEvaluatorWrapper` and `GpuBatchEvaluatorWrapper` as distinct types. Viable but adds boilerplate wrapper types with no behavior. Acceptable as a fallback.
- **Direct construction**: Construct the CPU evaluator inside `AddNeatSharpHybrid()` using `ActivatorUtilities`. Simplest approach, avoids keyed services.

**Chosen approach**: Use factory delegates to capture inner registrations. The `HybridBatchEvaluator` constructor receives the CPU evaluation strategy and GPU batch evaluator as explicit parameters, resolved by the factory.
