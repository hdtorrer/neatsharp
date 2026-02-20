# Tasks: Release Readiness — Benchmarks, Examples, Docs, CI Gates & NuGet Packaging

**Input**: Design documents from `/specs/008-release-readiness/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, quickstart.md

**Tests**: Cart-Pole physics unit tests are included per plan.md constitution check ("Cart-Pole physics unit-tested with deterministic assertions"). Benchmark comparison tool tests included per plan.md ("Benchmark comparison tool tested with synthetic baseline/current JSON pairs").

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Code style enforcement and formatting infrastructure — shared across all user stories

- [X] T001 Create `.editorconfig` at repository root with C# formatting rules: IDE0055 (whitespace/formatting, warning), IDE1006 (naming — `_camelCase` private instance fields, `s_camelCase` private static fields, `PascalCase` constants/types/methods, warning), IDE0011 (brace enforcement, warning), IDE0160/IDE0161 (file-scoped namespaces, warning), IDE0040 (accessibility modifiers, warning); suggestion-level for IDE0007/IDE0008 (var preferences) and expression-bodied members per research.md R4 in `.editorconfig`
- [X] T002 [P] Create `.gitattributes` at repository root with `* text=auto eol=lf` for cross-platform line ending normalization per research.md R4 in `.gitattributes`
- [X] T003 Run `dotnet format NeatSharp.sln` to apply one-time reformatting of all existing source files; commit as a standalone commit before CI format gate activation

---

## Phase 2: Foundational (Build Configuration & Packaging Metadata)

**Purpose**: Shared build properties and NuGet packaging infrastructure that blocks US1 (packaging), US4 (CI pack gate), and US5 (benchmarks project)

**CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Add shared NuGet packaging metadata and build enforcement to `Directory.Build.props`: `<Version>0.1.0</Version>`, `<Authors>hdtorresr</Authors>`, `<PackageLicenseExpression>MIT</PackageLicenseExpression>`, `<RepositoryUrl>https://github.com/hdtorrer/neatsharp</RepositoryUrl>`, `<RepositoryType>git</RepositoryType>`, `<Copyright>Copyright 2026 hdtorresr</Copyright>`, `<PackageProjectUrl>https://github.com/hdtorrer/neatsharp</PackageProjectUrl>`, `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`, and `<IsPackable>false</IsPackable>` (default all projects to non-packable; `src/` projects override to `true` in their own `.csproj` files) in `Directory.Build.props`

**Checkpoint**: Build configuration and shared metadata ready — user story implementation can now begin

---

## Phase 3: User Story 1 — One-Step Install and First Run (Priority: P1) MVP

**Goal**: NuGet packages for NeatSharp and NeatSharp.Gpu are buildable with complete metadata; non-library projects are excluded from packaging

**Independent Test**: Run `dotnet pack NeatSharp.sln --configuration Release` and inspect the produced `.nupkg` files for correct PackageId, version, description, license, tags, repository URL, and embedded README

### Implementation for User Story 1

- [X] T005 [P] [US1] Add package-specific NuGet metadata to `src/NeatSharp/NeatSharp.csproj`: `<PackageId>NeatSharp</PackageId>`, `<Description>A GPU-accelerated NEAT (NeuroEvolution of Augmenting Topologies) library for .NET</Description>`, `<PackageTags>neat;neuroevolution;neural-network;genetic-algorithm;machine-learning;dotnet</PackageTags>`, and `<PackageReadmeFile>README.md</PackageReadmeFile>` with `<None Include="../../README.md" Pack="true" PackagePath="/"/>` in `src/NeatSharp/NeatSharp.csproj`
- [X] T006 [P] [US1] Add package-specific NuGet metadata to `src/NeatSharp.Gpu/NeatSharp.Gpu.csproj`: `<PackageId>NeatSharp.Gpu</PackageId>`, `<Description>GPU acceleration (CUDA via ILGPU) for the NeatSharp NEAT library</Description>`, `<PackageTags>neat;neuroevolution;gpu;cuda;ilgpu;neural-network;dotnet</PackageTags>`, and `<PackageReadmeFile>README.md</PackageReadmeFile>` with `<None Include="../../README.md" Pack="true" PackagePath="/"/>` in `src/NeatSharp.Gpu/NeatSharp.Gpu.csproj`
- [X] T007 [P] [US1] Add `<IsPackable>true</IsPackable>` override to the PropertyGroup in `src/NeatSharp/NeatSharp.csproj` and `src/NeatSharp.Gpu/NeatSharp.Gpu.csproj` (overriding the `false` default from `Directory.Build.props`) to mark them as the only packable projects
- [X] T008 [US1] Verify `dotnet pack NeatSharp.sln --configuration Release` produces `NeatSharp.0.1.0.nupkg` and `NeatSharp.Gpu.0.1.0.nupkg` with correct metadata (description, MIT license, tags, repository URL, embedded README) and that test/sample projects produce no `.nupkg` output

