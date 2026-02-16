# Tasks: CUDA Evaluator Backend + Auto GPU Use + Fallback

**Input**: Design documents from `/specs/006-cuda-evaluator/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included (TDD required per plan.md constitution check VII)

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create NeatSharp.Gpu and NeatSharp.Gpu.Tests projects, integrate into solution

- [X] T001 Create NeatSharp.Gpu class library project with multi-target net8.0;net9.0, ILGPU 1.5.x, ILGPU.Algorithms 1.5.x, Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2, and project reference to NeatSharp in src/NeatSharp.Gpu/NeatSharp.Gpu.csproj
- [X] T002 Create NeatSharp.Gpu.Tests xUnit test project with multi-target net8.0;net9.0, xUnit 2.9.3, FluentAssertions 7.0.0, Microsoft.NET.Test.Sdk, ILGPU 1.5.x, ILGPU.Algorithms 1.5.x, and project reference to NeatSharp.Gpu in tests/NeatSharp.Gpu.Tests/NeatSharp.Gpu.Tests.csproj
- [X] T003 Add both new projects (NeatSharp.Gpu and NeatSharp.Gpu.Tests) to NeatSharp.sln and verify solution builds successfully

**Checkpoint**: Project skeleton ready — all three projects build, test runner discovers the GPU test project

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core abstractions, exceptions, configuration, and interfaces that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create GpuEvaluationException (extends NeatSharpException), GpuOutOfMemoryException, and GpuDeviceException in src/NeatSharp.Gpu/Exceptions/GpuEvaluationException.cs, src/NeatSharp.Gpu/Exceptions/GpuOutOfMemoryException.cs, src/NeatSharp.Gpu/Exceptions/GpuDeviceException.cs
- [X] T005 [P] Create GpuActivationFunction internal enum (Sigmoid=0, Tanh=1, ReLU=2, Step=3, Identity=4) in src/NeatSharp.Gpu/Evaluation/GpuActivationFunction.cs
- [X] T006 [P] Create IGpuFitnessFunction interface (CaseCount, InputCases, ComputeFitness) in src/NeatSharp.Gpu/Evaluation/IGpuFitnessFunction.cs
- [X] T007 [P] Create IGpuDeviceDetector interface, IGpuDeviceInfo interface, and GpuDeviceInfo immutable record in src/NeatSharp.Gpu/Detection/IGpuDeviceDetector.cs, src/NeatSharp.Gpu/Detection/IGpuDeviceInfo.cs, src/NeatSharp.Gpu/Detection/GpuDeviceInfo.cs
- [X] T008 [P] Create GpuOptions configuration class and GpuOptionsValidator (IValidateOptions) in src/NeatSharp.Gpu/Configuration/GpuOptions.cs and src/NeatSharp.Gpu/Configuration/GpuOptionsValidator.cs
- [X] T009 Write GpuOptionsValidator unit tests (range validation, null handling, edge cases) in tests/NeatSharp.Gpu.Tests/Configuration/GpuOptionsValidatorTests.cs

**Checkpoint**: Foundation ready — all shared types compile, validator tests pass, user story implementation can now begin

---

## Phase 3: User Story 1 — GPU-Accelerated Batch Evaluation (Priority: P1) MVP

**Goal**: Evaluate entire NEAT populations on the GPU via the existing IBatchEvaluator contract, achieving >= 5x throughput over CPU for medium-scale populations (500-2,000 genomes)

**Independent Test**: Run a known benchmark workload (XOR or multi-input function approximation with fixed population) using ILGPU CPU accelerator. All genomes receive correct fitness scores. Forward propagation produces expected outputs for known topologies.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation (TDD Red phase)**

- [X] T010 [P] [US1] Write GpuFeedForwardNetwork tests: construction from Genome, IGenome delegation to CPU fallback, flat topology extraction (InputIndices, OutputIndices, EvalOrder), degenerate genome (zero connections) handling in tests/NeatSharp.Gpu.Tests/Evaluation/GpuFeedForwardNetworkTests.cs
- [X] T011 [P] [US1] Write GpuNetworkBuilder tests: decorator pattern (delegates to FeedForwardNetworkBuilder), activation function mapping (all 5 built-in), unknown activation function throws GpuEvaluationException, heterogeneous genome topologies in tests/NeatSharp.Gpu.Tests/Evaluation/GpuNetworkBuilderTests.cs
- [X] T012 [P] [US1] Write GpuBatchEvaluator tests using ILGPU CPU accelerator: single genome evaluation, batch evaluation (multiple genomes), heterogeneous topologies, degenerate genomes, GPU memory management (buffer reuse), CPU fallback on GPU failure, GPU OOM handling (simulate memory exhaustion, verify GpuOutOfMemoryException with population size + estimated memory + available memory in message), IDisposable cleanup in tests/NeatSharp.Gpu.Tests/Evaluation/GpuBatchEvaluatorTests.cs

### Implementation for User Story 1

- [X] T013 [P] [US1] Create GpuEvalNode internal record struct (BufferIndex, IncomingSources, IncomingWeights, ActivationType) and GpuFeedForwardNetwork class implementing IGenome (wraps CPU phenotype + flat GPU topology arrays: InputIndices, BiasIndices, OutputIndices, NodeActivationTypes, EvalOrder) in src/NeatSharp.Gpu/Evaluation/GpuFeedForwardNetwork.cs
- [X] T014 [P] [US1] Create ActivationKernels with static ApplyActivation(float x, int activationType) method using ILGPU XMath (Sigmoid: steepened 4.9f, Tanh, ReLU, Step, Identity) in src/NeatSharp.Gpu/Kernels/ActivationKernels.cs
- [X] T015 [US1] Create GpuNetworkBuilder implementing INetworkBuilder as decorator over FeedForwardNetworkBuilder — builds CPU phenotype via inner builder, extracts flat GPU topology from Genome (topological sort, activation function mapping via case-insensitive dictionary), returns GpuFeedForwardNetwork in src/NeatSharp.Gpu/Evaluation/GpuNetworkBuilder.cs
- [X] T016 [US1] Create GpuPopulationData class that flattens IReadOnlyList of GpuFeedForwardNetwork into contiguous SoA arrays (GenomeNodeOffsets, GenomeNodeCounts, GenomeEvalOrderOffsets, GenomeEvalOrderCounts, GenomeInputOffsets, GenomeInputCounts, GenomeOutputOffsets, GenomeOutputCounts, NodeActivationTypes, EvalNodeIndices, EvalNodeConnOffsets, EvalNodeConnCounts, IncomingSourceIndices, IncomingWeights, InputNodeIndices, OutputNodeIndices) with validation (monotonic offsets, bounds checks) in src/NeatSharp.Gpu/Evaluation/GpuPopulationData.cs
- [X] T017 [US1] Create ForwardPropagationKernel — ILGPU kernel method (1 thread per genome) that loads test case inputs into activation buffer, iterates eval order nodes sequentially, accumulates weighted inputs, applies activation via ActivationKernels.ApplyActivation, writes outputs to result buffer in src/NeatSharp.Gpu/Kernels/ForwardPropagationKernel.cs
- [X] T018 [US1] Create GpuBatchEvaluator implementing IBatchEvaluator and IDisposable — constructor-inject IGpuDeviceDetector and IGpuFitnessFunction, lazy GPU detection, builds GpuPopulationData from genomes, manages ILGPU Context/Accelerator/MemoryBuffer1D lifecycle (pooled buffers, resize-on-grow), uploads topology + IGpuFitnessFunction.InputCases to GPU, launches ForwardPropagationKernel, downloads outputs, calls ComputeFitness per genome, reports fitness via setFitness callback, CPU fallback on GPU failure (log warning, use IGenome.Activate) in src/NeatSharp.Gpu/Evaluation/GpuBatchEvaluator.cs
- [X] T019 [US1] Verify all US1 tests pass with ILGPU CPU accelerator — run test suite, fix any failing tests, ensure Red-Green-Refactor cycle is complete

**Checkpoint**: GPU batch evaluation works end-to-end via ILGPU CPU accelerator. All genomes evaluated correctly. Buffer management validates. CPU fallback operates on simulated GPU failure.

---

## Phase 4: User Story 2 — Automatic GPU Detection and Selection (Priority: P2)

**Goal**: Auto-detect compatible GPUs at startup, select GPU evaluation when available, fall back to CPU transparently when no GPU is present, provide actionable diagnostics for incompatible hardware

**Independent Test**: Run the same training configuration with GPU enabled (should auto-detect) and with GPU disabled (should use CPU). Both complete successfully. On a machine without a GPU, CPU fallback is used and logged.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T020 [P] [US2] Write GpuDeviceDetector tests: detection with ILGPU CPU accelerator, caching behavior (detect called once), incompatible device returns IsCompatible=false with diagnostic message, no device returns null in tests/NeatSharp.Gpu.Tests/Detection/GpuDeviceDetectorTests.cs

### Implementation for User Story 2

- [X] T021 [US2] Implement GpuDeviceDetector — create ILGPU Context, enumerate CUDA devices, check compute capability >= configured MinComputeCapability, return GpuDeviceInfo with compatibility status and actionable diagnostic messages (missing CUDA runtime, unsupported CC), cache result after first call in src/NeatSharp.Gpu/Detection/GpuDeviceDetector.cs
- [X] T022 [US2] Create ServiceCollectionExtensions with AddNeatSharpGpu(Action<GpuOptions>?) extension method — register GpuOptions with ValidateDataAnnotations and ValidateOnStart, register GpuOptionsValidator, register IGpuDeviceDetector as singleton GpuDeviceDetector, register INetworkBuilder as singleton GpuNetworkBuilder (decorator over existing FeedForwardNetworkBuilder), register IBatchEvaluator as scoped GpuBatchEvaluator (user must register their IGpuFitnessFunction implementation separately, e.g. services.AddSingleton<IGpuFitnessFunction, MyFitness>()) in src/NeatSharp.Gpu/Extensions/ServiceCollectionExtensions.cs
- [X] T023 [US2] Add startup logging to GpuBatchEvaluator — log Information with GPU device name/CC/memory when GPU detected, log Information when CPU fallback selected (no GPU), log Warning with diagnostic details when GPU present but incompatible, log Warning when GPU evaluation fails mid-generation and falling back to CPU in src/NeatSharp.Gpu/Evaluation/GpuBatchEvaluator.cs
- [X] T024 [US2] Write DI integration tests — verify AddNeatSharpGpu registers all expected services (including IBatchEvaluator), verify GpuNetworkBuilder decorates FeedForwardNetworkBuilder, verify EnableGpu=false forces CPU evaluation, verify CPU fallback when no CUDA device in tests/NeatSharp.Gpu.Tests/Extensions/ServiceCollectionExtensionsTests.cs

**Checkpoint**: Auto-detection works. GPU selected when available, CPU fallback when not. Actionable diagnostics logged. DI registration wires up correctly. Same code runs on CPU-only and GPU machines.

---

## Phase 5: User Story 3 — CPU/GPU Result Consistency (Priority: P3)

**Goal**: Validate that GPU evaluation produces fitness scores within documented epsilon tolerance (1e-4 for fp32) of CPU evaluation for identical inputs and genomes

**Independent Test**: Evaluate a set of genomes on both CPU and GPU with identical inputs. Compare per-genome fitness values — each pair differs by no more than 1e-4. Run canonical XOR problem on both paths and compare champion fitness.

- [X] T025 [P] [US3] Write CPU vs GPU per-genome tolerance comparison tests — evaluate identical genomes (simple, medium, complex topologies) on both CPU (FeedForwardNetwork.Activate with fp64) and GPU (ForwardPropagationKernel with fp32) paths, assert per-output difference <= 1e-4, assert per-genome fitness difference <= documented epsilon in tests/NeatSharp.Gpu.Tests/Evaluation/GpuBatchEvaluatorTests.cs
- [X] T026 [US3] Write canonical XOR consistency test — run XOR evaluation with known genomes on both CPU and GPU, compare fitness scores within tolerance, verify all 5 activation functions produce consistent results across paths in tests/NeatSharp.Gpu.Tests/Evaluation/GpuBatchEvaluatorTests.cs
- [X] T027 [US3] Document epsilon tolerance and precision trade-offs in XML doc comments on GpuBatchEvaluator and IGpuFitnessFunction — fp32 vs fp64 precision differences, expected tolerance range, steepened sigmoid numeric sensitivity in src/NeatSharp.Gpu/Evaluation/GpuBatchEvaluator.cs and src/NeatSharp.Gpu/Evaluation/IGpuFitnessFunction.cs

**Checkpoint**: CPU/GPU consistency validated. Per-genome scores within documented epsilon. XOR canonical problem passes on both paths. Tolerance documented in code.

---

## Phase 6: User Story 4 — Determinism Documentation and Best-Effort GPU Reproducibility (Priority: P4)

**Goal**: Provide a best-effort deterministic GPU mode (one thread per genome, sequential accumulation, fixed thread assignment) and document what reproducibility guarantees each mode provides

**Independent Test**: Run same workload with same seed in best-effort deterministic mode twice using ILGPU CPU accelerator. Results should be identical. Document any operations that may introduce non-determinism.

- [X] T028 [US4] Implement BestEffortDeterministic option handling — when GpuOptions.BestEffortDeterministic is true, ensure kernel uses sequential accumulation per thread (already the default per R-008), document per-run determinism guarantees vs cross-machine limitations in ForwardPropagationKernel comments in src/NeatSharp.Gpu/Kernels/ForwardPropagationKernel.cs
- [X] T029 [US4] Write determinism validation tests — run identical workload twice with same seed and BestEffortDeterministic=true, assert bitwise identical GPU outputs, document non-determinism sources (driver variations, FMA instructions) in test comments in tests/NeatSharp.Gpu.Tests/Evaluation/GpuBatchEvaluatorTests.cs

**Checkpoint**: Best-effort deterministic mode implemented. Same-seed reproducibility validated on ILGPU CPU accelerator. Determinism guarantees and limitations documented.

---

## Phase 7: User Story 5 — Benchmark Report and Performance Validation (Priority: P5)

**Goal**: Create a benchmark suite that measures CPU vs GPU throughput on a representative workload across at least 3 population sizes and produces a report artifact suitable for repository inclusion

**Independent Test**: Run benchmark suite on reference hardware. Results match published report within reasonable variance. Report documents hardware config, population sizes, throughput, and speedup factors.

- [X] T030 [US5] Create benchmark suite — evaluate a multi-input function approximation problem at population sizes 150, 500, 1000, 2000, 5000 on both CPU and GPU paths, measure throughput (genomes/second), compute speedup factors, output results as structured data in samples/NeatSharp.Samples/ or a dedicated benchmark project
- [X] T031 [US5] Run benchmarks on reference hardware (requires CUDA GPU) and generate markdown report artifact with hardware configuration, methodology, results table, and speedup analysis — gated with [Trait("Category", "GPU")]
- [X] T032 [US5] Write benchmark validation tests verifying the benchmark suite executes correctly on ILGPU CPU accelerator (correctness, not performance) in tests/NeatSharp.Gpu.Tests/Integration/

**Checkpoint**: Benchmark suite runs. Report artifact documents CPU vs GPU performance across population sizes. Performance claims validated on reference hardware.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: End-to-end integration validation, sample updates, and final quality checks

- [X] T033 [P] Write GpuTrainingIntegrationTests — full NeatEvolver training loop with GpuBatchEvaluator on ILGPU CPU accelerator, verify XOR problem converges, verify champion fitness meets threshold, verify GPU-to-CPU fallback during training in tests/NeatSharp.Gpu.Tests/Integration/GpuTrainingIntegrationTests.cs
- [X] T034 [P] Run quickstart.md end-to-end validation — verify all code samples in specs/006-cuda-evaluator/quickstart.md compile and run correctly against the implemented API
- [X] T035 Final build verification — clean solution build on both net8.0 and net9.0, run all tests (GPU trait excluded on CI without GPU), verify zero nullable warnings, verify NeatSharp core has no ILGPU dependency
- [X] T036 [US2] Write SC-005 startup overhead test — measure GPU detection time when no CUDA device is available (GpuDeviceDetector.Detect() returns null via ILGPU CPU accelerator), assert total detection overhead < 1 second, verifying negligible startup cost for CPU-only machines in tests/NeatSharp.Gpu.Tests/Detection/GpuDeviceDetectorTests.cs

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **US1 (Phase 3)**: Depends on Foundational phase completion — MVP deliverable
- **US2 (Phase 4)**: Depends on US1 completion (modifies GpuBatchEvaluator, adds DI registration)
- **US3 (Phase 5)**: Depends on US1 completion (needs both CPU and GPU evaluation paths)
- **US4 (Phase 6)**: Depends on US1 completion (modifies kernel behavior)
- **US5 (Phase 7)**: Depends on US1 + US2 completion (needs full pipeline for benchmarking)
- **Polish (Phase 8)**: Depends on US1 + US2 completion (integration tests need DI registration)

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational (Phase 2) — No dependencies on other stories
- **US2 (P2)**: Depends on US1 — adds auto-detection and DI to the evaluator
- **US3 (P3)**: Depends on US1 — can run in parallel with US2
- **US4 (P4)**: Depends on US1 — can run in parallel with US2, US3
- **US5 (P5)**: Depends on US1 + US2 — needs complete pipeline

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD)
- Data types (GpuFeedForwardNetwork, GpuEvalNode) before builders (GpuNetworkBuilder)
- Builders before batch data (GpuPopulationData)
- Kernels (ActivationKernels) before composite kernel (ForwardPropagationKernel)
- All components before evaluator (GpuBatchEvaluator)
- Story complete and tests green before moving to next priority

### Parallel Opportunities

- **Phase 2**: T004-T008 all [P] — 5 tasks on different files, no dependencies
- **Phase 3 tests**: T010-T012 all [P] — 3 test files, independent
- **Phase 3 impl**: T013 + T014 [P] (GpuFeedForwardNetwork + ActivationKernels are independent); T015 + T016 can be [P] after T013 completes
- **US3 + US4 can run in parallel** after US1 is complete (different concerns, different files)
- **Phase 8**: T033 + T034 [P] (integration tests + quickstart validation)

---

## Parallel Example: User Story 1

```bash
# Launch all tests for US1 together (TDD Red phase):
Task: "Write GpuFeedForwardNetwork tests in tests/NeatSharp.Gpu.Tests/Evaluation/GpuFeedForwardNetworkTests.cs"
Task: "Write GpuNetworkBuilder tests in tests/NeatSharp.Gpu.Tests/Evaluation/GpuNetworkBuilderTests.cs"
Task: "Write GpuBatchEvaluator tests in tests/NeatSharp.Gpu.Tests/Evaluation/GpuBatchEvaluatorTests.cs"

