# Feature Specification: Release Readiness — Benchmarks, Examples, Docs, CI Gates & NuGet Packaging

**Feature Branch**: `008-release-readiness`
**Created**: 2026-02-18
**Status**: Draft
**Input**: User description: "Make the project announce-ready: examples, benchmarks, CI checks, and packaging polish."

## Clarifications

### Session 2026-02-18

- Q: Which open-source license should the project use for NuGet package metadata and the repository LICENSE file? → A: MIT
- Q: What versioning scheme and initial version number should the project use? → A: SemVer starting at 0.1.0
- Q: Which specific scenario should the third (black-box optimization) example implement? → A: Pole Balancing (Cart-Pole) — a sequential control task and canonical NEAT benchmark
- Q: How should benchmark regression detection work given variable CI runner performance? → A: CI runs benchmarks for visibility/trend reporting only (no hard-fail gate); contributors run regression checks locally against their own machine's baseline
- Q: Should NuGet packages be signed for the initial release? → A: Unsigned for now; defer package signing to a future release (standard for new OSS projects at 0.1.0)

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One-Step Install and First Run (Priority: P1)

A developer discovers NeatSharp and wants to try it immediately. They install the library from NuGet into an existing project with a single command, copy a quickstart snippet from the README, and run it. The program trains a small neural network on the XOR problem and prints generation-by-generation progress to the console. The developer sees fitness improving over generations and understands the library works. The entire experience — from discovery to running code — takes under 5 minutes with no manual build steps, source cloning, or dependency chasing.

**Why this priority**: Without installable packages and a working quickstart, users cannot try the library at all. This is the absolute minimum bar for a public announcement. Every other story depends on the library being installable.

**Independent Test**: Can be tested by creating a new empty console project, installing the NuGet package(s), pasting the README quickstart snippet, and running it. Success means the program compiles, runs, and produces visible output showing NEAT training progress.

**Acceptance Scenarios**:

1. **Given** a new empty console project targeting a supported runtime, **When** the user installs the NeatSharp NuGet package and pastes the quickstart code from the README, **Then** the project compiles and runs without errors, printing training progress to the console.
2. **Given** a user on a machine without a GPU, **When** they install only the core NeatSharp package and run the quickstart, **Then** training completes using CPU evaluation with no GPU-related errors or warnings.
3. **Given** a user on a machine with a compatible GPU, **When** they additionally install the NeatSharp.Gpu package and modify the quickstart to enable GPU evaluation, **Then** training completes using GPU acceleration.
4. **Given** a freshly published NuGet package, **When** inspecting the package metadata, **Then** it includes a description, license, repository URL, README, tags, and version — all populated and accurate.

---

### User Story 2 - Runnable Example Suite (Priority: P2)

A developer evaluating NeatSharp wants to see it solve real problems beyond XOR. They clone the repository and run the example suite, which includes at least three distinct examples: a classic XOR solver with generation-by-generation progress output, a function approximation task demonstrating continuous-output learning, and a black-box optimization task showing NEAT applied to a domain beyond simple function fitting. Each example runs independently, completes in a reasonable time, and clearly demonstrates what NeatSharp can do.

**Why this priority**: Examples are the primary evaluation tool for potential adopters. They demonstrate capability breadth and serve as copy-paste starting points. Without compelling examples, the quickstart alone is insufficient to convince developers to adopt the library.

**Independent Test**: Can be tested by cloning the repository and running each example independently. Each example should compile, run to completion, and produce clearly labeled output showing training progress and final results.

**Acceptance Scenarios**:

1. **Given** a cloned repository, **When** the user runs the XOR example, **Then** it trains to a solution, printing per-generation metrics (generation number, best fitness, species count) and a final summary showing the champion's accuracy.
2. **Given** a cloned repository, **When** the user runs the function approximation example, **Then** it demonstrates learning a continuous function (e.g., sine) and shows approximation quality improving over generations.
3. **Given** a cloned repository, **When** the user runs the black-box optimization example, **Then** it demonstrates NEAT optimizing a fitness function that is not a simple input-output mapping (e.g., a multi-objective evaluation, a sequential decision problem, or a parameterized simulation), and the output clearly explains what is being optimized.
4. **Given** any example, **When** the user runs it without modification, **Then** it completes within 60 seconds on a modern machine.

---

### User Story 3 - README and Quickstart Documentation (Priority: P3)

A developer landing on the repository for the first time wants to understand what NeatSharp is, what it can do, and how to get started. The README provides a concise project summary, installation instructions for both CPU-only and GPU-accelerated usage, a copy-paste quickstart snippet, links to examples, and a brief feature list. A developer can go from zero knowledge to running code by reading only the README.

