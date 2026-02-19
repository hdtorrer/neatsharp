# Research: Release Readiness

**Feature**: 008-release-readiness | **Date**: 2026-02-18

## R1: Benchmark Strategy — BenchmarkDotNet vs Hand-Rolled

### Decision: Hybrid strategy — BenchmarkDotNet in a dedicated project; retain hand-rolled in Samples

### Rationale

The spec requires statistical rigor with reported variance (FR-016), machine-readable output for automated comparison (FR-017), and a regression comparison tool with a 10% threshold (FR-018/SC-005). The existing hand-rolled benchmarks in `NeatSharp.Samples` produce only a single mean throughput figure with no variance, standard deviation, or confidence intervals. The benchmark report from feature 006 explicitly acknowledges: "Use BenchmarkDotNet for publication-quality data."

BenchmarkDotNet provides:
- Statistical rigor: warmup, pilot phase, outlier detection, confidence intervals
- Machine-readable JSON/CSV export via `--exporters json`
- Standardized JSON schema for automated comparison (Mean, StdDev, Median, Min, Max)
- Category-based filtering via `[BenchmarkCategory]` for CI lightweight subsets
- Industry-standard .NET benchmarking that contributors will expect

### GPU Lifecycle Compatibility

BenchmarkDotNet's lifecycle maps naturally to ILGPU:
- `[GlobalSetup]`: Create `GpuBatchEvaluator` with `Context` and `Accelerator`
- `[Benchmark]`: Single `EvaluateAsync` call per iteration
- `[GlobalCleanup]`: Dispose evaluator and GPU resources

For hybrid benchmarks, the `ServiceCollection` build and `ServiceProvider` creation happen in `[GlobalSetup]`, the `IBatchEvaluator` is resolved once, and `[GlobalCleanup]` disposes the provider. This matches the existing hand-rolled pattern.

### GPU-Skip Mechanism

GPU benchmarks are tagged `[BenchmarkCategory("GPU")]`. In `[GlobalSetup]`, if `GpuDeviceDetector.Detect()` returns null, a skip flag is set and the `[Benchmark]` method returns immediately. CI uses `--filter "*" --category-exclusion-filter GPU` or an explicit CPU-only filter.

### CI Lightweight Subset

Use `[BenchmarkCategory("CI")]` on a small-population benchmark (e.g., 500 genomes) with a `ShortRunJob` attribute. CI runs: `dotnet run --configuration Release -- --filter "*CI*" --exporters json`. JSON output uploaded as a CI artifact for trend visibility. No hard-fail gate.

### Why Retain Hand-Rolled Benchmarks

The `NeatSharp.Samples` benchmarks serve a different purpose: runnable demos and quick integration checks that developers can invoke with `dotnet run -- benchmark` without requiring Release mode or BenchmarkDotNet's minimum run constraints. Removing them would reduce accessibility.

### Alternatives Considered

- **Extend hand-rolled benchmarks with JSON output and variance**: Duplicates BenchmarkDotNet functionality with known pitfalls (GC interference, JIT in warmup, timer resolution). More code to maintain with less reliability.
- **Replace hand-rolled entirely**: Loses the quick-smoke-test developer workflow. BenchmarkDotNet requires Release mode and takes 30+ seconds minimum.

---

## R2: GitHub Actions CI Configuration

### Decision: Two workflows — `ci.yml` (primary) and `docs.yml` (README validation)

### Rationale

README validation has different tooling requirements (no .NET SDK needed), runs in <30 seconds, and should not trigger the full build matrix when only docs change. Separating it avoids wasted runner minutes.

### Workflow Structure

**`ci.yml` jobs:**
1. **format** — `dotnet format NeatSharp.sln --verify-no-changes --severity warn` on `ubuntu-latest` (single runner, no matrix)
2. **build-and-test** — matrix: `os: [ubuntu-latest, windows-latest]` x `tfm: [net8.0, net9.0]` = 4 cells
3. **pack** — `dotnet pack NeatSharp.sln --configuration Release --output ./artifacts/packages` on `ubuntu-latest`; upload `.nupkg` as artifact
4. **benchmark** — lightweight BenchmarkDotNet subset on `ubuntu-latest`; upload JSON results as artifact; no hard-fail

**`docs.yml` jobs:**
1. **validate-readme** — lychee link checker + shell grep for required sections