**Checkpoint**: NuGet packages are buildable locally with correct metadata — US1 acceptance scenarios 1-4 verifiable

---

## Phase 4: User Story 2 — Runnable Example Suite (Priority: P2)

**Goal**: Add a Cart-Pole (inverted pendulum) example as the third example demonstrating NEAT applied to a sequential control task; verify all three examples (XOR, Sine, Cart-Pole) run independently within 60 seconds

**Independent Test**: Run each example independently from a cloned repo — `dotnet run` (XOR+Sine default), `dotnet run -- cart-pole` — and verify each produces training progress and completes within 60 seconds

### Tests for User Story 2

- [X] T009 [P] [US2] Create unit tests for CartPoleSimulator: verify Euler integration produces correct state for known inputs (deterministic physics), failure conditions trigger correctly (|x|>2.4 → cart off track, |theta|>0.2094 → pole fallen), initial state is all zeros, Reset clears state, and configurable parameters are respected in `tests/NeatSharp.Tests/Examples/CartPoleSimulatorTests.cs`

### Implementation for User Story 2

- [X] T009a [P] [US2] Audit existing XOR and Sine examples in `samples/NeatSharp.Samples/` for FR-009 compliance: verify each has inline comments or console output explaining configuration, training, and evaluation steps; add missing commentary where needed to match the standard established by the Cart-Pole example (T012)
- [X] T010 [P] [US2] Create `CartPoleConfig` record with canonical Barto/Stanley parameters: `Gravity=9.8`, `CartMass=1.0`, `PoleMass=0.1`, `PoleHalfLength=0.5`, `ForceMagnitude=10.0`, `TimeStep=0.02`, `MaxSteps=10_000`, `TrackHalfLength=2.4`, `FailureAngle=0.2094` in `samples/NeatSharp.Samples/CartPole/CartPoleConfig.cs`
- [X] T011 [US2] Create `CartPoleSimulator` with `CartPoleState` (X, XDot, Theta, ThetaDot), `Step(double force)` method implementing standard Euler integration (theta_ddot, x_ddot from Barto/Stanley equations per research.md R3), `IsFailed()` checking both failure conditions, and `Reset()` method in `samples/NeatSharp.Samples/CartPole/CartPoleSimulator.cs`
- [X] T012 [US2] Create `CartPoleExample` runner with: DI setup via `AddNeatSharp()` composition root, `IFitnessFunction` implementation that feeds 4 inputs (x, x_dot, theta, theta_dot) to the network, interprets 1 output as force direction (>0.5 → +10N right, else -10N left), computes `fitness = steps_survived / maxSteps`, per-generation console output (generation, best fitness, species count), and champion result display (steps balanced, final state) in `samples/NeatSharp.Samples/CartPole/CartPoleExample.cs`
- [X] T013 [US2] Add `"cart-pole"` command to the argument handling in `samples/NeatSharp.Samples/Program.cs` that invokes `CartPoleExample`, alongside existing default (XOR+Sine), `benchmark`, and `hybrid-benchmark` commands
- [X] T014 [US2] Verify all three examples run independently and complete within 60 seconds: default mode (XOR + Sine), `cart-pole` mode; confirm each prints per-generation metrics and final results

