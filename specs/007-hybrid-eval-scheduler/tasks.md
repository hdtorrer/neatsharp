# Tasks: Hybrid Evaluation Scheduler (CPU + GPU Concurrent, Adaptive Partitioning)

**Input**: Design documents from `/specs/007-hybrid-eval-scheduler/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included per constitution (VII. TDD: PASS — "All hybrid code developed with Red-Green-Refactor").

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create directory structure for hybrid scheduler types and tests

- [ ] T001 Create `Scheduling/` directory in `src/NeatSharp.Gpu/` and `tests/NeatSharp.Gpu.Tests/` per implementation plan

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared abstractions, configuration types, and metrics records that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [ ] T002 [P] Create `SplitPolicyType` enum, `HybridOptions`, `AdaptivePidOptions`, and `CostModelOptions` configuration classes in `src/NeatSharp.Gpu/Configuration/HybridOptions.cs` per contracts/HybridOptions.cs and data-model.md
- [ ] T003 [P] Create `IPartitionPolicy` interface and `PartitionResult` readonly record struct in `src/NeatSharp.Gpu/Scheduling/IPartitionPolicy.cs` and `src/NeatSharp.Gpu/Scheduling/PartitionResult.cs` per contracts/IPartitionPolicy.cs and data-model.md
- [ ] T004 [P] Create `SchedulingMetrics` sealed record in `src/NeatSharp.Gpu/Scheduling/SchedulingMetrics.cs` and `FallbackEventInfo` readonly record struct in `src/NeatSharp.Gpu/Scheduling/FallbackEventInfo.cs` per contracts/SchedulingMetrics.cs and data-model.md
- [ ] T005 [P] Create `ISchedulingMetricsReporter` interface in `src/NeatSharp.Gpu/Scheduling/ISchedulingMetricsReporter.cs` per contracts/ISchedulingMetricsReporter.cs
- [ ] T006 Create `HybridOptionsValidator` implementing `IValidateOptions<HybridOptions>` in `src/NeatSharp.Gpu/Configuration/HybridOptionsValidator.cs` — validate StaticGpuFraction [0,1], MinPopulationForSplit [2,100000], GpuReprobeInterval [1,1000], Kp > 0, and sub-object ranges per data-model.md validation rules

**Checkpoint**: All shared types compiled — user story implementation can now begin

---

## Phase 3: User Story 1 — Concurrent CPU+GPU Evaluation with Static Split (Priority: P1) MVP

**Goal**: Partition a population across CPU and GPU backends using a fixed split ratio, dispatch both concurrently via `Task.WhenAll`, and merge fitness results via index-remapped `setFitness` callbacks. Below `MinPopulationForSplit`, delegate to a single backend.

**Independent Test**: Run a known benchmark with static 70/30 split and verify (a) all genomes receive correct fitness, (b) results match per-backend output, (c) concurrent execution occurs.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T007 [P] [US1] Write `HybridOptionsValidatorTests` in `tests/NeatSharp.Gpu.Tests/Configuration/HybridOptionsValidatorTests.cs` — test valid defaults pass, invalid StaticGpuFraction/MinPopulationForSplit/GpuReprobeInterval/Kp=0 fail, boundary values
- [ ] T008 [P] [US1] Write `StaticPartitionPolicyTests` in `tests/NeatSharp.Gpu.Tests/Scheduling/StaticPartitionPolicyTests.cs` — test 70/30 split, 0% GPU (all CPU), 100% GPU (all GPU), single genome, rounding behavior, index correctness, Update is no-op
- [ ] T009 [P] [US1] Write `HybridBatchEvaluatorTests` (core scenarios) in `tests/NeatSharp.Gpu.Tests/Scheduling/HybridBatchEvaluatorTests.cs` — test concurrent dispatch with mock IBatchEvaluators, index-remapped setFitness merges correctly, no ID misalignment/duplication/omission (FR-005), population below MinPopulationForSplit delegates to single backend (FR-014), EnableHybrid=false passthrough (FR-012), metrics are emitted via ISchedulingMetricsReporter (FR-010)

### Implementation for User Story 1

- [ ] T010 [US1] Implement `StaticPartitionPolicy` in `src/NeatSharp.Gpu/Scheduling/StaticPartitionPolicy.cs` — assigns first `(1-gpuFraction)*count` genomes to CPU, rest to GPU per data-model.md StaticPartitionPolicy definition; deterministic, stateless; Update is no-op
- [ ] T011 [US1] Implement `HybridBatchEvaluator` core in `src/NeatSharp.Gpu/Scheduling/HybridBatchEvaluator.cs` — implements `IBatchEvaluator` as decorator wrapping CPU and GPU backends; partitions via `IPartitionPolicy`, dispatches concurrently via `Task.WhenAll` with index-remapped `setFitness` callbacks per R-004; handles `MinPopulationForSplit` threshold (FR-014); `EnableHybrid=false` passthrough (FR-012); creates and emits `SchedulingMetrics` per generation; implements `IDisposable` forwarding to inner evaluators; tracks generation counter
- [ ] T012 [US1] Add `AddNeatSharpHybrid()` extension method to `src/NeatSharp.Gpu/Extensions/ServiceCollectionExtensions.cs` — registers HybridOptions with DataAnnotations + HybridOptionsValidator, resolves existing IBatchEvaluator (GPU) as GPU backend, resolves or creates a CPU IBatchEvaluator adapter from the existing IEvaluationStrategy registration as the CPU backend per R-008 step 3, registers IPartitionPolicy based on SplitPolicyType option, replaces IBatchEvaluator registration with HybridBatchEvaluator decorator wrapping both CPU and GPU backends per R-008 DI pattern

**Checkpoint**: Static hybrid evaluation works end-to-end — genomes split, evaluated concurrently, results merged correctly

---

## Phase 4: User Story 2 — Adaptive Partitioning That Converges to Optimal Split (Priority: P2)

**Goal**: Automatically adjust the CPU/GPU split ratio each generation using a PID controller that targets zero idle-time difference between backends, converging to a stable ratio within 10 generations.

**Independent Test**: Run 20+ generations with adaptive policy and verify split ratio stabilizes (< 5pp variance over 5 consecutive generations) within 10 generations (SC-002).

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T013 [P] [US2] Write `PidControllerTests` in `tests/NeatSharp.Gpu.Tests/Scheduling/PidControllerTests.cs` — test zero error produces no adjustment, positive error (CPU idle) decreases GPU fraction, negative error (GPU idle) increases GPU fraction, output clamped to [0,1], anti-windup stops integral accumulation at saturation per R-001, convergence within 10 steps for synthetic steady-state workload, derivative dampens oscillation
- [ ] T014 [P] [US2] Write `AdaptivePartitionPolicyTests` in `tests/NeatSharp.Gpu.Tests/Scheduling/AdaptivePartitionPolicyTests.cs` — test initial GPU fraction from options, Update feeds PID controller with idle-time error, GPU fraction adjusts after Update, partition uses current PID-controlled fraction, convergence behavior over multiple updates

### Implementation for User Story 2

- [ ] T015 [US2] Implement `PidController` in `src/NeatSharp.Gpu/Scheduling/PidController.cs` — discrete PID with error signal `(gpuIdleTime - cpuIdleTime) / max(cpuTime, gpuTime)` (positive error → GPU has spare capacity → increase GPU fraction), default gains Kp=0.5/Ki=0.1/Kd=0.05, output clamped to [0,1], conditional integration anti-windup per R-001, state reset on GPU failure recovery
- [ ] T016 [US2] Implement `AdaptivePartitionPolicy` in `src/NeatSharp.Gpu/Scheduling/AdaptivePartitionPolicy.cs` — implements `IPartitionPolicy`, contains `PidController` state, performs a count-based split (same algorithm as StaticPartitionPolicy) using the PID-controlled GPU fraction; does not wrap StaticPartitionPolicy (self-contained per data-model.md), Update computes error from SchedulingMetrics latencies and feeds PID controller per R-001

**Checkpoint**: Adaptive partitioning converges to optimal split within 10 generations on steady-state workloads

---

## Phase 5: User Story 3 — GPU Failure Transparent Fallback (Priority: P3)

**Goal**: Detect GPU backend failure mid-generation, transparently reroute all GPU-partition genomes to CPU, log the failure, continue training with CPU-only, and periodically re-probe GPU availability.

**Independent Test**: Inject GPU failure via mock IBatchEvaluator and verify training continues with CPU-only, no fitness scores lost, fallback event logged.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T017 [US3] Write GPU fallback test scenarios in `tests/NeatSharp.Gpu.Tests/Scheduling/HybridBatchEvaluatorTests.cs` — test GPU exception triggers CPU fallback for remaining genomes (FR-007), no genomes lost/duplicated during fallback, fallback warning logged with reason and rerouted count (FR-008), subsequent generations use CPU-only (FR-009), re-probe after N generations restores hybrid mode, re-probe failure continues CPU-only, OperationCanceledException is not caught as GPU failure, GPU failure on first generation (no throughput history) defaults to CPU, no GPU available at startup (GPU backend fails on first initialization) logs warning and operates CPU-only from generation 1 with no error (edge case per spec.md)

### Implementation for User Story 3

- [ ] T018 [US3] Add GPU failure detection, CPU fallback rerouting, and periodic re-probe logic to `HybridBatchEvaluator` in `src/NeatSharp.Gpu/Scheduling/HybridBatchEvaluator.cs` — catch all exceptions from GPU backend except OperationCanceledException per R-003, reroute GPU-partition genomes to CPU backend for current generation, mark GPU unavailable and reset re-probe counter, track `_generationsSinceGpuFailure`, every `GpuReprobeInterval` generations attempt GPU re-initialization, log warning with failure details and genome count rerouted (FR-008), populate FallbackEventInfo in SchedulingMetrics

**Checkpoint**: GPU failures handled transparently — training continues without data loss, GPU re-probed periodically

---

## Phase 6: User Story 4 — Scheduling Metrics and Observability (Priority: P4)

**Goal**: Emit complete per-generation scheduling metrics (genome counts, throughput, latency, split ratio, fallback events, scheduler overhead) via `ISchedulingMetricsReporter` with a default logging implementation.

**Independent Test**: Run a training session and verify all documented metrics are emitted each generation, accessible programmatically and via logs.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T019 [P] [US4] Write `SchedulingMetricsTests` in `tests/NeatSharp.Gpu.Tests/Scheduling/SchedulingMetricsTests.cs` — test all metrics fields populated (genome counts, throughput genomes/sec, latency TimeSpan, split ratio, active policy, scheduler overhead), FallbackEventInfo populated on fallback, metrics immutability, throughput calculation correctness
- [ ] T020 [P] [US4] Write `LoggingMetricsReporterTests` in `tests/NeatSharp.Gpu.Tests/Scheduling/SchedulingMetricsTests.cs` — test metrics summary logged at Information level, fallback events logged at Warning level, null fallback event not logged as warning

### Implementation for User Story 4

- [ ] T021 [US4] Implement `LoggingMetricsReporter` in `src/NeatSharp.Gpu/Scheduling/LoggingMetricsReporter.cs` — implements `ISchedulingMetricsReporter`, logs per-generation summary at Information level (genome counts, throughput, split ratio, overhead), logs fallback events at Warning level with timestamp/reason/rerouted count per data-model.md
- [ ] T022 [US4] Verify and enhance full metrics emission in `HybridBatchEvaluator` in `src/NeatSharp.Gpu/Scheduling/HybridBatchEvaluator.cs` — ensure scheduler overhead timing uses Stopwatch excluding backend evaluation, throughput calculated as genomeCount/latency.TotalSeconds, split ratio reflects active policy's GPU fraction, all FR-010 fields populated, register LoggingMetricsReporter as default in AddNeatSharpHybrid() via TryAddSingleton

**Checkpoint**: All per-generation metrics available and logged — operators can diagnose throughput imbalances and verify adaptive convergence

---

## Phase 7: User Story 5 — Cost-Based Partitioning Using Genome Complexity (Priority: P5)

**Goal**: Route complex genomes (high node/connection count) to GPU and simple genomes to CPU using a weighted linear cost model, improving throughput on populations with high structural diversity.

**Independent Test**: Construct a bimodal-complexity population and compare throughput with cost-based vs. uniform partitioning — cost-based should achieve at least 10% higher throughput (SC-006).

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T023 [US5] Write `CostBasedPartitionPolicyTests` in `tests/NeatSharp.Gpu.Tests/Scheduling/CostBasedPartitionPolicyTests.cs` — test cost formula `alpha*nodeCount + beta*connectionCount` per R-002, genomes sorted by cost descending with highest-cost assigned to GPU, configurable NodeWeight/ConnectionWeight, uniform complexity degrades gracefully to even split, GPU fraction respected, Update is no-op, index mapping correctness

### Implementation for User Story 5

- [ ] T024 [US5] Implement `CostBasedPartitionPolicy` in `src/NeatSharp.Gpu/Scheduling/CostBasedPartitionPolicy.cs` — implements `IPartitionPolicy`, computes `cost = NodeWeight * genome.NodeCount + ConnectionWeight * genome.ConnectionCount` per R-002, sorts genomes by cost descending, assigns highest-cost genomes to GPU up to configured GPU fraction, uses `StaticGpuFraction` from options, Update is no-op

**Checkpoint**: Cost-based partitioning routes complex genomes to GPU, simple to CPU — throughput improvement on diverse populations

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: End-to-end integration tests, quickstart validation, and build verification

- [ ] T025 Write `HybridTrainingIntegrationTests` in `tests/NeatSharp.Gpu.Tests/Integration/HybridTrainingIntegrationTests.cs` — end-to-end test with all three policies (static, adaptive, cost-based), verify CPU determinism preserved when hybrid disabled (SC-003), verify scheduler overhead < 5% of slower backend (FR-018), GPU integration tests gated with `[Trait("Category", "GPU")]`
- [ ] T026 Run quickstart.md validation — verify all code samples from `specs/007-hybrid-eval-scheduler/quickstart.md` compile and execute correctly with the implemented types
- [ ] T027 Build verification — ensure `dotnet build` succeeds for both net8.0 and net9.0 targets with zero warnings, `dotnet test` passes all new tests, no nullable warnings
- [ ] T028 [GPU] Create benchmark report in `specs/007-hybrid-eval-scheduler/benchmark-report.md` — compare hybrid vs. CPU-only vs. GPU-only across 3 population sizes (200, 1000, 5000) and 2 workload profiles (transfer-dominated, compute-dominated) per SC-007; include cost-based vs. uniform partitioning throughput comparison on bimodal-complexity population (50% <10 nodes, 50% >100 nodes) validating SC-006 (≥10% throughput improvement); document methodology, hardware, and reproducible steps

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3-7)**: All depend on Foundational phase completion
  - US1 (Phase 3): Can start after Phase 2 — no dependencies on other stories
  - US2 (Phase 4): Can start after Phase 2 — no dependencies on other stories (standalone PID + policy)
  - US3 (Phase 5): Depends on US1 (extends HybridBatchEvaluator with fallback logic)
  - US4 (Phase 6): Depends on US1 (extends HybridBatchEvaluator with full metrics emission)
  - US5 (Phase 7): Can start after Phase 2 — no dependencies on other stories (standalone policy)
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Foundation only — MVP, can be delivered independently
- **US2 (P2)**: Foundation only — PidController and AdaptivePartitionPolicy are self-contained
- **US3 (P3)**: Depends on US1 — adds fallback behavior to HybridBatchEvaluator
- **US4 (P4)**: Depends on US1 — adds LoggingMetricsReporter and full metrics verification to HybridBatchEvaluator
- **US5 (P5)**: Foundation only — CostBasedPartitionPolicy is self-contained

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD per constitution)
- Models/records before services
- Services before DI registration
- Core implementation before integration

### Parallel Opportunities

- All Foundational tasks (T002-T005) can run in parallel
- All US1 tests (T007-T009) can run in parallel
- All US2 tests (T013-T014) can run in parallel
- US2 and US5 can run in parallel with US1 (different files, no dependencies)
- US4 tests (T019-T020) can run in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests in parallel (TDD - write first, ensure they fail):
Task: "HybridOptionsValidatorTests in tests/NeatSharp.Gpu.Tests/Configuration/HybridOptionsValidatorTests.cs"
Task: "StaticPartitionPolicyTests in tests/NeatSharp.Gpu.Tests/Scheduling/StaticPartitionPolicyTests.cs"
Task: "HybridBatchEvaluatorTests in tests/NeatSharp.Gpu.Tests/Scheduling/HybridBatchEvaluatorTests.cs"

# Then implement sequentially:
Task: "StaticPartitionPolicy in src/NeatSharp.Gpu/Scheduling/StaticPartitionPolicy.cs"
Task: "HybridBatchEvaluator in src/NeatSharp.Gpu/Scheduling/HybridBatchEvaluator.cs"
Task: "AddNeatSharpHybrid() in src/NeatSharp.Gpu/Extensions/ServiceCollectionExtensions.cs"
```

