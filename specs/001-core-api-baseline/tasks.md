# Tasks: Core Package, Public API & Reproducibility Baseline

**Input**: Design documents from `/specs/001-core-api-baseline/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included — constitution Principle VII mandates TDD (Red-Green-Refactor), and plan.md explicitly lists test files.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create solution structure, project files, and build configuration per plan.md project structure.

- [x] T001 Create NeatSharp.sln solution file and Directory.Build.props with shared settings (LangVersion 13, Nullable enable, ImplicitUsings enable, TreatWarningsAsErrors true) at repo root
- [x] T002 Create src/NeatSharp/NeatSharp.csproj with multi-target net8.0;net9.0 and dependencies: Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2
- [x] T003 [P] Create tests/NeatSharp.Tests/NeatSharp.Tests.csproj with multi-target net8.0;net9.0 and dependencies: xUnit 2.9.3, FluentAssertions 7.0.0, Microsoft.NET.Test.Sdk 18.0.1, coverlet.collector 6.0.4, project reference to NeatSharp

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core types that ALL user stories depend on — genome abstraction, exception base, and configuration types.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T004 [P] Create IGenome interface with Activate(ReadOnlySpan, Span), NodeCount, and ConnectionCount in src/NeatSharp/Genetics/IGenome.cs per contracts/IGenome.cs
- [x] T005 [P] Write NeatSharpException tests (constructor with message, constructor with message and inner exception, verify message and InnerException propagation) in tests/NeatSharp.Tests/Exceptions/NeatSharpExceptionTests.cs
- [x] T006 [P] Create NeatSharpException base exception class (inherits Exception, wraps InnerException) in src/NeatSharp/Exceptions/NeatSharpException.cs per data-model.md
- [x] T007 [P] Create StoppingCriteria class with nullable MaxGenerations, FitnessTarget, StagnationThreshold and validation annotations in src/NeatSharp/Configuration/StoppingCriteria.cs per data-model.md
- [x] T008 [P] Create ComplexityLimits class with nullable MaxNodes, MaxConnections and validation annotations in src/NeatSharp/Configuration/ComplexityLimits.cs per data-model.md
- [x] T009 Create NeatSharpOptions class with PopulationSize (default 150), Seed (nullable), Stopping, Complexity, EnableMetrics (default true) and data annotations in src/NeatSharp/Configuration/NeatSharpOptions.cs per data-model.md (depends on T007, T008)

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 — First Evolution Run (Priority: P1) 🎯 MVP

**Goal**: Define the complete public API surface so a developer can configure NeatSharp via DI, create a simple fitness function, start an evolution run, and receive a champion result — all in <20 lines of user code (SC-004).

**Independent Test**: Verify that all types compile, DI registration resolves services, configuration validation rejects invalid inputs, and EvaluationStrategy factory creates valid strategy instances.

### Tests for User Story 1 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T010 [P] [US1] Write tests for NeatSharpOptions defaults and validation (PopulationSize range, Seed nullable behavior, default values) in tests/NeatSharp.Tests/Configuration/NeatSharpOptionsTests.cs
- [x] T011 [P] [US1] Write tests for StoppingCriteria validation (at least one criterion required, field range constraints) in tests/NeatSharp.Tests/Configuration/StoppingCriteriaTests.cs
- [x] T012 [P] [US1] Write tests for ComplexityLimits validation (MaxNodes and MaxConnections must be > 0 if set, null is valid for unbounded) in tests/NeatSharp.Tests/Configuration/ComplexityLimitsTests.cs
- [x] T013 [P] [US1] Write tests for EvaluationStrategy.FromFunction(sync) factory method (null guard, creates valid IEvaluationStrategy, evaluates genomes via callback) in tests/NeatSharp.Tests/Evaluation/EvaluationStrategyTests.cs
- [x] T014 [P] [US1] Write tests for AddNeatSharp DI registration (registers INeatEvolver, configures NeatSharpOptions via Action, ValidateOnStart triggers validation) in tests/NeatSharp.Tests/Extensions/ServiceCollectionExtensionsTests.cs
- [x] T015 [P] [US1] Write tests verifying EvolutionResult record construction with Seed property (non-nullable int) and WasCancelled flag in tests/NeatSharp.Tests/Evolution/EvolutionResultTests.cs

### Implementation for User Story 1

#### Reporting Value Types (leaf records, no cross-dependencies)

- [x] T016 [P] [US1] Create ComplexityStatistics record with AverageNodes, AverageConnections in src/NeatSharp/Reporting/ComplexityStatistics.cs per data-model.md
- [x] T017 [P] [US1] Create TimingBreakdown record with Evaluation, Reproduction, Speciation (all TimeSpan) in src/NeatSharp/Reporting/TimingBreakdown.cs per data-model.md
- [x] T018 [P] [US1] Create GenomeInfo record with Fitness, NodeCount, ConnectionCount in src/NeatSharp/Reporting/GenomeInfo.cs per data-model.md
- [x] T019 [P] [US1] Create Champion record with Genome (IGenome), Fitness, Generation in src/NeatSharp/Reporting/Champion.cs per data-model.md

#### Reporting Composite Types (depend on leaf records)

- [x] T020 [US1] Create GenerationStatistics record with Generation, BestFitness, AverageFitness, SpeciesCount, Complexity (ComplexityStatistics), Timing (TimingBreakdown) in src/NeatSharp/Reporting/GenerationStatistics.cs (depends on T016, T017)
- [x] T021 [US1] Create SpeciesSnapshot record with Id, Members (IReadOnlyList\<GenomeInfo\>) in src/NeatSharp/Reporting/SpeciesSnapshot.cs (depends on T018)
- [x] T022 [US1] Create RunHistory record with Generations (IReadOnlyList\<GenerationStatistics\>), TotalGenerations in src/NeatSharp/Reporting/RunHistory.cs (depends on T020)
- [x] T023 [US1] Create PopulationSnapshot record with Species (IReadOnlyList\<SpeciesSnapshot\>), TotalCount in src/NeatSharp/Reporting/PopulationSnapshot.cs (depends on T021)

#### Evolution Result Type

- [x] T024 [US1] Create EvolutionResult record with Champion, Population, History, Seed, WasCancelled in src/NeatSharp/Evolution/EvolutionResult.cs (depends on T019, T022, T023)

#### Evaluation Contracts

- [x] T025 [P] [US1] Create IEvaluationStrategy interface with EvaluatePopulationAsync in src/NeatSharp/Evaluation/IEvaluationStrategy.cs per contracts/IEvaluationStrategy.cs
- [x] T026 [US1] Create EvaluationStrategy static class with FromFunction(Func\<IGenome, double\>) factory method and internal adapter in src/NeatSharp/Evaluation/EvaluationStrategy.cs (depends on T025)

#### Evolution Entry Point

- [x] T027 [US1] Create INeatEvolver interface with RunAsync(IEvaluationStrategy, CancellationToken) and NeatEvolverExtensions with RunAsync(Func\<IGenome, double\>) convenience overload in src/NeatSharp/Evolution/INeatEvolver.cs per contracts/INeatEvolver.cs (depends on T024, T025, T026)

#### DI Registration

- [x] T028 [US1] Create ServiceCollectionExtensions with AddNeatSharp(IServiceCollection, Action\<NeatSharpOptions\>?) extension method, options registration with ValidateDataAnnotations and ValidateOnStart, and IValidateOptions\<NeatSharpOptions\> implementation for cross-field validation (at least one stopping criterion) in src/NeatSharp/Extensions/ServiceCollectionExtensions.cs (depends on T009, T027)

**Checkpoint**: At this point, the complete public API surface for a basic evolution run is defined. All types compile, DI registration works, configuration validation rejects invalid inputs.

---

## Phase 4: User Story 2 — Reproducible Runs (Priority: P2)

**Goal**: Ensure the API contracts correctly support reproducibility: seed auto-generation when null, seed recording in results, and RunHistory containing sufficient data for verification.

**Independent Test**: Verify that NeatSharpOptions.Seed defaults to null, that auto-generation behavior is documented, and that EvolutionResult always carries the seed used.

### Tests for User Story 2 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T029 [P] [US2] Write tests verifying NeatSharpOptions.Seed defaults to null (auto-generate trigger) and that non-null Seed values are preserved through options configuration in tests/NeatSharp.Tests/Configuration/NeatSharpOptionsTests.cs

### Implementation for User Story 2

- [x] T030 [US2] Verify and add XML documentation to NeatSharpOptions.Seed describing auto-generation behavior (FR-010: null triggers auto-generation via Random.Shared.Next(), recorded in EvolutionResult.Seed) in src/NeatSharp/Configuration/NeatSharpOptions.cs
- [x] T031 [US2] Verify and add XML documentation to EvolutionResult.Seed and WasCancelled describing reproducibility contract (FR-009: identical seed + config → identical results on CPU) in src/NeatSharp/Evolution/EvolutionResult.cs

**Checkpoint**: Reproducibility API surface is complete. Types correctly support deterministic seed handling.

---

## Phase 5: User Story 3 — Environment-Based Evaluation (Priority: P3)

**Goal**: Add the environment-based and batch evaluation interfaces, factory methods, and convenience extensions so developers can evaluate genomes through multi-step episodes or bulk scoring.

**Independent Test**: Verify IEnvironmentEvaluator and IBatchEvaluator compile, EvaluationStrategy factory methods create valid adapters, and NeatEvolverExtensions convenience overloads route to correct factories.

### Tests for User Story 3 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T032 [P] [US3] Write tests for EvaluationStrategy.FromFunction(async), FromEnvironment, and FromBatch factory methods (null guards, creates valid IEvaluationStrategy, correct delegation) in tests/NeatSharp.Tests/Evaluation/EvaluationStrategyTests.cs
- [x] T033 [P] [US3] Write tests for NeatEvolverExtensions async, environment, and batch overloads (correct factory delegation) in tests/NeatSharp.Tests/Evaluation/EvaluationStrategyTests.cs

### Implementation for User Story 3

- [x] T034 [P] [US3] Create IEnvironmentEvaluator interface with EvaluateAsync(IGenome, CancellationToken) in src/NeatSharp/Evaluation/IEnvironmentEvaluator.cs per contracts/IEnvironmentEvaluator.cs
- [x] T035 [P] [US3] Create IBatchEvaluator interface with EvaluateAsync(IReadOnlyList\<IGenome\>, Action\<int, double\>, CancellationToken) in src/NeatSharp/Evaluation/IBatchEvaluator.cs per contracts/IBatchEvaluator.cs
- [x] T036 [US3] Add EvaluationStrategy.FromFunction(async), FromEnvironment(IEnvironmentEvaluator), FromBatch(IBatchEvaluator) factory methods and internal adapters to src/NeatSharp/Evaluation/EvaluationStrategy.cs (depends on T034, T035)
- [x] T037 [US3] Add NeatEvolverExtensions overloads for RunAsync(Func\<IGenome, CancellationToken, Task\<double\>\>), RunAsync(IEnvironmentEvaluator), RunAsync(IBatchEvaluator) in src/NeatSharp/Evolution/INeatEvolver.cs (depends on T034, T035, T036)

**Checkpoint**: All evaluation patterns (simple, async, environment, batch) are fully supported. API surface matches contracts/ definitions.

---

## Phase 6: User Story 4 — Run Monitoring (Priority: P4)

**Goal**: Ensure the API surface supports structured logging via ILogger and metrics collection controlled by NeatSharpOptions.EnableMetrics, with zero overhead when disabled (FR-013).

**Independent Test**: Verify DI registration includes ILogger, EnableMetrics flag exists on options, and GenerationStatistics types contain all required metrics fields (FR-012).

### Tests for User Story 4 ⚠️

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T038 [P] [US4] Write tests for RunReporter default output format in tests/NeatSharp.Tests/Reporting/RunReporterTests.cs

### Implementation for User Story 4

- [x] T039 [P] [US4] Verify AddNeatSharp registers services compatible with ILogger injection and add XML documentation to EnableMetrics describing zero-overhead behavior (FR-013: [LoggerMessage] source generator, skipped when disabled) in src/NeatSharp/Extensions/ServiceCollectionExtensions.cs
- [x] T040 [US4] Add XML documentation to GenerationStatistics and ComplexityStatistics describing their role in per-generation metrics reporting (FR-012) and relationship to EnableMetrics toggle in src/NeatSharp/Reporting/GenerationStatistics.cs and src/NeatSharp/Reporting/ComplexityStatistics.cs
- [x] T041 [US4] Verify RunHistory.Generations documents that it is empty when EnableMetrics is false, while TotalGenerations still reflects actual count in src/NeatSharp/Reporting/RunHistory.cs
- [x] T042 [P] [US4] Create IRunReporter interface with GenerateSummary(EvolutionResult) returning string in src/NeatSharp/Reporting/IRunReporter.cs per contracts/IRunReporter.cs (FR-019)
- [x] T043 [US4] Create default RunReporter implementation (champion fitness, generation count, seed, species count, cancellation status) in src/NeatSharp/Reporting/RunReporter.cs (depends on T042, T024)
- [x] T044 [US4] Register IRunReporter → RunReporter in AddNeatSharp DI extension (Singleton lifetime) in src/NeatSharp/Extensions/ServiceCollectionExtensions.cs (depends on T043, T028)

**Checkpoint**: Monitoring API surface is complete. Logging, metrics, and summary reporting contracts are documented and ready for algorithm implementation.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, build verification, and cross-cutting improvements.

- [x] T045 Verify solution builds successfully on both net8.0 and net9.0 targets with zero warnings (TreatWarningsAsErrors) by running `dotnet build` at repo root
- [x] T046 Run all tests on both net8.0 and net9.0 targets by running `dotnet test` at repo root and verify all pass
- [x] T047 Validate quickstart.md code samples compile against the implemented public API surface (namespace imports, type names, method signatures match)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational (Phase 2) — defines core API surface
- **US2 (Phase 4)**: Depends on US1 (Phase 3) — extends types created in US1
- **US3 (Phase 5)**: Depends on Foundational (Phase 2) and US1 T025-T027 — adds evaluation patterns
- **US4 (Phase 6)**: Depends on US1 (Phase 3) — extends documentation on existing types
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) — no dependencies on other stories
- **User Story 2 (P2)**: Can start after US1 core types exist (T024, T028) — extends their documentation and tests
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) + US1 evaluation types (T025-T027) — adds new evaluation interfaces
- **User Story 4 (P4)**: Can start after US1 complete — extends documentation on existing types

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD per constitution Principle VII)
- Leaf records/types before composite types
- Interfaces before factory implementations
- Factory implementations before convenience extensions
- DI registration last (depends on all types)

### Parallel Opportunities

- T002 and T003 can run in parallel (different project files)
- T004, T005, T006, T007, T008 can all run in parallel (independent foundational types; T005 test before T006 impl for TDD)
- T010, T011, T012, T013, T014, T015 can all run in parallel (independent test files)
- T016, T017, T018, T019 can run in parallel (leaf reporting records)
- T025 can run in parallel with T016-T023 (evaluation interface vs reporting types)
- T034, T035 can run in parallel (independent evaluation interfaces)
- US3 implementation (T034-T037) can partially overlap with US2 (T029-T031) since they target different files

---

## Parallel Example: User Story 1

```bash
# Launch all tests for US1 together (TDD - write first, must fail):
Task: "NeatSharpOptions defaults/validation tests in tests/.../NeatSharpOptionsTests.cs"
Task: "StoppingCriteria validation tests in tests/.../StoppingCriteriaTests.cs"
Task: "ComplexityLimits validation tests in tests/.../ComplexityLimitsTests.cs"
Task: "EvaluationStrategy factory tests in tests/.../EvaluationStrategyTests.cs"
Task: "ServiceCollectionExtensions DI tests in tests/.../ServiceCollectionExtensionsTests.cs"
Task: "EvolutionResult construction tests in tests/.../EvolutionResultTests.cs"