**Checkpoint**: Three distinct examples (XOR, Sine Approximation, Cart-Pole) all run independently — US2 acceptance scenarios 1-4 verifiable

---

## Phase 5: User Story 3 — README and Quickstart Documentation (Priority: P3)

**Goal**: A comprehensive README that enables a new user to understand NeatSharp, install it, and run their first example by reading only the README

**Independent Test**: A developer with no prior NeatSharp knowledge follows the README from top to bottom: installs the package, runs the quickstart, finds links to examples and troubleshooting — all without leaving the README

### Implementation for User Story 3

- [X] T015 [US3] Rewrite `README.md` with: project description and feature highlights (CPU+GPU NEAT, multi-targeted .NET 8/9), installation instructions for CPU-only (`dotnet add package NeatSharp`) and GPU (`dotnet add package NeatSharp.Gpu`), copy-paste quickstart code snippet (~15-20 lines demonstrating XOR training with DI setup, `AddNeatSharp()`, training loop, and console output), GPU quickstart variant showing GPU evaluator configuration, links to all three examples in `samples/`, links to conceptual documentation in `docs/`, troubleshooting section linking to `docs/troubleshooting.md`, and license/contributing links in `README.md`

**Checkpoint**: README provides complete onboarding path — US3 acceptance scenarios 1-4 verifiable

---

## Phase 6: User Story 4 — CI Gates for Quality Assurance (Priority: P4)

**Goal**: GitHub Actions CI pipelines that build, test, format-check, and package on both Windows and Linux, with README validation in a separate workflow

**Independent Test**: Submit a PR with an intentional formatting violation and a broken test — verify CI reports both failures as blocking. Submit a clean PR — verify all gates pass within 10 minutes.

### Implementation for User Story 4

- [X] T016 [US4] Create primary CI workflow in `.github/workflows/ci.yml` with: `format` job (`dotnet format NeatSharp.sln --verify-no-changes --severity warn` on ubuntu-latest, 5-min timeout), `build-and-test` matrix job (`ubuntu-latest` + `windows-latest` × `net8.0` + `net9.0`, `dotnet test --filter "Category!=GPU"`, 10-min timeout per SC-003), `pack` job (`dotnet pack --configuration Release --output ./artifacts/packages` on ubuntu-latest, upload `.nupkg` as artifact, 5-min timeout), NuGet cache via `actions/cache@v4` keyed on `*.csproj` + `Directory.Build.props` hash, concurrency group `ci-${{ github.ref }}` with `cancel-in-progress: true`, trigger on pull_request to main and push to main
- [X] T017 [P] [US4] Create README validation workflow in `.github/workflows/docs.yml` with: `validate-readme` job using `lycheeverse/lychee-action` for link checking and shell grep verifying required sections (Installation, Quickstart, Examples, Troubleshooting) exist as headings in `README.md`, 3-min timeout, trigger on pull_request to main when `README.md` or `docs/**` change
- [X] T017a [US4] Configure GitHub branch protection on `main` via `gh api`: require status checks to pass before merging (format, build-and-test, pack, validate-readme), require branches to be up to date before merging, enforce for administrators; document the `gh api repos/{owner}/{repo}/branches/main/protection` command in `CONTRIBUTING.md` release checklist for reproducibility per FR-014

**Checkpoint**: CI pipeline detects build, test, formatting, and documentation issues and branch protection enforces them — US4 acceptance scenarios 1-4 verifiable

---

## Phase 7: User Story 5 — Benchmark Harness with Regression Guardrails (Priority: P5)

**Goal**: BenchmarkDotNet-based benchmark suite measuring CPU and GPU evaluation throughput, a local regression comparison tool with 10% threshold, and CI benchmark trend reporting

**Independent Test**: Run the benchmark suite locally, verify it produces structured JSON output. Introduce an artificial slowdown and run the comparison tool against the baseline — verify it detects the regression.

### Implementation for User Story 5

