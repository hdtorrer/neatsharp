# Tasks: Parallel CPU Evaluation

**Input**: Design documents from `/specs/009-parallel-cpu-eval/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included — spec.md references TDD (Constitution Principle VII) and the plan specifies test files.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup

**Purpose**: Make `EvaluationStrategy` partial-class-ready and add the `MaxDegreeOfParallelism` configuration property

- [ ] T001 Add `partial` modifier to existing `EvaluationStrategy` static class in `src/NeatSharp/Evaluation/EvaluationStrategy.cs`
- [ ] T002 Add `MaxDegreeOfParallelism` property (`int?`, default `null`) to `src/NeatSharp/Configuration/EvaluationOptions.cs` with XML doc comment per contracts/api-changes.md

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Validation logic and factory method overloads that ALL parallel adapters depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T003 Add validation for `MaxDegreeOfParallelism` (must be `null` or `≥ 1`; throw `ArgumentOutOfRangeException` for `≤ 0`) in the new factory overloads in `src/NeatSharp/Evaluation/EvaluationStrategy.cs`
- [ ] T004 Add `EvaluationOptions`-accepting factory overloads (`FromFunction` sync, `FromFunction` async, `FromEnvironment`) to `src/NeatSharp/Evaluation/EvaluationStrategy.cs` — when `MaxDegreeOfParallelism == 1` or options not provided, return existing sequential adapters; when `null` or `> 1`, return parallel adapters (stub with `throw new NotImplementedException()` until Phase 3)
- [ ] T005 Create partial class file `src/NeatSharp/Evaluation/EvaluationStrategy.Parallel.cs` with the `EvaluationStrategy` partial class shell and shared helper: lock-wrapped `setFitness` callback wrapper and `ConcurrentBag<(int, Exception)>` error-to-`EvaluationException` conversion method

**Checkpoint**: Factory overloads compile, existing tests still pass, parallel adapter file exists as shell

---

## Phase 3: User Story 1 — Parallel Fitness Evaluation (Priority: P1) 🎯 MVP

**Goal**: Evaluate genomes across all CPU cores using `Parallel.ForEachAsync` for synchronous fitness functions, achieving near-linear speedup

**Independent Test**: Run a population evaluation with a CPU-bound sync fitness function; verify wall-clock speedup and identical fitness scores vs sequential

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T006 [P] [US1] Create `tests/NeatSharp.Tests/Evaluation/ParallelSyncFunctionAdapterTests.cs` with tests: (1) all genomes receive correct fitness scores matching sequential results, (2) error accumulation — some genomes throw, remaining get correct scores and `EvaluationException` contains all failures, (3) `ErrorMode.AssignFitness` assigns default fitness to failed genomes, (4) cancellation token is respected — already-completed scores preserved, (5) population size smaller than `MaxDegreeOfParallelism` — uses only as many threads as genomes, (6) all genomes throw — aggregated error contains all failures and all genomes receive default fitness if error mode permits

### Implementation for User Story 1

- [ ] T007 [US1] Implement `ParallelSyncFunctionAdapter` nested class in `src/NeatSharp/Evaluation/EvaluationStrategy.Parallel.cs` — use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism` from options, `ConcurrentBag` error accumulation, lock-wrapped `setFitness` callback, cancellation token propagation
- [ ] T008 [US1] Wire `FromFunction(Func<IGenome, double>, EvaluationOptions)` in `src/NeatSharp/Evaluation/EvaluationStrategy.cs` to return `ParallelSyncFunctionAdapter` when `MaxDegreeOfParallelism != 1` (replace `NotImplementedException` stub from T004)

**Checkpoint**: Sync parallel evaluation works end-to-end. All T006 tests pass. Existing sequential tests unaffected.

---

## Phase 4: User Story 2 — Configurable Degree of Parallelism (Priority: P2)

**Goal**: Users control concurrency via `MaxDegreeOfParallelism` — exact core count, all cores (null), or sequential (1)

**Independent Test**: Set `MaxDegreeOfParallelism` to a specific value and verify concurrency is bounded to that value

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T009 [P] [US2] Add tests to `tests/NeatSharp.Tests/Evaluation/ParallelSyncFunctionAdapterTests.cs`: (1) `MaxDegreeOfParallelism = 2` limits concurrent evaluations to 2 (use a `SemaphoreSlim`-based fitness function that tracks max concurrency), (2) `MaxDegreeOfParallelism = null` uses all cores (verify evaluations run), (3) `MaxDegreeOfParallelism = 1` produces sequential behavior (factory returns sequential adapter)
- [ ] T010 [P] [US2] Add validation tests to `tests/NeatSharp.Tests/Evaluation/EvaluationStrategyTests.cs`: (1) `MaxDegreeOfParallelism = 0` throws `ArgumentOutOfRangeException`, (2) `MaxDegreeOfParallelism = -1` throws `ArgumentOutOfRangeException`

