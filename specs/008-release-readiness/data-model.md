# Data Model: Release Readiness

**Feature**: 008-release-readiness | **Date**: 2026-02-18

## Entities

### CartPoleState

Runtime state for the Cart-Pole physics simulation. Updated each time step via Euler integration.

| Field | Type | Initial Value | Description |
|-------|------|---------------|-------------|
| X | `double` | 0.0 | Cart position on track (meters) |
| XDot | `double` | 0.0 | Cart velocity (m/s) |
| Theta | `double` | 0.0 | Pole angle from vertical (radians) |
| ThetaDot | `double` | 0.0 | Pole angular velocity (rad/s) |

**Failure conditions**:
- `|X| > 2.4` — cart off track
- `|Theta| > 0.2094` — pole fallen (12 degrees)

**State transitions**: Updated each simulation step by `CartPoleSimulator.Step()`. State is mutable during simulation, created fresh per evaluation episode.

---

### CartPoleConfig

Immutable configuration for the Cart-Pole physics simulation. Uses canonical Barto/Stanley parameters.

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| Gravity | `double` | 9.8 | Gravitational acceleration (m/s^2) |
| CartMass | `double` | 1.0 | Mass of the cart (kg) |
| PoleMass | `double` | 0.1 | Mass of the pole (kg) |
| PoleHalfLength | `double` | 0.5 | Half-length of the pole (m) |
| ForceMagnitude | `double` | 10.0 | Force applied to cart (+/- N) |
| TimeStep | `double` | 0.02 | Simulation time step (s) — 50 Hz |
| MaxSteps | `int` | 10_000 | Maximum simulation steps before success |
| TrackHalfLength | `double` | 2.4 | Half-length of the track (m) |
| FailureAngle | `double` | 0.2094 | Maximum pole angle before failure (radians, ~12 degrees) |

---

### BenchmarkBaseline

JSON schema for the versioned benchmark baseline stored in `benchmarks/baseline.json`. This is a subset of the BenchmarkDotNet JSON export format — only the fields needed for regression comparison.

| Field | Type | Description |
|-------|------|-------------|
| Timestamp | `string` (ISO 8601) | When the baseline was captured |
| Machine | `MachineInfo` | Machine identifier for the baseline |
| Results | `BenchmarkResult[]` | Array of individual benchmark results |

### MachineInfo

| Field | Type | Description |
|-------|------|-------------|
| Cpu | `string` | CPU model name |
| Gpu | `string?` | GPU model name (null if no GPU) |
| Os | `string` | Operating system and version |
| DotnetVersion | `string` | .NET SDK version |

### BenchmarkResult

| Field | Type | Description |
|-------|------|-------------|
| FullName | `string` | BenchmarkDotNet fully-qualified method name (e.g., `CpuEvaluatorBenchmarks.Evaluate(PopulationSize: 1000)`) |
| Mean | `double` | Mean execution time in nanoseconds |
| StdDev | `double` | Standard deviation in nanoseconds |
| Median | `double` | Median execution time in nanoseconds |
| ThroughputGenomesPerSecond | `double` | Derived: (PopulationSize / Mean_seconds) |

**Note**: The comparison tool reads BenchmarkDotNet's native JSON export format. The baseline schema above describes the fields the tool cares about — BenchmarkDotNet JSON contains many additional fields that are ignored.

---

### NuGetPackageMetadata

Metadata structure for NuGet packages, split between shared (Directory.Build.props) and per-package (.csproj) properties.

**Shared properties (Directory.Build.props):**

| Property | Value | Source |
|----------|-------|--------|
| Version | 0.1.0 | FR-002 |
| Authors | hdtorresr | Repository owner |
| PackageLicenseExpression | MIT | FR-001, constitution |
| RepositoryUrl | https://github.com/hdtorrer/neatsharp | FR-001 |
| RepositoryType | git | Standard |
| Copyright | Copyright 2026 hdtorresr | Standard |
| PackageProjectUrl | https://github.com/hdtorrer/neatsharp | Standard |

**Per-package properties:**

| Package | PackageId | Description | Tags |
|---------|-----------|-------------|------|
| NeatSharp | NeatSharp | GPU-accelerated NEAT library for .NET | neat;neuroevolution;neural-network;genetic-algorithm;machine-learning;dotnet |
| NeatSharp.Gpu | NeatSharp.Gpu | GPU acceleration (CUDA via ILGPU) for NeatSharp | neat;neuroevolution;gpu;cuda;ilgpu;neural-network;dotnet |

---

### CIPipelineGates

Logical model of the CI pipeline gates and their pass/fail criteria.

| Gate | Job | Trigger | Pass Criteria | Blocks Merge |
|------|-----|---------|---------------|--------------|
| Format | format | PR to main | `dotnet format --verify-no-changes` exits 0 | Yes |
| Build | build-and-test | PR to main | `dotnet build` succeeds on all 4 matrix cells | Yes |
| Test | build-and-test | PR to main | `dotnet test --filter "Category!=GPU"` passes on all 4 matrix cells | Yes |
| Pack | pack | PR to main | `dotnet pack` produces .nupkg without errors | Yes |
| Benchmark | benchmark | PR to main | Always passes (trend reporting only) | No |
| README Links | validate-readme | PR to main | lychee finds no broken links | Yes |
| README Sections | validate-readme | PR to main | All required headings present | Yes |

---

## Relationships

```text
Directory.Build.props ──defines──→ Version (0.1.0)
       │                            ├──→ NeatSharp.csproj (inherits + adds package-specific metadata)
       │                            └──→ NeatSharp.Gpu.csproj (inherits + adds package-specific metadata)
       │
       └──defines──→ Shared metadata (Authors, License, RepositoryUrl)

.editorconfig ──enforced by──→ dotnet format ──checked by──→ CI format gate

CartPoleConfig ──configures──→ CartPoleSimulator ──produces──→ CartPoleState (per step)
                                      │
                                      └── used by ──→ CartPoleExample (fitness = steps / maxSteps)

BenchmarkDotNet ──produces──→ JSON export ──compared by──→ benchmark-compare tool
                                                                │
                                                                ├── reads ──→ baseline.json (version control)
                                                                └── reads ──→ current results (local run)

ci.yml ──orchestrates──→ [format, build-and-test, pack, benchmark] gates
docs.yml ──orchestrates──→ [validate-readme] gate
```

## Data Flow

### Cart-Pole Evaluation (per genome)

```text
1. CartPoleExample creates CartPoleSimulator with CartPoleConfig
2. For each genome in population:
   a. Build FeedForwardNetwork from genome
   b. Reset CartPoleState to initial (all zeros)
   c. Loop for maxSteps:
      i.   Feed state [x, x_dot, theta, theta_dot] to network
      ii.  Get output → force direction (left/right)
      iii. CartPoleSimulator.Step(force) → update CartPoleState
      iv.  Check failure conditions → break if failed
   d. fitness = steps_survived / maxSteps
3. Report champion: steps survived, final state visualization
```

### Benchmark Regression Check (local)

```text
1. Developer runs full benchmark suite → BenchmarkDotNet JSON export
2. Developer runs: benchmark-compare --baseline baseline.json --current results.json --threshold 10
3. Tool reads both JSON files, matches by FullName
4. For each matched pair: compute % change = ((current.Mean - baseline.Mean) / baseline.Mean) * 100
5. If any benchmark shows >10% degradation:
   - Report: benchmark name, baseline Mean, current Mean, % change
   - Exit code 1 (warning)
6. If no regressions: exit code 0, summary of all comparisons
```