- [X] T018 [US5] Create benchmark project `benchmarks/NeatSharp.Benchmarks/NeatSharp.Benchmarks.csproj` targeting net9.0 with BenchmarkDotNet dependency, project references to `NeatSharp` and `NeatSharp.Gpu`; add project to `NeatSharp.sln` in a `benchmarks` solution folder (inherits `<IsPackable>false</IsPackable>` from `Directory.Build.props`)
- [X] T019 [US5] Create BenchmarkDotNet entry point with `BenchmarkSwitcher` supporting `--filter` and `--exporters json` for category-based filtering and JSON export in `benchmarks/NeatSharp.Benchmarks/Program.cs`
- [X] T020 [P] [US5] Create CPU evaluator benchmarks with `[Params(150, 500, 1000, 5000)]` population sizes, fixed-seed genome generation in `[GlobalSetup]`, `[Benchmark]` calling batch evaluation, `[BenchmarkCategory("CI")]` on the 500-genome variant for lightweight CI runs, and `ShortRunJob` attribute for CI benchmarks in `benchmarks/NeatSharp.Benchmarks/CpuEvaluatorBenchmarks.cs`
- [X] T021 [P] [US5] Create GPU evaluator benchmarks with same `[Params(150, 500, 1000, 5000)]` population sizes, GPU detection via `GpuDeviceDetector.Detect()` in `[GlobalSetup]` (set skip flag if null; benchmark returns immediately when skipped), `[BenchmarkCategory("GPU")]`, and `[GlobalCleanup]` for GPU resource disposal in `benchmarks/NeatSharp.Benchmarks/GpuEvaluatorBenchmarks.cs`
- [X] T022 [P] [US5] Create hybrid evaluator benchmarks comparing partition policies (Static, Adaptive, CostBased) across population sizes with `[BenchmarkCategory("GPU")]`, ServiceCollection-based DI setup in `[GlobalSetup]`, and `[GlobalCleanup]` for provider disposal in `benchmarks/NeatSharp.Benchmarks/HybridEvaluatorBenchmarks.cs`
- [X] T023 [P] [US5] Create unit tests for benchmark-compare tool in `tests/NeatSharp.Tests/Tools/BenchmarkCompareTests.cs`: matching benchmarks by FullName across baseline/current JSON pairs, correct percentage change calculation (positive and negative deltas), exit code 1 when regression exceeds threshold, exit code 0 when within threshold, handling of benchmarks present in baseline but missing in current (and vice versa), and malformed/empty JSON input handling. Use synthetic BenchmarkDotNet JSON fixtures.
- [X] T023a [US5] Create benchmark comparison console tool: net9.0, System.Text.Json only, accepts `--baseline <path>` and `--current <path>` and `--threshold <percent>` arguments, reads BenchmarkDotNet JSON exports, matches benchmarks by `FullName`, computes `% change = ((current.Mean - baseline.Mean) / baseline.Mean) * 100`, reports all comparisons with benchmark name/baseline Mean/current Mean/delta, exits 1 if any benchmark degrades beyond threshold (default 10%), exits 0 otherwise; add project to `NeatSharp.sln` in a `tools` solution folder in `tools/benchmark-compare/benchmark-compare.csproj` and `tools/benchmark-compare/Program.cs`
- [X] T024 [US5] Run full benchmark suite locally (CPU benchmarks only if no GPU available) and save BenchmarkDotNet JSON export as initial baseline in `benchmarks/baseline.json`
- [X] T025 [US5] Add `benchmark` job to `.github/workflows/ci.yml`: run on ubuntu-latest, 10-min timeout, execute `dotnet run --project benchmarks/NeatSharp.Benchmarks --configuration Release -- --filter "*CI*" --exporters json`, upload JSON results as CI artifact for trend visibility, `continue-on-error: true` (no hard-fail gate)

**Checkpoint**: Benchmark suite produces structured reports, comparison tool detects regressions, CI reports trend data — US5 acceptance scenarios 1-4 verifiable

---

## Phase 8: User Story 6 — Conceptual Documentation and Troubleshooting (Priority: P6)

**Goal**: Markdown documentation set enabling developers to tune parameters, reproduce experiments, use checkpointing, set up GPU acceleration, and troubleshoot common issues — all self-service