### Implementation for User Story 2

- [ ] T011 [US2] Verify `MaxDegreeOfParallelism` flows correctly through `ParallelSyncFunctionAdapter` to `ParallelOptions.MaxDegreeOfParallelism` in `src/NeatSharp/Evaluation/EvaluationStrategy.Parallel.cs` — resolve `null` to `Environment.ProcessorCount` at adapter construction

**Checkpoint**: Concurrency control works as specified. Validation rejects invalid values.

---

## Phase 5: User Story 3 — Parallel Async Evaluation with Error Resilience (Priority: P2)

**Goal**: Async fitness functions evaluate in parallel using `SemaphoreSlim`-bounded `Task.WhenAll`, with the same error-handling contract as sequential. This phase is the primary vehicle for validating the error resilience contract (spec US3), though error resilience is a cross-cutting concern tested across all adapters.

**Independent Test**: Provide an async fitness function that throws for specific genomes, run in parallel, verify remaining genomes get correct scores and aggregated error reported

> **Note — Error Resilience**: Error resilience is a cross-cutting concern validated across all adapters: T006 (sync, Phase 3), T012 (async, Phase 5), and T015 (environment, Phase 6). It is not scoped to this phase alone.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T012 [P] [US3] Create `tests/NeatSharp.Tests/Evaluation/ParallelAsyncFunctionAdapterTests.cs` with tests: (1) all genomes receive correct fitness, (2) error accumulation with partial failures, (3) `ErrorMode.AssignFitness` for failed genomes, (4) cancellation preserves completed scores, (5) `MaxDegreeOfParallelism` bounds concurrent async evaluations, (6) population size smaller than `MaxDegreeOfParallelism` — completes without error, (7) all genomes throw — aggregated error contains all failures and default fitness assigned if error mode permits

### Implementation for User Story 3

- [ ] T013 [US3] Implement `ParallelAsyncFunctionAdapter` nested class in `src/NeatSharp/Evaluation/EvaluationStrategy.Parallel.cs` — use `SemaphoreSlim(maxDegreeOfParallelism)` + `Task.WhenAll`, `ConcurrentBag` error accumulation, lock-wrapped callback, cancellation token propagation
- [ ] T014 [US3] Wire `FromFunction(Func<IGenome, CancellationToken, Task<double>>, EvaluationOptions)` in `src/NeatSharp/Evaluation/EvaluationStrategy.cs` to return `ParallelAsyncFunctionAdapter` when `MaxDegreeOfParallelism != 1`

**Checkpoint**: Async parallel evaluation works with full error resilience. Both sync and async paths handle failures identically.

---

## Phase 6: User Story 4 — Parallel Environment-Based Evaluation (Priority: P3)

**Goal**: Environment evaluators (e.g., Cart-Pole) evaluate multiple genomes concurrently, each running its own episodes in parallel

**Independent Test**: Run an environment evaluation with parallel mode; verify concurrent episode execution and correct fitness scores

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T015 [P] [US4] Create `tests/NeatSharp.Tests/Evaluation/ParallelEnvironmentAdapterTests.cs` with tests: (1) all genomes receive correct fitness matching sequential, (2) error accumulation with partial failures, (3) `MaxDegreeOfParallelism` bounds concurrent environment evaluations, (4) cancellation support, (5) population size smaller than `MaxDegreeOfParallelism` — completes without error, (6) all genomes throw — aggregated error contains all failures and default fitness assigned if error mode permits

### Implementation for User Story 4

- [ ] T016 [US4] Implement `ParallelEnvironmentAdapter` nested class in `src/NeatSharp/Evaluation/EvaluationStrategy.Parallel.cs` — use `Parallel.ForEachAsync` (same pattern as sync adapter but calling `IEnvironmentEvaluator.EvaluateAsync`), `ConcurrentBag` error accumulation, lock-wrapped callback
- [ ] T017 [US4] Wire `FromEnvironment(IEnvironmentEvaluator, EvaluationOptions)` in `src/NeatSharp/Evaluation/EvaluationStrategy.cs` to return `ParallelEnvironmentAdapter` when `MaxDegreeOfParallelism != 1`

**Checkpoint**: All three parallel adapters (sync, async, environment) fully functional and tested.

---

## Phase 7: User Story 5 — Integration with Hybrid Evaluator (Priority: P3)

**Goal**: CPU batch in the hybrid evaluator automatically uses multi-core evaluation when `MaxDegreeOfParallelism != 1`