**Why this priority**: The README is the most-read artifact in any open-source project. It is the first impression and primary onboarding path. Without a complete README, even a well-packaged library with great examples will fail to attract users.

**Independent Test**: Can be tested by having a developer with no prior NeatSharp knowledge follow the README from top to bottom. They should be able to install the package, understand the project's purpose, run the quickstart, and find links to further documentation — all without leaving the README.

**Acceptance Scenarios**:

1. **Given** the repository README, **When** a new user reads it, **Then** they can identify: what NeatSharp is, what problems it solves, how to install it, and how to run a first example — all within the first screen of content.
2. **Given** the README quickstart section, **When** a user copies the code snippet into a new project, **Then** the code compiles and runs with only the documented NuGet package installation as a prerequisite.
3. **Given** the README, **When** a user with a GPU wants to enable GPU acceleration, **Then** the README contains a clearly labeled section explaining how to add the GPU package and configure GPU evaluation.
4. **Given** the README, **When** a user encounters a problem, **Then** the README links to a troubleshooting section that covers common issues (GPU not detected, driver mismatch, runtime version mismatch).

---

### User Story 4 - CI Gates for Quality Assurance (Priority: P4)

A contributor submits a pull request to NeatSharp. Before it can be merged, automated CI pipelines verify that the code builds on both Windows and Linux, all tests pass on both platforms, code formatting conforms to project standards, the NuGet packages can be produced, and documentation (README) passes basic validation. The contributor receives clear feedback about what passed and what failed, enabling them to fix issues before review.

**Why this priority**: CI gates prevent regressions, enforce standards, and reduce reviewer burden. Without CI, every PR must be manually validated — a process that does not scale and inevitably misses issues. CI is foundational infrastructure for a healthy open-source project.

**Independent Test**: Can be tested by submitting a PR with an intentional formatting violation, a broken test, and a valid change — verifying that CI reports the formatting and test failures as blocking while the valid change shows green.

**Acceptance Scenarios**:

1. **Given** a pull request targeting the main branch, **When** CI runs, **Then** the build is executed on both Windows and Linux with all supported target frameworks.
2. **Given** a pull request with code that violates formatting rules, **When** CI runs, **Then** the formatting check fails with a clear message identifying the violation and how to fix it.
3. **Given** a pull request where all tests pass and formatting is clean, **When** CI runs, **Then** a NuGet package is produced as a build artifact (not published) to validate that packaging works.
4. **Given** a pull request, **When** CI runs, **Then** all checks complete within 10 minutes for a typical change.

---

### User Story 5 - Benchmark Harness with Regression Guardrails (Priority: P5)

A maintainer wants to ensure that code changes do not degrade evaluation performance. A structured benchmark suite measures CPU and GPU evaluation throughput across multiple population sizes. The benchmarks produce consistent, comparable results and can be run locally by contributors. In CI, a lightweight benchmark subset runs on each PR and reports throughput numbers for trend visibility (no hard-fail gate due to shared runner performance variance). Contributors detect regressions locally by comparing against their own machine's baseline using a provided comparison tool with a 10% degradation threshold.

**Why this priority**: Performance is a key selling point of NeatSharp, especially GPU evaluation. Without regression guardrails, performance improvements can be silently undone. The benchmark suite also produces data for the published performance claims in documentation.

**Independent Test**: Can be tested by running the benchmark suite locally and verifying it produces a structured report. The regression check can be tested by introducing an artificial slowdown (e.g., adding a sleep to the evaluation loop) and verifying the CI gate catches it.

**Acceptance Scenarios**:

1. **Given** a developer machine with a GPU, **When** the benchmark suite runs, **Then** it produces a structured report showing CPU vs. GPU throughput across at least 3 population sizes, with timing statistics.
2. **Given** a developer machine without a GPU, **When** the benchmark suite runs, **Then** it produces a report for CPU-only benchmarks and skips GPU benchmarks gracefully with an informational message.
3. **Given** an established local performance baseline, **When** a contributor runs the local regression check after introducing a change that degrades throughput by more than 10%, **Then** the comparison tool reports the regression magnitude and affected benchmark with a clear warning.
4. **Given** a PR, **When** CI runs the benchmark subset, **Then** it reports current throughput numbers in the CI log for trend visibility (no hard-fail gate).

---

### User Story 6 - Conceptual Documentation and Troubleshooting (Priority: P6)

A developer adopting NeatSharp wants to go beyond the quickstart and understand how to tune NEAT parameters, achieve reproducible results, use checkpointing, and leverage GPU acceleration effectively. A documentation set provides conceptual guides covering NEAT fundamentals as they apply to NeatSharp, parameter tuning guidance, reproducibility and determinism, checkpointing and serialization, and GPU setup with troubleshooting. These guides enable self-service adoption without requiring direct support.