**Independent Test**: A developer attempts to: tune parameters for a new problem, resume from a checkpoint, diagnose a GPU issue — using only the documentation. Success means they can accomplish each task without external help.

### Implementation for User Story 6

- [X] T026 [P] [US6] Create NEAT basics guide covering: genomes (NodeGene, ConnectionGene), species and speciation (CompatibilityDistance, CompatibilitySpeciation), innovation numbers (InnovationTracker), complexification (AddNodeMutation, AddConnectionMutation), and the training loop (NeatEvolver) — mapping each concept to NeatSharp types in `docs/neat-basics.md`
- [X] T027 [P] [US6] Create parameter tuning guide covering: key `NeatSharpOptions` parameters (population size, mutation rates, speciation thresholds, stagnation limits), recommended starting values for classification vs. approximation vs. control tasks, adjustment strategies with examples, and complexity penalty configuration in `docs/parameter-tuning.md`
- [X] T028 [P] [US6] Create reproducibility guide covering: CPU determinism guarantees with `options.Seed`, GPU floating-point non-determinism (ILGPU parallel reduction order), epsilon tolerance for GPU comparisons, seed usage patterns, and how to combine seeds with checkpointing for fully resumable experiments in `docs/reproducibility.md`
- [X] T029 [P] [US6] Create checkpointing guide covering: `ICheckpointSerializer` API (SaveAsync/LoadAsync), save/restore workflow with code examples, versioned checkpoint format (SchemaVersion, compatibility), `ICheckpointValidator` for integrity checks, and resuming interrupted training with determinism in `docs/checkpointing.md`
- [X] T030 [P] [US6] Create GPU setup guide covering: CUDA toolkit and driver prerequisites, `NeatSharp.Gpu` package installation, `GpuOptions` configuration via `AddNeatSharpGpu()`, `IGpuDeviceDetector` for device discovery, performance tips (batch sizing, population thresholds for GPU benefit), and hybrid evaluation via `HybridOptions` in `docs/gpu-setup.md`
- [X] T031 [P] [US6] Create troubleshooting guide with step-by-step resolutions for: GPU not detected (driver check, CUDA toolkit version, `GpuDeviceDetector.Detect()` diagnostics), driver/toolkit version mismatch (compatibility matrix), out-of-memory errors (reduce population size, check GPU VRAM), training stalls (premature convergence — increase speciation threshold; no fitness improvement — check fitness function, increase mutation rates), and .NET runtime version issues in `docs/troubleshooting.md`
- [X] T032 [P] [US6] Create offline usage guide explaining: all core and GPU functionality works offline after initial `dotnet restore`, NuGet restore requires connectivity for new projects, how to configure local NuGet package sources for air-gapped environments, and what produces network requests (only NuGet restore) in `docs/offline-usage.md`

**Checkpoint**: All seven conceptual documentation topics authored — US6 acceptance scenarios 1-4 verifiable

---

## Phase 9: User Story 7 — Contribution Guide and Release Process (Priority: P7)

**Goal**: Contribution infrastructure enabling new contributors to set up, build, test, and submit well-formed PRs; maintainers can follow a release checklist to produce NuGet packages

**Independent Test**: A new contributor forks the repo, follows the contribution guide to set up their environment, makes a small change, and submits a PR using the template — completing the workflow without undocumented steps.

### Implementation for User Story 7