### Matrix Strategy

Both .NET 8.0 and 9.0 SDKs installed on every runner (avoids restoration edge cases). `--framework` flag restricts test execution per matrix cell. The Samples project (net9.0-only) builds on all cells but tests only run for matching TFM.

### GPU Test Filtering

`dotnet test --filter "Category!=GPU"` — xUnit trait-based filtering. Currently, most GPU tests use ILGPU's CPU accelerator and don't need the `[Trait("Category", "GPU")]` annotation. The filter will only exclude future tests that require real CUDA hardware.

### Timeouts and Concurrency

| Job | Timeout |
|-----|---------|
| format | 5 min |
| build-and-test (per cell) | 15 min |
| pack | 5 min |
| benchmark | 10 min |
| validate-readme | 3 min |

Concurrency: `group: ${{ github.workflow }}-${{ github.ref }}`, `cancel-in-progress: true`.

NuGet package caching via `actions/cache@v4` keyed on `*.csproj` + `Directory.Build.props` hash.

### README Validation Tooling

- **Link checking**: lychee (`lycheeverse/lychee-action`) — fast Rust-based link checker with GitHub Actions first-party integration. Handles internal anchor links and external URLs.
- **Required sections**: Shell grep checking for specific Markdown headings (Installation, Quickstart, Examples, Troubleshooting, etc.). Zero external dependencies.

### Alternatives Considered

- **Single workflow for everything**: Couples doc validation to .NET build; wastes runner minutes on doc-only changes.
- **macOS runner**: Spec only requires Windows + Linux (FR-010). macOS triples matrix cost for no stated benefit.
- **markdown-link-check (Node.js)**: Slower, heavier dependency than lychee.

---

## R3: Cart-Pole (Inverted Pendulum) Physics

### Decision: Standard Barto/Stanley single-pole balancing with canonical parameters

### Physics Equations

State variables: cart position (x), cart velocity (x_dot), pole angle (theta), pole angular velocity (theta_dot).

Standard Euler integration per time step:

```
sin_theta = sin(theta)
cos_theta = cos(theta)
total_mass = cart_mass + pole_mass

temp = (force + pole_mass * half_length * theta_dot^2 * sin_theta) / total_mass

theta_ddot = (g * sin_theta - cos_theta * temp)
             / (half_length * (4/3 - (pole_mass * cos_theta^2) / total_mass))

x_ddot = temp - (pole_mass * half_length * theta_ddot * cos_theta) / total_mass

x         += dt * x_dot
x_dot     += dt * x_ddot
theta     += dt * theta_dot
theta_dot += dt * theta_ddot
```

### Standard Parameters

| Parameter | Value |
|-----------|-------|
| Gravity (g) | 9.8 m/s^2 |
| Cart mass (m_c) | 1.0 kg |
| Pole mass (m_p) | 0.1 kg |
| Pole half-length (l) | 0.5 m |
| Force magnitude (F) | +/- 10.0 N |
| Time step (dt) | 0.02 s (50 Hz) |

### Failure Conditions

- Cart position: |x| > 2.4 m
- Pole angle: |theta| > 12 degrees (0.2094 radians)

### Fitness Function

`fitness = steps_survived / max_steps` where `max_steps = 100,000` (matching Stanley 2002). Normalized to [0.0, 1.0].

For the example, a reduced `max_steps` of 10,000 is used to keep runtime under 60 seconds (FR-008), with the "solved" threshold at 0.95 (9,500+ steps balanced).

### Neural Network Interface