# Launch independent implementation tasks together:
Task: "Create GpuEvalNode and GpuFeedForwardNetwork in src/NeatSharp.Gpu/Evaluation/GpuFeedForwardNetwork.cs"
Task: "Create ActivationKernels in src/NeatSharp.Gpu/Kernels/ActivationKernels.cs"

# Then launch dependent tasks that share T013 as prerequisite:
Task: "Create GpuNetworkBuilder in src/NeatSharp.Gpu/Evaluation/GpuNetworkBuilder.cs"
Task: "Create GpuPopulationData in src/NeatSharp.Gpu/Evaluation/GpuPopulationData.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (3 tasks)
2. Complete Phase 2: Foundational (6 tasks, CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1 (10 tasks)
4. **STOP and VALIDATE**: All tests pass with ILGPU CPU accelerator, genomes evaluated correctly, buffer management works, CPU fallback operates
5. This is a deployable MVP — GPU evaluation works when an accelerator is manually provided

### Incremental Delivery

1. Setup + Foundational -> Foundation ready
2. Add US1 -> Test with CPU accelerator -> MVP! (core GPU evaluation)
3. Add US2 -> Test auto-detection + DI -> Usable! (one-liner AddNeatSharpGpu registration)
4. Add US3 -> Validate consistency -> Trustworthy! (CPU/GPU parity proven)
5. Add US4 -> Document determinism -> Scientific! (reproducibility guarantees documented)
6. Add US5 -> Run benchmarks -> Validated! (performance claims backed by data)
7. Polish -> Integration tests + cleanup -> Release-ready!

### Parallel Team Strategy

With multiple developers after Foundational is complete:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 (core GPU evaluation) — MUST complete first
3. Once US1 is done:
   - Developer A: US2 (auto-detection + DI)
   - Developer B: US3 (consistency validation) — parallel with US2
   - Developer C: US4 (determinism) — parallel with US2, US3
4. Once US1 + US2 are done:
   - Developer B: US5 (benchmarks)
5. Polish phase: any developer

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- All GPU kernel logic testable via ILGPU CPU accelerator (no hardware required)
- Hardware-specific tests gated with `[Trait("Category", "GPU")]`
- ILGPU kernel constraints: no reference types, no exceptions, no delegates, no dynamic allocation — value types and ArrayView<T> only
- GpuBatchEvaluator implements IDisposable (GPU buffers, accelerator, context cleanup)

- fp32 GPU precision vs fp64 CPU baseline — 1e-4 epsilon tolerance