- [X] T033 [P] [US7] Create `CONTRIBUTING.md` with: prerequisites (.NET 8/9 SDK, optional CUDA toolkit for GPU development), clone and build commands (`dotnet build NeatSharp.sln`), running tests (`dotnet test NeatSharp.sln --filter "Category!=GPU"`), code style requirements (`.editorconfig`, run `dotnet format` before committing), branch naming conventions, PR submission process referencing the PR template, and how to run benchmarks locally in `CONTRIBUTING.md`
- [X] T034 [P] [US7] Create PR template with sections: Description (what and why), Spec Impact (which feature specs are affected), Spec IDs (e.g., `008-release-readiness`), Testing Performed (manual and automated), and Quality Checklist (checkboxes for: builds on both TFMs, tests pass, `dotnet format` clean, docs updated if applicable, no new warnings) in `.github/pull_request_template.md`
- [X] T035 [P] [US7] Create `CHANGELOG.md` with Keep-a-Changelog format: `## [0.1.0] - Unreleased` header, categories (Added, Changed, Fixed, Removed), entries for all features 001-008 with spec ID references (e.g., "Added: Core NEAT API with genome, network, and evolution types (001-core-api-baseline)"), and changelog conventions explanation at the top in `CHANGELOG.md`
- [X] T036 [US7] Add release checklist to `CONTRIBUTING.md` documenting step-by-step release process: update `Version` in `Directory.Build.props`, update `CHANGELOG.md` (move Unreleased to versioned section with date), verify all CI gates pass on main, run full benchmark suite locally, `dotnet pack --configuration Release`, inspect `.nupkg` contents, test package installation from local source, `dotnet nuget push` to nuget.org in `CONTRIBUTING.md`

**Checkpoint**: Contribution and release infrastructure complete — US7 acceptance scenarios 1-4 verifiable

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Final verification and cross-cutting updates

- [X] T037 [P] Update `CLAUDE.md` with new project structure entries (benchmarks/, tools/, docs/, .github/ directories), new commands (benchmark suite, comparison tool, cart-pole example), and any new conventions established during implementation in `CLAUDE.md`
- [X] T038 Run quickstart.md end-to-end verification: validate all success criteria SC-001 through SC-008 (NuGet install flow, three examples, CI gates, packaging, benchmark regression detection, troubleshooting docs, contributor workflow, conceptual docs completeness)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion (T003 reformatting must precede T004 `EnforceCodeStyleInBuild` to avoid build failures on pre-existing format violations)
- **US1 (Phase 3)**: Depends on Phase 2 (needs `Version` and shared metadata from `Directory.Build.props`)
- **US2 (Phase 4)**: Depends on Phase 2 (needs working build); independent of US1
- **US3 (Phase 5)**: Depends on US1 (needs package names for install instructions) and US2 (needs example names for links)
- **US4 (Phase 6)**: Depends on Phase 1 (`.editorconfig` for format gate) and Phase 2 (packaging for pack gate); independent of US2/US3
- **US5 (Phase 7)**: Depends on Phase 2 (needs solution and build infrastructure); independent of US1-US4
- **US6 (Phase 8)**: No strict dependencies; can start after Phase 2 but references US2 examples in guides
- **US7 (Phase 9)**: Depends on US4 (references CI in contribution guide) and US6 (references docs)
- **Polish (Phase 10)**: Depends on all prior phases

### User Story Dependencies

```text
Phase 1: Setup
    │
    v
Phase 2: Foundational
    │
    ├──────────────────────┬─────────────┬──────────────┐
    v                      v             v              v
Phase 3: US1 (P1)    Phase 4: US2   Phase 6: US4   Phase 7: US5
    │                  (P2)           (P4)           (P5)
    │                      │             │
    └──────┬───────────────┘             │         Phase 8: US6
           v                             │           (P6)
    Phase 5: US3 (P3)                    │              │
           │                             │              │
           └─────────────────────────────┼──────────────┘
                                         v
                                  Phase 9: US7 (P7)
                                         │
                                         v
                                  Phase 10: Polish
```

### Within Each User Story

- Tests MUST be written first to establish expected behavior per Constitution VII (TDD Red-Green-Refactor): T009 for US2, T023 for US5
- Models/config before services/simulators (e.g., T010 CartPoleConfig before T011 CartPoleSimulator)
- Core implementation before integration (e.g., T011 Simulator before T012 Example runner)
- Verification tasks (T008, T014) are the final task in each story

### Parallel Opportunities

**Phase 1**: T001 ‖ T002 (different files: `.editorconfig` ‖ `.gitattributes`)

**Phase 3 (US1)**: T005 ‖ T006 ‖ T007 (three different `.csproj` files)