**Why this priority**: Conceptual docs bridge the gap between "I ran the quickstart" and "I'm productively using NeatSharp." Without them, users hit a wall after the initial excitement and abandon the library. However, a working install and examples are prerequisites — hence P6.

**Independent Test**: Can be tested by having a developer attempt to accomplish specific tasks (tune parameters for a new problem, resume from a checkpoint, diagnose a GPU issue) using only the documentation. Success means they can accomplish each task without external help.

**Acceptance Scenarios**:

1. **Given** the documentation set, **When** a user reads the NEAT basics guide, **Then** they understand the core concepts (genomes, species, innovation numbers, complexification) well enough to configure a training run for a new problem.
2. **Given** the documentation set, **When** a user encounters a GPU-related issue (GPU not detected, driver mismatch, out-of-memory), **Then** the troubleshooting guide contains the issue and provides step-by-step resolution instructions.
3. **Given** the documentation set, **When** a user wants to reproduce an experiment, **Then** the reproducibility guide explains how to set seeds, what determinism guarantees exist for CPU vs. GPU, and how to use checkpointing for resumable runs.
4. **Given** the documentation, **When** a user reads the offline usage section, **Then** they understand what works without an internet connection after initial install (all core functionality) and what requires connectivity (NuGet restore for new projects).

---

### User Story 7 - Contribution Guide and Release Process (Priority: P7)

A potential contributor wants to submit a bug fix or feature to NeatSharp. A contribution guide explains how to set up the development environment, run tests, follow code style, and submit PRs. PR templates enforce quality by requiring spec impact analysis and spec ID references. A release checklist and changelog conventions ensure consistent, predictable releases. Maintainers can follow the release checklist to produce a NuGet release candidate from any commit on main.

**Why this priority**: Contribution infrastructure is essential for community growth but is the last priority because it primarily benefits contributors (who arrive after users), and the project must be usable before it can attract contributors.

**Independent Test**: Can be tested by having a new contributor fork the repository, follow the contribution guide to set up their environment, make a small change, and submit a PR using the template. Success means they can complete the workflow without undocumented steps.

**Acceptance Scenarios**:

1. **Given** the contribution guide, **When** a new contributor follows it, **Then** they can clone the repo, build, and run all tests within 10 minutes.
2. **Given** a PR template, **When** a contributor creates a new PR, **Then** the template includes sections for: description, spec impact (which specs are affected), spec IDs, testing performed, and a checklist of common quality checks.
3. **Given** the release checklist, **When** a maintainer follows it, **Then** they can produce a release candidate NuGet package with correct version, changelog entry, and all quality gates passed.
4. **Given** the changelog conventions, **When** reviewing the changelog, **Then** entries are organized by version, categorized (added, changed, fixed, removed), and include spec IDs linking back to feature specifications.

---

### Edge Cases

- What happens when a user tries to install NeatSharp on an unsupported runtime version (e.g., .NET 7)? The NuGet package should clearly indicate supported target frameworks, and the package manager should prevent installation with an informative error.
- What happens when CI runs on a machine without a GPU? GPU-specific tests and benchmarks must be skipped gracefully (not fail), and the build/test gates must still pass for the CPU-only test subset.
- What happens when a contributor runs the benchmark suite on significantly different hardware than the baseline? Contributors maintain their own local baselines. The regression comparison tool compares against the contributor's own previous results (percentage change from their own baseline), not a shared absolute baseline. CI reports benchmark numbers for trend visibility only.
- What happens when the README quickstart references a NuGet package version that hasn't been published yet? The README should reference the latest stable version and the contribution guide should document how to use local package sources for pre-release testing.
- What happens when a formatting rule conflicts with existing code in the repository? The initial formatting enforcement should be applied as a one-time reformatting commit, and subsequent CI checks should only enforce formatting on changed files or all files consistently (no partial enforcement that creates noise).

## Requirements *(mandatory)*

### Functional Requirements

#### NuGet Packaging

- **FR-001**: The project MUST produce NuGet packages for both the core library (NeatSharp) and the GPU library (NeatSharp.Gpu) with complete metadata (package ID, version, authors, description, license (MIT — `<PackageLicenseExpression>MIT</PackageLicenseExpression>`), repository URL, tags, and embedded README). A LICENSE file containing the MIT license text MUST exist at the repository root.
- **FR-002**: The project MUST use Semantic Versioning (SemVer) with an initial release version of 0.1.0. Both packages (NeatSharp, NeatSharp.Gpu) MUST share a consistent version number managed from a single location (e.g., `Directory.Build.props`). The 0.x version range signals pre-release status where the public API may still evolve.
- **FR-003**: Non-library projects (tests, samples) MUST be excluded from NuGet packaging.
- **FR-004**: The NuGet packages MUST be buildable locally by a developer using a single command without requiring any special tooling beyond the .NET SDK.