- **Inputs (4)**: x, x_dot, theta, theta_dot (raw values, no normalization — matching Stanley's original)
- **Outputs (1)**: force direction (output > 0.5 → push right +10N, else push left -10N)

### "Solved" Criterion

A network that survives 100,000 time steps (2,000 simulated seconds) is considered solved. Stanley reports NEAT solving single-pole in ~100 generations with population 150.

### Alternatives Considered

- **Double-pole without velocity**: Harder benchmark but more complex to implement. Deferred — single-pole is sufficient to demonstrate sequential control.
- **Proportional force (tanh output)**: More nuanced but binary force is the canonical NEAT benchmark approach.
- **Input normalization**: Not used in Stanley's original; raw values work fine for NEAT's adaptive networks.

---

## R4: EditorConfig and Code Formatting

### Decision: `.editorconfig` with `dotnet format --verify-no-changes` CI gate; `.gitattributes` for line ending normalization

### Key Rules

**Enforced in CI (severity = warning via `dotnet_diagnostic.IDE####.severity`):**
- `IDE0055` — all whitespace/formatting rules (auto-fixable)
- `IDE1006` — naming rule violations (_camelCase fields, PascalCase types)
- `IDE0011` — brace enforcement
- `IDE0160`/`IDE0161` — file-scoped namespace declarations
- `IDE0040` — accessibility modifier requirements

**Suggestion-only (not CI-blocking):**
- `IDE0007`/`IDE0008` — `var` preferences (not always auto-fixable)
- Expression-bodied member preferences
- Pattern matching preferences

### Private Field Naming (_camelCase)

Requires two naming rules in editorconfig:
1. Private instance fields → `_camelCase` (prefix `_`, camelCase capitalization)
2. Private static fields → `s_camelCase` (prefix `s_`, camelCase capitalization)
3. Private const fields → `PascalCase` (override to prevent `s_` prefix on constants)

Enforcement requires both `dotnet_naming_rule.*.severity = warning` AND `dotnet_diagnostic.IDE1006.severity = warning`.

### Line Endings

Use `.gitattributes` with `* text=auto eol=lf` to normalize line endings to LF. This prevents `dotnet format --verify-no-changes` from failing on Windows due to CRLF/LF mismatch between editorconfig and git checkout behavior.

### Known Gotchas

1. **One-time reformatting required**: Run `dotnet format NeatSharp.sln` locally and commit before activating the CI gate.
2. **Inline severity (`:warning`) unreliable on .NET 8**: Use explicit `dotnet_diagnostic.IDE####.severity` for all CI-enforced rules.
3. **`dotnet format` requires restore**: Always run `dotnet restore` before `dotnet format --verify-no-changes` in CI.
4. **LINQ query syntax enforcement**: No built-in Roslyn rule prevents query syntax. Enforced via code review only (per constitution: "LINQ expressions MUST prefer method syntax").

### Alternatives Considered

- **`<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`**: Useful for local dev feedback but not a replacement for `dotnet format` as CI gate. Both can be used together.
- **Third-party analyzers (Meziantou.Analyzer, StyleCop)**: Add LINQ enforcement but also add a new dependency. Deferred — `dotnet format` with built-in analyzers is sufficient for initial release.
- **`end_of_line` in editorconfig without `.gitattributes`**: Fragile on Windows with `core.autocrlf = true`. `.gitattributes` is the correct approach.

---

## R5: NuGet Packaging Metadata

### Decision: Shared metadata in `Directory.Build.props`; package-specific metadata in individual `.csproj` files

### `Directory.Build.props` (shared)

| Property | Value |
|----------|-------|
| Version | 0.1.0 |
| Authors | hdtorresr |
| PackageLicenseExpression | MIT |
| RepositoryUrl | https://github.com/hdtorrer/neatsharp |
| RepositoryType | git |
| Copyright | Copyright 2026 hdtorresr |
| PackageProjectUrl | https://github.com/hdtorrer/neatsharp |

### Per-project `.csproj` (unique per package)

**NeatSharp.csproj:**
- PackageId: NeatSharp
- Description: A GPU-accelerated NEAT (NeuroEvolution of Augmenting Topologies) library for .NET
- PackageTags: neat;neuroevolution;neural-network;genetic-algorithm;machine-learning;dotnet
- PackageReadmeFile: README.md (include via `<None Include="../../README.md" Pack="true" PackagePath="/"/>`)

**NeatSharp.Gpu.csproj:**
- PackageId: NeatSharp.Gpu
- Description: GPU acceleration (CUDA via ILGPU) for the NeatSharp NEAT library
- PackageTags: neat;neuroevolution;gpu;cuda;ilgpu;neural-network;dotnet
- PackageReadmeFile: README.md (same include pattern)

### Exclusions

Test projects already have `<IsPackable>false</IsPackable>`. The Samples project needs `<IsPackable>false</IsPackable>` added. The Benchmarks project will have `<IsPackable>false</IsPackable>`.

### Rationale

Single version source (Directory.Build.props) satisfies FR-002. Package-specific descriptions and tags enable correct NuGet discoverability. README embedded in both packages per FR-001 metadata requirements.
