# Contributing to NeatSharp

Thank you for your interest in contributing to NeatSharp! This guide will help you get set up and submit high-quality contributions.

## Prerequisites

Before you begin, ensure you have the following installed:

- **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)** (LTS) and **[.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)** (Current) -- both are required for multi-targeted builds
- **[Git](https://git-scm.com/)**
- **CUDA Toolkit** (optional) -- only needed for GPU development and running GPU-related tests. See [docs/gpu-setup.md](docs/gpu-setup.md) for version requirements.

## Getting Started

1. **Clone the repository:**

   ```bash
   git clone https://github.com/hdtorrer/neatsharp.git
   cd neatsharp
   ```

2. **Build the solution:**

   ```bash
   dotnet build NeatSharp.sln
   ```

3. **Run tests:**

   ```bash
   dotnet test NeatSharp.sln --filter "Category!=GPU"
   ```

   To also run GPU tests (requires CUDA-capable GPU):

   ```bash
   dotnet test NeatSharp.sln
   ```

## Code Style

NeatSharp enforces code style via `.editorconfig` and the `EnforceCodeStyleInBuild` MSBuild property. CI will reject formatting violations.

- **Before committing**, always run:

  ```bash
  dotnet format NeatSharp.sln
  ```

- Key conventions enforced by `.editorconfig`:
  - File-scoped namespaces (`namespace Foo;`)
  - Explicit accessibility modifiers on all members
  - Braces required for all control flow statements
  - Private instance fields: `_camelCase`
  - Private static fields: `s_camelCase`
  - Constants and types: `PascalCase`

- The CI format check runs:

  ```bash
  dotnet format NeatSharp.sln --verify-no-changes --severity warn
  ```

## Running Examples

The `samples/NeatSharp.Samples` project includes several runnable examples:

```bash
# Default: runs XOR + Sine Approximation examples
dotnet run --project samples/NeatSharp.Samples

# Cart-Pole (inverted pendulum) example
dotnet run --project samples/NeatSharp.Samples -- cart-pole

# GPU benchmark example (requires CUDA GPU)
dotnet run --project samples/NeatSharp.Samples -- benchmark

# Hybrid CPU+GPU benchmark example (requires CUDA GPU)
dotnet run --project samples/NeatSharp.Samples -- hybrid-benchmark
```

## Running Benchmarks

The BenchmarkDotNet-based benchmark suite lives in `benchmarks/NeatSharp.Benchmarks`:

```bash
# Run CPU benchmarks only
dotnet run --project benchmarks/NeatSharp.Benchmarks -c Release -- --filter "*CPU*"

# Run all benchmarks (CPU + GPU, requires CUDA GPU)
dotnet run --project benchmarks/NeatSharp.Benchmarks -c Release

# Run CI-category benchmarks (lightweight subset)
dotnet run --project benchmarks/NeatSharp.Benchmarks -c Release -- --filter "*CI*"

# Export results as JSON for comparison
dotnet run --project benchmarks/NeatSharp.Benchmarks -c Release -- --filter "*CPU*" --exporters json
```

To compare benchmark results against a baseline:

```bash
dotnet run --project tools/benchmark-compare -- --baseline benchmarks/baseline.json --current BenchmarkDotNet.Artifacts/results/*.json --threshold 10
```

## Branch Naming

Use the following conventions for branch names:

- `feature/description` -- new functionality
- `fix/description` -- bug fixes
- `docs/description` -- documentation-only changes

Examples: `feature/add-recurrent-networks`, `fix/crossover-alignment-bug`, `docs/update-gpu-setup`

## Pull Request Process

1. **Create a feature branch** from `main`:

   ```bash
   git checkout -b feature/your-description main
   ```

2. **Make your changes** following the code style guidelines above.

3. **Ensure formatting is clean:**

   ```bash
   dotnet format NeatSharp.sln
   ```

4. **Run tests locally:**

   ```bash
   dotnet test NeatSharp.sln --filter "Category!=GPU"
   ```

5. **Push your branch** and open a pull request against `main` using the PR template.

6. **Address review feedback** -- CI must pass all required checks before merge.

## CI Pipeline

Every pull request and push to `main` triggers the CI pipeline (`.github/workflows/ci.yml`), which runs the following jobs:

| Job | Description | Timeout |
|-----|-------------|---------|
| **Format Check** | Runs `dotnet format --verify-no-changes` on ubuntu-latest | 5 min |
| **Build & Test** | Matrix build across ubuntu-latest + windows-latest with net8.0 + net9.0 (4 combinations). Runs all tests except GPU-category. | 15 min |
| **Pack** | Runs `dotnet pack --configuration Release` and uploads `.nupkg` artifacts | 5 min |
| **Benchmark** | Runs CI-category benchmarks and uploads JSON results (non-blocking, `continue-on-error: true`) | 10 min |

A separate workflow (`.github/workflows/docs.yml`) validates the README on pull requests that modify `README.md` or `docs/**`:

| Job | Description | Timeout |
|-----|-------------|---------|
| **Validate README** | Checks that required sections (Installation, Quickstart, Examples, Troubleshooting) exist and validates links via lychee | 3 min |

All CI jobs use NuGet package caching via `actions/cache@v4` keyed on `*.csproj` and `Directory.Build.props` file hashes.

## Branch Protection

The `main` branch is protected with the following rules:

- All required status checks must pass before merging
- Branches must be up to date before merging
- At least one approving review is required
- Rules are enforced for administrators

To configure (or reconfigure) branch protection via the GitHub CLI:

```bash
gh api repos/hdtorrer/neatsharp/branches/main/protection \
  --method PUT \
  --field required_status_checks='{"strict":true,"contexts":["Format Check","Build & Test (ubuntu-latest, net8.0)","Build & Test (ubuntu-latest, net9.0)","Build & Test (windows-latest, net8.0)","Build & Test (windows-latest, net9.0)","Pack"]}' \
  --field enforce_admins=true \
  --field required_pull_request_reviews='{"required_approving_review_count":1}'
```

## Release Process

Follow this checklist when preparing a new release:

1. **Update the version number** in `Directory.Build.props`:

   ```xml
   <Version>X.Y.Z</Version>
   ```

2. **Update `CHANGELOG.md`**: Move the `## [X.Y.Z] - Unreleased` section to a versioned section with today's date:

   ```markdown
   ## [X.Y.Z] - YYYY-MM-DD
   ```

   Add a new `## [Unreleased]` section above it for future changes.

3. **Verify all CI gates pass on `main`**: Ensure the Format Check, all Build & Test matrix jobs, and Pack jobs are green.

4. **Run the full benchmark suite locally** to confirm no performance regressions:

   ```bash
   dotnet run --project benchmarks/NeatSharp.Benchmarks -c Release -- --exporters json
   dotnet run --project tools/benchmark-compare -- --baseline benchmarks/baseline.json --current BenchmarkDotNet.Artifacts/results/*.json --threshold 10
   ```

5. **Build the NuGet packages:**

   ```bash
   dotnet pack --configuration Release --output ./artifacts/packages
   ```

6. **Inspect `.nupkg` contents** to verify correct metadata, dependencies, and embedded README:

   ```bash
   # List package contents
   dotnet nuget locals global-packages --list
   unzip -l ./artifacts/packages/NeatSharp.X.Y.Z.nupkg
   unzip -l ./artifacts/packages/NeatSharp.Gpu.X.Y.Z.nupkg
   ```

7. **Test package installation from a local source:**

   ```bash
   mkdir /tmp/test-neatsharp && cd /tmp/test-neatsharp
   dotnet new console
   dotnet nuget add source /path/to/neatsharp/artifacts/packages --name local-test
   dotnet add package NeatSharp --version X.Y.Z --source local-test
   dotnet build
   ```

8. **Push packages to NuGet.org:**

   ```bash
   dotnet nuget push ./artifacts/packages/NeatSharp.X.Y.Z.nupkg --api-key <YOUR_API_KEY> --source https://api.nuget.org/v3/index.json
   dotnet nuget push ./artifacts/packages/NeatSharp.Gpu.X.Y.Z.nupkg --api-key <YOUR_API_KEY> --source https://api.nuget.org/v3/index.json
   ```

9. **Create a GitHub release** with the version tag and changelog excerpt:

   ```bash
   git tag vX.Y.Z
   git push origin vX.Y.Z
   gh release create vX.Y.Z --title "vX.Y.Z" --notes "See CHANGELOG.md for details."
   ```