**Phase 4 (US2)**: T009 ‖ T010 (test file ‖ config file); T009 ‖ T010 both before T011

**Phase 6 (US4)**: T016 ‖ T017 (different workflow files: `ci.yml` ‖ `docs.yml`)

**Phase 7 (US5)**: T020 ‖ T021 ‖ T022 (three different benchmark files, all after T018+T019)

**Phase 8 (US6)**: T026 ‖ T027 ‖ T028 ‖ T029 ‖ T030 ‖ T031 ‖ T032 (all seven docs are independent files)

**Phase 9 (US7)**: T033 ‖ T034 ‖ T035 (CONTRIBUTING.md ‖ PR template ‖ CHANGELOG.md); T036 depends on T033

**Cross-story parallelism**: After Phase 2, US1 ‖ US2 ‖ US4 ‖ US5 ‖ US6 can all proceed in parallel

---

## Parallel Example: User Story 2

```text
# Launch test + config in parallel (different files):
Task: T009 "Create CartPoleSimulator unit tests in tests/NeatSharp.Tests/Examples/CartPoleSimulatorTests.cs"
Task: T010 "Create CartPoleConfig record in samples/NeatSharp.Samples/CartPole/CartPoleConfig.cs"

# Then sequentially (T011 depends on T010):
Task: T011 "Create CartPoleSimulator in samples/NeatSharp.Samples/CartPole/CartPoleSimulator.cs"
Task: T012 "Create CartPoleExample in samples/NeatSharp.Samples/CartPole/CartPoleExample.cs"
Task: T013 "Add cart-pole command to samples/NeatSharp.Samples/Program.cs"
Task: T014 "Verify all three examples run within 60 seconds"
```

## Parallel Example: User Story 6

```text
# All seven documentation files are independent — launch all in parallel:
Task: T026 "docs/neat-basics.md"
Task: T027 "docs/parameter-tuning.md"
Task: T028 "docs/reproducibility.md"
Task: T029 "docs/checkpointing.md"
Task: T030 "docs/gpu-setup.md"
Task: T031 "docs/troubleshooting.md"
Task: T032 "docs/offline-usage.md"
```

---

## Implementation Strategy

### MVP First (US1 Only)

1. Complete Phase 1: Setup (`.editorconfig`, `.gitattributes`, one-time reformat)
2. Complete Phase 2: Foundational (`Directory.Build.props` packaging metadata)
3. Complete Phase 3: US1 (per-project metadata, packaging verification)
4. **STOP and VALIDATE**: `dotnet pack` produces valid `.nupkg` files with correct metadata
5. Library is installable from local NuGet source — minimum bar for announcement

### Incremental Delivery

1. Setup + Foundational → Build infrastructure ready
2. **+ US1** → Packages buildable (MVP!)
3. **+ US2** → Three compelling examples
4. **+ US3** → README enables self-service onboarding
5. **+ US4** → CI gates prevent regressions
6. **+ US5** → Benchmark regression guardrails
7. **+ US6** → Conceptual docs for deeper adoption
8. **+ US7** → Contribution infrastructure for community growth
9. **+ Polish** → Full release readiness validated

### Parallel Team Strategy

With multiple developers after Phase 2 completes:

- **Developer A**: US1 → US3 (packaging → README, sequential dependency)
- **Developer B**: US2 (Cart-Pole example, independent)
- **Developer C**: US4 + US5 (CI + benchmarks, partially sequential)
- **Developer D**: US6 → US7 (docs → contribution guide, sequential dependency)

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable at its checkpoint
- Cart-Pole tests (T009) follow existing `{Method}_{Scenario}_{ExpectedResult}` naming convention
- XOR and Sine examples already exist in `samples/NeatSharp.Samples/` — US2 adds only Cart-Pole
- Existing hand-rolled benchmarks in Samples are retained — BenchmarkDotNet suite (US5) is complementary
- All CI workflows use `actions/cache@v4` for NuGet package caching
- Benchmark CI job uses `continue-on-error: true` — no hard-fail gate per FR-018 clarification
- Commit the one-time reformatting (T003) as a standalone commit to keep diffs reviewable