**Independent Test**: Run a hybrid evaluation and verify the CPU-assigned genomes evaluate in parallel within their batch

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T018 [US5] Add integration test to `tests/NeatSharp.Tests/Evaluation/EvaluationStrategyTests.cs` verifying that `EvaluationStrategyBatchAdapter` delegates to a parallel `IEvaluationStrategy` when constructed with parallel options, and genomes evaluate correctly

### Implementation for User Story 5

- [ ] T019 [US5] Verify no code changes needed — `EvaluationStrategyBatchAdapter` already delegates to `IEvaluationStrategy.EvaluatePopulationAsync()`. Confirm by running the integration test from T018. If DI registration in `AddNeatSharp()` needs to flow `EvaluationOptions` to the strategy factory, update `src/NeatSharp/Configuration/ServiceCollectionExtensions.cs` (or equivalent DI wiring)

**Checkpoint**: Hybrid evaluator CPU batch parallelized. End-to-end flow validated.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, code quality, and final validation

- [ ] T020 [P] Add XML doc comments to all three parallel adapter classes and factory overloads documenting thread-safety requirements for user-provided fitness functions (FR-013) in `src/NeatSharp/Evaluation/EvaluationStrategy.Parallel.cs` and `src/NeatSharp/Evaluation/EvaluationStrategy.cs`
- [ ] T021 [P] Run `dotnet format NeatSharp.sln --verify-no-changes --severity warn` and fix any formatting issues
- [ ] T022 Run full test suite `dotnet test NeatSharp.sln --filter "Category!=GPU"` — all existing + new tests must pass
- [ ] T023 Add a compilable smoke test to `tests/NeatSharp.Tests/Evaluation/` that exercises the quickstart.md code examples against the implemented API — verifies factory overloads, option configuration, and parallel evaluation signatures compile and run correctly
- [ ] T024 [P] Add a `ParallelEvaluationBenchmark` class to `benchmarks/NeatSharp.Benchmarks/` comparing parallel vs. sequential sync evaluation for a CPU-bound fitness function with population sizes of 100 and 1000 genomes. Run and record results to validate SC-001 (wall-clock time ≤ 2× sequential_time / N). Constitution Principle III: benchmark evidence required for performance-sensitive changes.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Phase 2
- **US2 (Phase 4)**: Depends on Phase 3 (tests validate concurrency behavior added in US1)
- **US3 (Phase 5)**: Depends on Phase 2 (async adapter is independent of sync adapter)
- **US4 (Phase 6)**: Depends on Phase 2 (environment adapter is independent of sync/async)
- **US5 (Phase 7)**: Depends on Phase 2 (integration test; benefits from all adapters existing)
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: After Phase 2 — no dependencies on other stories
- **US2 (P2)**: After US1 — tests verify concurrency bounding on the sync adapter built in US1
- **US3 (P2)**: After Phase 2 — async adapter is independent; can run in parallel with US1
- **US4 (P3)**: After Phase 2 — environment adapter is independent; can run in parallel with US1/US3
- **US5 (P3)**: After Phase 2 — integration test only; can run after any adapter is wired

### Parallel Opportunities

- **Phase 2**: T003, T004, T005 are sequential (T004 depends on T003 validation, T005 is the shell file)
- **Phase 3+**: US3 (async) and US4 (environment) can run in parallel with US1 (sync) since they touch different adapter classes
- **Within each story**: Test tasks marked [P] can run in parallel with tests from other stories
- **Phase 8**: T020 and T021 can run in parallel

---

## Parallel Example: After Phase 2 Completes

```
# These three user stories can proceed in parallel (different adapter files):

Stream A (US1 - sync):   T006 → T007 → T008
Stream B (US3 - async):  T012 → T013 → T014
Stream C (US4 - env):    T015 → T016 → T017

# US2 follows US1 (tests concurrency on sync adapter):
Stream A continued:       T009, T010 → T011

# US5 can start after Phase 2 (integration test):
Stream D (US5):           T018 → T019
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: Foundational (T003–T005)
3. Complete Phase 3: User Story 1 — Parallel Sync Evaluation (T006–T008)
4. **STOP and VALIDATE**: Run tests, verify speedup with a CPU-bound fitness function
5. This alone delivers the core value — multi-core sync evaluation

### Incremental Delivery

1. Setup + Foundational → Factory overloads compile, sequential behavior unchanged
2. US1 (sync parallel) → Core speedup for sync fitness functions (MVP!)
3. US2 (configurable parallelism) → Power users can tune concurrency
4. US3 (async parallel) → Async fitness functions also parallelized
5. US4 (environment parallel) → Cart-Pole and episode-based evaluators parallelized
6. US5 (hybrid integration) → Full hybrid evaluator CPU batch parallelized
7. Polish → Documentation, formatting, final validation

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- No new external dependencies — all parallelism uses in-box .NET BCL types