## Parallel Example: Foundational Phase

```bash
# All foundational types can be created in parallel (different files, no dependencies):
Task: "HybridOptions + SplitPolicyType in src/NeatSharp.Gpu/Configuration/HybridOptions.cs"
Task: "IPartitionPolicy + PartitionResult in src/NeatSharp.Gpu/Scheduling/"
Task: "SchedulingMetrics + FallbackEventInfo in src/NeatSharp.Gpu/Scheduling/"
Task: "ISchedulingMetricsReporter in src/NeatSharp.Gpu/Scheduling/"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1 — Static split + concurrent dispatch + result merge
4. **STOP and VALIDATE**: Test US1 independently — verify correct fitness scores, no misalignment, concurrent execution
5. Deploy/demo if ready — hybrid evaluation works with static split

### Incremental Delivery

1. Complete Setup + Foundational -> Foundation ready
2. Add US1 (Static Split) -> Test independently -> MVP!
3. Add US2 (Adaptive PID) -> Test convergence -> Hands-free optimization
4. Add US3 (GPU Fallback) -> Test reliability -> Production-safe
5. Add US4 (Metrics) -> Test observability -> Diagnosable
6. Add US5 (Cost-Based) -> Test throughput gains -> Optimized for diverse populations
7. Each story adds value without breaking previous stories

### Parallel Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 (core hybrid evaluator)
   - Developer B: US2 (PID controller + adaptive policy) — can run in parallel since PidController and AdaptivePartitionPolicy are self-contained
   - Developer C: US5 (cost-based policy) — can run in parallel since CostBasedPartitionPolicy is self-contained
3. After US1 completes:
   - Developer A: US3 (GPU fallback — extends HybridBatchEvaluator)
   - Developer B: US4 (metrics — extends HybridBatchEvaluator)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Tests MUST fail before implementing (TDD per constitution VII)
- All new code in `NeatSharp.Gpu` project — no changes to core `NeatSharp`
- GPU integration tests gated with `[Trait("Category", "GPU")]`
- Test naming: `{Method}_{Scenario}_{ExpectedResult}` per testing conventions
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
