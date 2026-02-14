# Tasks: Training Runner + Evaluation Adapters + Reporting

**Input**: Design documents from `/specs/004-training-runner/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: TDD is required per project constitution (VII. TDD — Red-Green-Refactor). All new types include tests in the same PR.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Extend existing configuration to support population initialization

- [ ] T001 Add `InputCount` (default 2, `[Range(1, 10_000)]`) and `OutputCount` (default 1, `[Range(1, 10_000)]`) properties to `NeatSharpOptions` in `src/NeatSharp/Configuration/NeatSharpOptions.cs`. Add corresponding validation tests to `tests/NeatSharp.Tests/Configuration/NeatSharpOptionsTests.cs`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**CRITICAL**: No user story work can begin until this phase is complete

- [ ] T002 [P] Create `IPopulationFactory` interface in `src/NeatSharp/Evolution/IPopulationFactory.cs` per contracts/IPopulationFactory.cs — single method `CreateInitialPopulation(int populationSize, int inputCount, int outputCount, Random random, IInnovationTracker tracker)` returning `IReadOnlyList<Genome>`
- [ ] T003 [P] Implement `TrainingLog` source-generated `[LoggerMessage]` static partial class in `src/NeatSharp/Evolution/TrainingLog.cs` per contracts/TrainingLog.cs — six events: GenerationCompleted (1001, Info), NewBestFitness (1002, Info), SpeciesExtinct (1003, Warning), StagnationDetected (1004, Warning), RunCompleted (1005, Info), EvaluationFailed (1006, Warning)
- [ ] T004 Write `PopulationFactory` unit tests (TDD — must fail initially) in `tests/NeatSharp.Tests/Evolution/PopulationFactoryTests.cs`. Test: correct genome count, node layout (inputs 0..I-1, bias I, outputs I+1..I+O), full connectivity (input+bias → output), innovation numbers via tracker dedup, randomized weights within `[WeightMinValue, WeightMaxValue]`, deterministic output with same seed
- [ ] T005 Implement `PopulationFactory` in `src/NeatSharp/Evolution/PopulationFactory.cs`. Depends on `IOptions<NeatSharpOptions>` for weight bounds. Creates minimal-topology genomes: `inputCount` Input nodes + 1 Bias node + `outputCount` Output nodes, fully connected (input+bias → output), weights randomized via provided `Random`, innovation numbers assigned via tracker. All genomes share identical topology with different weights (per R-001, R-002)

**Checkpoint**: Foundation ready — user story implementation can now begin

---

## Phase 3: User Story 1 — Solve XOR with Simple Fitness Function (Priority: P1) MVP

**Goal**: Complete end-to-end NEAT training loop that evolves a champion genome solving XOR within 150 generations

**Independent Test**: Run XOR fitness function with fixed seed, verify champion produces correct XOR outputs for all four input combinations

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (TDD)**

- [ ] T006a [P] [US1] Write `NeatEvolver` core loop unit tests in `tests/NeatSharp.Tests/Evolution/NeatEvolverTests.cs`. Test with mocked/test-double dependencies. Cover: (a) generation loop executes evaluate → speciate → reproduce in correct order, (b) stops at `MaxGenerations`, (c) stops when `FitnessTarget` met, (d) stops when all species simultaneously stagnant (run-level stagnation per R-004), (e) champion tracks highest fitness across all generations with correct `Generation` discovered (FR-016, FR-020)
- [ ] T006b [P] [US1] Write `NeatEvolver` cancellation and determinism unit tests in `tests/NeatSharp.Tests/Evolution/NeatEvolverTests.cs`. Cover: (f) cancellation returns partial result with `WasCancelled=true` (FR-004), (g) cancellation before any generation completes returns valid result, (h) deterministic reproducibility — same seed produces identical results (FR-005), (i) `IInnovationTracker.NextGeneration()` called between generations (FR-006)
- [ ] T006c [P] [US1] Write `NeatEvolver` error handling and edge case unit tests in `tests/NeatSharp.Tests/Evolution/NeatEvolverTests.cs`. Cover: (j) evaluation failure assigns zero fitness and continues (FR-021), (k) complexity limits enforced on offspring — over-limit genomes replaced with un-mutated parent clone (FR-007, R-007), (l) preserves fittest species when stagnation would eliminate all (FR-022), (m) zero-fitness population proceeds without crash, (n) single-species population continues normally, (o) stopping on generation 0 returns valid result
- [ ] T007 [P] [US1] Write XOR end-to-end integration tests in `tests/NeatSharp.Tests/Examples/XorExampleTests.cs`. Test: (a) XOR solved within 150 generations using fixed seed (SC-001, FR-025), (b) champion produces correct outputs for all four XOR cases within error tolerance, (c) two runs with same seed produce identical results (SC-003), (d) run stops at `MaxGenerations` if fitness target not reached and returns best genome found

### Implementation for User Story 1

- [ ] T008a [US1] Implement `NeatEvolver` core loop in `src/NeatSharp/Evolution/NeatEvolver.cs`. Constructor injects: `IOptions<NeatSharpOptions>`, `IPopulationFactory`, `INetworkBuilder`, `ISpeciationStrategy`, `ReproductionOrchestrator`, `IInnovationTracker`, `ILogger<NeatEvolver>`. `RunAsync(IEvaluationStrategy, CancellationToken)` implements: (1) resolve seed from `options.Seed ?? Random.Shared.Next()`, create seeded `Random`, (2) create initial population via `IPopulationFactory`, (3) call `tracker.NextGeneration()` after init, (4) generation loop: build phenotypes via `INetworkBuilder.Build()` → evaluate via `IEvaluationStrategy.EvaluatePopulationAsync()` → track champion (compare best this gen vs running best) → speciate via `ISpeciationStrategy.Speciate()` → check stopping criteria (max gens, fitness target, all-species stagnant) → if stopping criterion met: break → reproduce via `ReproductionOrchestrator.Reproduce()` → `tracker.NextGeneration()` → advance generation counter, (5) build `EvolutionResult` with champion (`IGenome` via `INetworkBuilder.Build()`), `PopulationSnapshot` from final species, `RunHistory` (empty generations list, total generation count), seed, `WasCancelled` flag. Must pass T006a tests.
- [ ] T008b [US1] Add cancellation and determinism support to `NeatEvolver.RunAsync` in `src/NeatSharp/Evolution/NeatEvolver.cs`. Add cancellation token check at generation boundary (top of loop) — if cancelled, break and return result with `WasCancelled=true`. Handle cancellation before any generation completes (return valid result with no champion data). Ensure deterministic PRNG consumption order. Must pass T006b tests.
- [ ] T008c [US1] Add error handling and edge cases to `NeatEvolver.RunAsync` in `src/NeatSharp/Evolution/NeatEvolver.cs`. Add try-catch for individual genome evaluation failures (assign 0.0 fitness, log via `TrainingLog.EvaluationFailed`). Enforce complexity limits on offspring (replace over-limit genomes with un-mutated parent clone). Preserve fittest species when stagnation would eliminate all. Must pass T006c tests.
- [ ] T009 [US1] Update DI registration in `src/NeatSharp/Extensions/ServiceCollectionExtensions.cs` — replace `NeatEvolverStub` with `NeatEvolver` (scoped), add `IPopulationFactory → PopulationFactory` (scoped), remove `NeatEvolverStub` private class. Update corresponding tests in `tests/NeatSharp.Tests/Extensions/ServiceCollectionExtensionsTests.cs`

**Checkpoint**: At this point, the library can solve XOR end-to-end. Core training loop is functional. This is the MVP.

---

## Phase 4: User Story 2 — Monitor Training Progress and Analyze Results (Priority: P2)

**Goal**: Per-generation metrics collection with zero-overhead disable, structured logging for key training events, human-readable run summary

**Independent Test**: Run any training scenario and verify per-generation statistics are recorded accurately; run with metrics disabled and verify no `GenerationStatistics` objects allocated

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (TDD)**

- [ ] T010 [US2] Write metrics collection, zero-overhead, and structured logging verification tests in `tests/NeatSharp.Tests/Evolution/NeatEvolverTests.cs`. Test: (a) `GenerationStatistics` recorded each generation when `EnableMetrics=true` — verify generation number, best fitness, average fitness, species count, species sizes (member count per species matching actual species membership), complexity statistics (avg nodes, avg connections), timing breakdown (evaluation, reproduction, speciation) (FR-015, SC-004), (b) `RunHistory.Generations` is empty and no `GenerationStatistics` allocated when `EnableMetrics=false` (FR-019, SC-005), (c) `RunHistory.TotalGenerations` correct regardless of metrics setting, (d) structured log events emitted for: generation completed, new best fitness, species extinction, stagnation detected, run completed, evaluation failure — use mock `ILogger` to verify (FR-018, SC-006), (e) human-readable summary via `IRunReporter.GenerateSummary()` contains champion fitness, champion generation, total generations, species count, seed (FR-017)

### Implementation for User Story 2

- [ ] T011 [US2] Add conditional metrics collection and structured logging to `NeatEvolver.RunAsync` in `src/NeatSharp/Evolution/NeatEvolver.cs`. When `EnableMetrics=true`: wrap evaluation, speciation, and reproduction phases with `Stopwatch` timing; after each generation, construct `GenerationStatistics` from current population state (fitness distribution, species count, avg nodes/connections, timing); append to history list. When `EnableMetrics=false`: skip all `Stopwatch` and `GenerationStatistics` allocation (zero overhead per FR-019). Add `TrainingLog.*` calls at appropriate points: `GenerationCompleted` after each generation, `NewBestFitness` when champion updates, `SpeciesExtinct` when species removed during speciation, `StagnationDetected` when species stagnation counter exceeds threshold, `RunCompleted` at loop exit, `EvaluationFailed` on caught evaluation exceptions. Update `RunHistory` construction to use populated generations list when metrics enabled.

**Checkpoint**: At this point, training runs produce full observability — per-generation statistics, structured logs, and human-readable summaries

---

## Phase 5: User Story 3 — Solve a Function Approximation Problem (Priority: P3)

**Goal**: Prove the library generalizes beyond XOR by evolving a network that approximates sin(x) over [0, 2pi]

**Independent Test**: Run sine fitness function with fixed seed, verify champion's MSE is below threshold

### Tests for User Story 3

- [ ] T012 [US3] Write sine wave function approximation end-to-end tests in `tests/NeatSharp.Tests/Examples/FunctionApproximationExampleTests.cs`. Fitness function: `1.0 / (1.0 + MSE)` over 20 evenly-spaced sample points in [0, 2pi] with normalized inputs and outputs per quickstart.md and R-011. Test: (a) champion fitness meets threshold within 500 generations using fixed seed (SC-002, FR-024, FR-025), (b) result metrics show progressive fitness improvement across generations (SC-004), (c) deterministic — same seed produces same result

**Checkpoint**: Library proven on two distinct problem classes (discrete classification + continuous regression)

---

## Phase 6: User Story 4 — Train Using Environment-Based Evaluation (Priority: P4)

**Goal**: Validate environment-based (multi-step episodic) evaluation works through the training loop

**Independent Test**: Run with a mock environment evaluator that scores genomes over multiple steps, verify fitness reflects environment performance

### Tests for User Story 4

- [ ] T013 [US4] Write environment evaluator integration tests in `tests/NeatSharp.Tests/Evolution/NeatEvolverTests.cs`. Create a mock `IEnvironmentEvaluator` that runs a fixed number of steps, scores genome outputs, and returns cumulative reward. Wrap via `EvaluationStrategy.FromEnvironment()`. Test: (a) training loop evaluates each genome through the environment and uses returned score as fitness, (b) champion fitness reflects actual environment performance, (c) run completes successfully with valid `EvolutionResult`

**Checkpoint**: Environment-based evaluation pattern (reinforcement-learning-style) validated

---

## Phase 7: User Story 5 — Evaluate Candidates in Batch (Priority: P5)

**Goal**: Validate batch evaluation works through the training loop

**Independent Test**: Run with a batch evaluator that receives full population and assigns scores, verify all genomes receive fitness

### Tests for User Story 5

- [ ] T014 [US5] Write batch evaluator integration tests in `tests/NeatSharp.Tests/Evolution/NeatEvolverTests.cs`. Create a mock `IBatchEvaluator` that receives all genomes and assigns scores. Wrap via `EvaluationStrategy.FromBatch()`. Test: (a) batch evaluator receives full population in single call, (b) all genomes get assigned fitness scores from evaluator, (c) training loop proceeds with speciation and reproduction using batch-assigned scores, (d) run completes successfully with valid `EvolutionResult`

**Checkpoint**: All three evaluation patterns (simple function, environment, batch) validated

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final validation across all user stories

- [ ] T015 Create `tests/NeatSharp.Tests/Examples/QuickstartValidationTests.cs` with one test per quickstart.md snippet. Tests: (a) XOR example — champion fitness >= 3.9, run completes within 150 generations, (b) Sine approximation example — champion fitness >= 0.95, run completes within 500 generations, (c) Cancellation example — `WasCancelled=true` when token fires before completion, champion fitness returned, (d) Batch evaluation example — all genomes receive fitness scores, run completes with valid result, (e) Environment evaluation example — mock environment runs multi-step episodes, champion fitness reflects environment performance, (f) Metrics access example — `result.History.Generations` is non-empty, each entry has expected fields (Generation, BestFitness, AverageFitness, SpeciesCount, SpeciesSizes, Complexity, Timing)
- [ ] T016 Verify full test suite passes on both `net8.0` and `net9.0` target frameworks via `dotnet test`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup (Phase 1) — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational (Phase 2) — delivers MVP
- **User Story 2 (Phase 4)**: Depends on User Story 1 (Phase 3) — adds observability to existing NeatEvolver
- **User Stories 3, 4, 5 (Phases 5, 6, 7)**: Each depends on User Story 1 (Phase 3) — can proceed in parallel with each other and with US2
- **Polish (Phase 8)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Depends on Foundational phase only — delivers the complete training loop (MVP)
- **User Story 2 (P2)**: Depends on US1 — modifies NeatEvolver to add metrics and logging
- **User Story 3 (P3)**: Depends on US1 — test-only, no source changes. Can run in parallel with US2, US4, US5
- **User Story 4 (P4)**: Depends on US1 — test-only, no source changes. Can run in parallel with US2, US3, US5
- **User Story 5 (P5)**: Depends on US1 — test-only, no source changes. Can run in parallel with US2, US3, US4

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD)
- Interface before implementation
- Implementation before DI registration
- Core implementation before integration

### Parallel Opportunities

- T002 and T003 can run in parallel (Phase 2 — different files)
- T006a, T006b, T006c, and T007 can run in parallel (Phase 3 tests — same file but independent test classes/groups)
- T008a → T008b → T008c must be sequential (incremental implementation in same file)
- Phases 5, 6, 7 (US3, US4, US5) can all proceed in parallel after Phase 3 completes
- Phase 4 (US2) can proceed in parallel with Phases 5, 6, 7 (US2 modifies NeatEvolver source; US3/4/5 only add test files)

---

## Parallel Example: After Phase 3 (MVP) Completes

```text
# All four of these can proceed simultaneously:

Thread A (US2): T010 → T011 (metrics + logging in NeatEvolver.cs + GenerationStatistics.cs)
Thread B (US3): T012 (FunctionApproximationExampleTests.cs — test only)
Thread C (US4): T013 (NeatEvolverTests.cs — add environment eval tests)
Thread D (US5): T014 (NeatEvolverTests.cs — add batch eval tests)

Note: Threads C and D both add to NeatEvolverTests.cs so cannot truly
run in parallel — sequence them as C → D or D → C.
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T005)
3. Complete Phase 3: User Story 1 (T006a–T009)
4. **STOP and VALIDATE**: XOR solves within 150 generations with fixed seed. Core loop works.

### Incremental Delivery

1. Setup + Foundational → Configuration and building blocks ready
2. Add User Story 1 → Core training loop works, XOR validates (MVP!)
3. Add User Story 2 → Full observability: metrics, logging, summaries
4. Add User Stories 3, 4, 5 → All evaluation patterns and problem classes validated
5. Polish → Cross-framework validation, quickstart verification

### Task Summary

| Phase | Story | Tasks | Implementation Files | Test Files |
|-------|-------|-------|---------------------|------------|
| 1 | Setup | 1 | NeatSharpOptions.cs | NeatSharpOptionsTests.cs |
| 2 | Foundation | 4 | IPopulationFactory.cs, PopulationFactory.cs, TrainingLog.cs | PopulationFactoryTests.cs |
| 3 | US1 (P1) | 8 | NeatEvolver.cs, ServiceCollectionExtensions.cs | NeatEvolverTests.cs, XorExampleTests.cs, ServiceCollectionExtensionsTests.cs |
| 4 | US2 (P2) | 2 | NeatEvolver.cs (modify), GenerationStatistics.cs (modify) | NeatEvolverTests.cs (add tests) |
| 5 | US3 (P3) | 1 | — (test only) | FunctionApproximationExampleTests.cs |
| 6 | US4 (P4) | 1 | — (test only) | NeatEvolverTests.cs (add tests) |
| 7 | US5 (P5) | 1 | — (test only) | NeatEvolverTests.cs (add tests) |
| 8 | Polish | 2 | — | QuickstartValidationTests.cs |
| **Total** | | **20** | **5 new + 3 modified** | **6 new + 2 modified** |

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story is independently testable after its phase completes
- Commit after each task or logical group
- Stop at any checkpoint to validate independently
- NeatEvolver is the single central implementation — US1 builds the core loop, US2 adds observability
- US3, US4, US5 are test-only phases validating different evaluation patterns and problem types
- All existing evaluation adapters (FromFunction, FromEnvironment, FromBatch) are already implemented — training loop consumes them via `IEvaluationStrategy`
