# Quickstart: Release Readiness Implementation

## Implementation Order

This feature spans 7 user stories across multiple workstreams. The recommended implementation order respects dependencies:

### Phase A: Foundation (blocks everything else)

**A1. NuGet Packaging Metadata** (FR-001, FR-002, FR-003, FR-004)
- Modify `Directory.Build.props`: add `Version`, `Authors`, `PackageLicenseExpression`, `RepositoryUrl`, `RepositoryType`, `Copyright`, `PackageProjectUrl`
- Modify `src/NeatSharp/NeatSharp.csproj`: add `PackageId`, `Description`, `PackageTags`, `PackageReadmeFile` (include README.md via `<None Include="../../README.md" Pack="true" PackagePath="/"/>`)
- Modify `src/NeatSharp.Gpu/NeatSharp.Gpu.csproj`: same per-package metadata
- Add `<IsPackable>false</IsPackable>` to `samples/NeatSharp.Samples/NeatSharp.Samples.csproj`
- Verify: `dotnet pack NeatSharp.sln --configuration Release` produces two `.nupkg` files with correct metadata

**A2. EditorConfig and Formatting** (FR-011)
- Create `.editorconfig` at repo root with rules per research.md R4
- Create `.gitattributes` with `* text=auto eol=lf`
- Run `dotnet format NeatSharp.sln` to apply one-time reformatting
- Commit the reformatting as a standalone commit (before CI gate activation)
- Add `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` to `Directory.Build.props`

### Phase B: CI Infrastructure (depends on A)

**B1. Primary CI Workflow** (FR-010, FR-011, FR-012, FR-014, FR-015)
- Create `.github/workflows/ci.yml` with jobs:
  - `format`: `dotnet format NeatSharp.sln --verify-no-changes --severity warn`
  - `build-and-test`: matrix `[ubuntu-latest, windows-latest]` x `[net8.0, net9.0]`; `dotnet test --filter "Category!=GPU"`
  - `pack`: `dotnet pack --configuration Release --output ./artifacts/packages`; upload artifact
- Set up NuGet cache with `actions/cache@v4`
- Set concurrency: `group: ci-${{ github.ref }}`, `cancel-in-progress: true`

**B2. README Validation Workflow** (FR-013)
- Create `.github/workflows/docs.yml` with jobs:
  - `validate-readme`: lychee link checker + grep for required sections
- Required sections to validate: `## Installation`, `## Quickstart`, `## Examples`, `## Troubleshooting`

### Phase C: Examples (independent of B, depends on A)

**C1. Cart-Pole Example** (FR-007, FR-008, FR-009)
- Create `samples/NeatSharp.Samples/CartPole/CartPoleConfig.cs` — simulation parameters as a record
- Create `samples/NeatSharp.Samples/CartPole/CartPoleSimulator.cs` — Euler integration physics
- Create `samples/NeatSharp.Samples/CartPole/CartPoleExample.cs` — DI setup, training loop, console output
- Modify `samples/NeatSharp.Samples/Program.cs` — add `"cart-pole"` command
- Create unit tests for CartPoleSimulator in `tests/NeatSharp.Tests/` (physics determinism, failure conditions, known-state assertions)
- Verify: example completes within 60 seconds, prints per-generation metrics, shows champion balancing result

### Phase D: Benchmarks (independent of B and C, depends on A)

**D1. BenchmarkDotNet Project** (FR-016, FR-017, FR-020)
- Create `benchmarks/NeatSharp.Benchmarks/NeatSharp.Benchmarks.csproj` (net9.0, BenchmarkDotNet, IsPackable=false)
- Add project to `NeatSharp.sln` in a `benchmarks` solution folder
- Create `CpuEvaluatorBenchmarks.cs` — `[Params(150, 500, 1000, 5000)]` population sizes
- Create `GpuEvaluatorBenchmarks.cs` — same params, `[GlobalSetup]` with GPU detection guard
- Create `HybridEvaluatorBenchmarks.cs` — partition policy comparison
- Tag CI-suitable benchmarks with `[BenchmarkCategory("CI")]`
- Verify: `dotnet run --configuration Release` produces BenchmarkDotNet report with stats

**D2. Regression Comparison Tool** (FR-018, FR-019)
- Create `tools/benchmark-compare/benchmark-compare.csproj` (net9.0 console, System.Text.Json only)
- Implement: read two BDN JSON files, match by FullName, compute % change on Mean
- Report any >10% degradation with clear message (benchmark name, baseline, current, delta)
- Exit code 1 on regression, 0 on pass
- Create initial `benchmarks/baseline.json` (run benchmarks locally, save output)

**D3. CI Benchmark Job** (FR-018)
- Add `benchmark` job to `.github/workflows/ci.yml`
- Run lightweight subset: `--filter "*CI*" --exporters json`
- Upload JSON results as CI artifact for trend visibility
- No hard-fail gate

### Phase E: Documentation (depends on A and C)

**E1. README** (FR-021)
- Rewrite `README.md` with sections:
  - Project description and feature highlights
  - Installation (CPU-only: `dotnet add package NeatSharp`; GPU: also add `NeatSharp.Gpu`)
  - Quickstart code snippet (XOR example, copy-paste ready)
  - GPU quickstart variant
  - Links to examples
  - Links to conceptual docs
  - Troubleshooting section (or link to docs/troubleshooting.md)
  - License, contributing link

**E2. Conceptual Documentation** (FR-022, FR-023, FR-024)
- Create `docs/neat-basics.md` — genomes, species, innovation numbers, complexification
- Create `docs/parameter-tuning.md` — key parameters, starting values, adjustment strategies
- Create `docs/reproducibility.md` — CPU determinism with seeds, GPU epsilon tolerance, seed usage
- Create `docs/checkpointing.md` — serialization API, save/restore workflow, versioned format
- Create `docs/gpu-setup.md` — prerequisites (CUDA toolkit, driver), ILGPU configuration, performance tips
- Create `docs/troubleshooting.md` — GPU not detected, driver mismatch, OOM, training stalls, runtime version issues
- Create `docs/offline-usage.md` — what works offline, what requires NuGet restore

### Phase F: Contribution Infrastructure (depends on B and E)

**F1. Contribution Guide** (FR-025)
- Create `CONTRIBUTING.md` — development setup, build commands, test commands, code style, PR process

**F2. PR Template** (FR-026)
- Create `.github/pull_request_template.md` — description, spec impact, spec IDs, testing performed, quality checklist

**F3. Release Process** (FR-027, FR-028)
- Create release checklist (in CONTRIBUTING.md or separate `docs/release-process.md`)
- Create `CHANGELOG.md` with initial `## [0.1.0] - Unreleased` entry
- Document changelog conventions: added/changed/fixed/removed categories with spec ID references

## Verification

After all phases complete:

1. **SC-001**: Create empty console project → `dotnet add package NeatSharp` → paste README quickstart → runs successfully
2. **SC-002**: Run all three examples from clone → all complete < 60s
3. **SC-003**: Submit PR with formatting violation → CI fails within 10 min
4. **SC-004**: `dotnet pack NeatSharp.sln --configuration Release` → produces valid `.nupkg` files
5. **SC-005**: Introduce artificial slowdown → `benchmark-compare` reports regression
6. **SC-006**: Follow troubleshooting docs for common GPU issue → resolution steps exist
7. **SC-007**: New contributor follows CONTRIBUTING.md → builds, tests, submits PR
8. **SC-008**: All conceptual doc topics covered and actionable