#### Examples

- **FR-005**: The project MUST include an XOR example that trains a NEAT network to solve XOR, outputs per-generation progress metrics (generation number, best fitness, species count, population size), and prints a final summary showing champion accuracy on all four input combinations.
- **FR-006**: The project MUST include a function approximation example that trains a NEAT network to approximate a continuous mathematical function and reports approximation quality metrics.
- **FR-007**: The project MUST include a Pole Balancing (Cart-Pole) example demonstrating NEAT applied to a sequential control task — the canonical NEAT benchmark. The example simulates a cart-pole system where a neural network controller must balance a pole by applying forces to a cart, with fitness determined by how long the pole remains balanced. This shows NEAT's applicability to sequential decision-making beyond simple input-output function mapping.
- **FR-008**: Each example MUST run independently without requiring the other examples, and each MUST complete within 60 seconds on a modern consumer machine.
- **FR-009**: Each example MUST include inline comments or console output explaining what is happening at each major step (configuration, training, evaluation of results).

#### CI Gates

- **FR-010**: The project MUST have CI pipelines that build and test on both Windows and Linux using all supported target frameworks (net8.0 and net9.0).
- **FR-011**: The CI pipeline MUST include a formatting and style check (`dotnet format` with `.editorconfig` IDE diagnostics and `EnforceCodeStyleInBuild`) that fails the build when code does not conform to project style rules.
- **FR-012**: The CI pipeline MUST include a packaging check that verifies NuGet packages can be produced successfully from the current code.
- **FR-013**: The CI pipeline MUST include a README validation step that checks for broken internal links and ensures required sections per FR-021 are present (at minimum: Installation, Quickstart, Examples, Troubleshooting).
- **FR-014**: All CI checks MUST pass before a pull request can be merged to the main branch.
- **FR-015**: CI pipelines MUST handle the absence of GPU hardware gracefully — GPU-specific tests skip cleanly and do not block the pipeline.

#### Benchmarks and Regression Guardrails

- **FR-016**: The project MUST include a benchmark suite that measures CPU and GPU evaluation throughput across at least three population sizes with statistical rigor (BenchmarkDotNet defaults: automated warmup, multiple iterations, reported mean/standard deviation/confidence intervals).
- **FR-017**: The benchmark suite MUST produce machine-readable output suitable for automated comparison between runs.
- **FR-018**: The CI pipeline MUST run a lightweight benchmark subset and report results for trend visibility (throughput numbers logged in CI output), but MUST NOT hard-fail the build based on benchmark results due to shared runner performance variance. Regression detection is performed locally by contributors comparing against their own machine's baseline using a provided comparison script/tool, with a 10% degradation threshold.
- **FR-019**: The benchmark baseline MUST be updatable by maintainers and stored in version control.
- **FR-020**: The benchmark suite MUST skip GPU benchmarks gracefully when no GPU is available, running only CPU benchmarks.

#### Documentation

- **FR-021**: The README MUST include: project description, feature highlights, installation instructions (CPU-only and GPU), a quickstart code snippet, links to examples, links to conceptual documentation, and a troubleshooting section or link.
- **FR-022**: The project MUST include conceptual documentation covering: NEAT algorithm basics as applied to NeatSharp, parameter tuning guidance, reproducibility and determinism guarantees, checkpointing and serialization usage, and GPU setup and configuration.
- **FR-023**: The project MUST include troubleshooting documentation covering: GPU not detected, driver/toolkit version mismatches, common training stalls (e.g., premature convergence, no fitness improvement), and runtime version issues.
- **FR-024**: The project MUST document offline usage — explaining what functionality works after initial install without internet connectivity.

#### Contribution and Release Process

- **FR-025**: The project MUST include a contribution guide explaining: development environment setup, how to build and test, code style requirements, and PR submission process.
- **FR-026**: The project MUST include a PR template that requires: description, spec impact statement, related spec IDs, testing performed, and a quality checklist.
- **FR-027**: The project MUST include a release checklist documenting the step-by-step process for producing a release candidate NuGet package.
- **FR-028**: The project MUST establish changelog conventions that categorize changes (added, changed, fixed, removed) and reference spec IDs.

### Key Entities