# Launch all leaf reporting records together:
Task: "ComplexityStatistics in src/.../Reporting/ComplexityStatistics.cs"
Task: "TimingBreakdown in src/.../Reporting/TimingBreakdown.cs"
Task: "GenomeInfo in src/.../Reporting/GenomeInfo.cs"
Task: "Champion in src/.../Reporting/Champion.cs"

# IEvaluationStrategy can run in parallel with composite reporting types:
Task: "IEvaluationStrategy in src/.../Evaluation/IEvaluationStrategy.cs"
Task: "GenerationStatistics in src/.../Reporting/GenerationStatistics.cs"  # needs ComplexityStatistics
Task: "SpeciesSnapshot in src/.../Reporting/SpeciesSnapshot.cs"            # needs GenomeInfo
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (solution, projects)
2. Complete Phase 2: Foundational (IGenome, NeatSharpException, configuration types)
3. Complete Phase 3: User Story 1 (core API surface)
4. **STOP and VALIDATE**: `dotnet build` and `dotnet test` pass on both TFMs
5. This gives a fully defined, compilable, testable public API surface

### Incremental Delivery

1. Setup + Foundational → Projects build with zero warnings
2. Add US1 → Core API surface defined → Build + test (MVP!)
3. Add US2 → Reproducibility contracts validated → Build + test
4. Add US3 → All evaluation patterns supported → Build + test
5. Add US4 → Monitoring surface complete → Build + test
6. Polish → Full validation against quickstart.md

### Suggested MVP Scope

**User Story 1 only** (Phases 1-3, tasks T001-T028). This delivers the complete API surface for a basic evolution run: configuration, DI registration, simple fitness evaluation, and champion result. All other stories extend this surface.

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Verify tests fail before implementing (TDD)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- The NEAT algorithm implementation is OUT OF SCOPE (spec assumptions) — these tasks define API contracts and types only
- All types should use C# records where appropriate (immutable result types) per data-model.md
- Nullable reference types are enabled project-wide per Directory.Build.props