- **NuGet Package**: A distributable unit of the library, containing compiled assemblies, metadata, and an embedded README. Two packages exist: core (NeatSharp) and GPU (NeatSharp.Gpu). Versioned together from a single source of truth.
- **Example**: A self-contained, runnable program demonstrating a specific NeatSharp use case. Each example is independently executable, well-commented, and designed as a copy-paste starting point for users.
- **CI Pipeline**: An automated workflow triggered on pull requests and pushes. Composed of gates (build, test, format, package, docs, benchmark) that must all pass before merging.
- **Benchmark Baseline**: A versioned record of benchmark throughput measurements stored in the repository. Used as the reference point for regression detection. Updated intentionally by maintainers after verified performance changes.
- **Contribution Guide**: A developer-facing document describing the workflow for contributing to NeatSharp, including environment setup, standards, and PR process.
- **Release Checklist**: A step-by-step document that maintainers follow to produce a versioned release, including quality gate verification, changelog update, package build, and publication.

## Assumptions

- The CI platform is GitHub Actions, since the repository is hosted on GitHub. This is the standard CI/CD solution for GitHub-hosted open-source projects.
- The benchmark regression threshold is set at 10% throughput degradation for local regression checks. CI runs benchmarks for trend reporting only (no hard-fail) due to shared runner performance variance. The threshold is configurable and can be adjusted based on experience.
- The "charted progress" for the XOR example means per-generation console output showing fitness metrics in a structured, readable format (not a graphical chart). Users who want graphical charts can redirect the structured output to their preferred visualization tool.
- The third example (black-box optimization) is a Pole Balancing (Cart-Pole) task — the canonical NEAT benchmark. A neural network controller applies forces to a cart to keep a pole balanced, with fitness based on balancing duration. This demonstrates sequential decision-making where fitness depends on a sequence of actions over time.
- Documentation will be authored as Markdown files in the repository (in a `docs/` directory and `README.md`), not hosted on a separate documentation site. This keeps documentation close to code and simplifies maintenance. A documentation site can be added later as a follow-up.
- Code formatting rules will use an `.editorconfig` file and `dotnet format` as the enforcement tool — the standard .NET ecosystem approach requiring no additional tooling.
- The benchmark regression check in CI runs a lightweight subset of benchmarks (not the full suite) to keep CI times reasonable. The full benchmark suite is available for manual runs.
- NuGet packages produced by CI are build artifacts (not published to nuget.org). Publishing is a separate, manual release step covered by the release checklist.
- All examples run CPU-only by default, with optional GPU examples or configuration noted in comments. This ensures examples work on all machines out of the box.

## Non-Goals

- Publishing NuGet packages to nuget.org automatically from CI (manual release process only for now).
- NuGet package signing (deferred to a future release; unsigned packages are standard for new OSS projects at 0.1.0).
- A hosted documentation website (e.g., GitHub Pages, ReadTheDocs). Markdown files in the repository are sufficient for the initial release.
- Code coverage enforcement gates in CI (coverage tooling exists via coverlet but enforcement thresholds are deferred).
- Multi-platform benchmarking infrastructure (benchmarks run on a single CI runner configuration; cross-hardware comparison is out of scope).
- Video tutorials or interactive documentation.
- Automated dependency update tooling (e.g., Dependabot configuration).
- New algorithm features or runtime capabilities beyond what is already specified in features 001–007.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A developer can go from zero to running a NeatSharp example in under 5 minutes using only the README and NuGet package installation — no source cloning, manual builds, or undocumented steps required.
- **SC-002**: All three examples (XOR, function approximation, black-box optimization) run to completion without errors on a fresh clone of the repository, completing within 60 seconds each.
- **SC-003**: The CI pipeline detects and blocks PRs with build failures, test failures, or formatting violations on both Windows and Linux within 10 minutes of submission.
- **SC-004**: A release candidate NuGet package can be produced locally by a maintainer following the release checklist, and the same package can be produced in CI as a build artifact.
- **SC-005**: The local benchmark regression check detects a 10% throughput degradation against the contributor's own baseline, providing a clear warning message about the regression magnitude and affected benchmark. CI benchmark runs report throughput numbers for trend visibility.
- **SC-006**: A new user encountering a common GPU issue (not detected, driver mismatch, out-of-memory) can find and follow resolution steps in the troubleshooting documentation without external support.
- **SC-007**: A new contributor can set up their development environment, build, test, and submit a well-formed PR by following the contribution guide and PR template alone.
- **SC-008**: All conceptual documentation topics (NEAT basics, tuning, reproducibility, checkpointing, GPU usage) are covered, and each guide enables a developer to accomplish its stated task without trial-and-error.
