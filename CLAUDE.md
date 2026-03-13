# neatsharp Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-02-12

## Active Technologies
- N/A (in-memory; genome data structures only) (002-genome-phenotype)
- C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted + Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (003-evolution-operators)
- N/A (in-memory only) (004-training-runner)
- C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>` + System.Text.Json (in-box), Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (005-versioned-serialization)
- Stream-based I/O (no filesystem dependency; `System.IO.Stream` abstraction) (005-versioned-serialization)
- C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>` + ILGPU 1.5.x, ILGPU.Algorithms 1.5.x (GPU package only); Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (shared with core) (006-cuda-evaluator)
- N/A (in-memory GPU buffers; `ILGPU.MemoryBuffer1D<T>` for device memory) (006-cuda-evaluator)
- C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>` + Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (shared with core and GPU); ILGPU 1.5.x (transitive via NeatSharp.Gpu) (007-hybrid-eval-scheduler)
- N/A (in-memory only; PID controller state and metrics held in-memory per scope) (007-hybrid-eval-scheduler)
- C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>` + Existing — Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2, ILGPU 1.5.3, ILGPU.Algorithms 1.5.3; New — BenchmarkDotNet (benchmarks project only, not shipped in NuGet packages) (008-release-readiness)
- N/A (no new data storage; benchmark baselines stored as JSON files in version control) (008-release-readiness)
- C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted + Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (existing; no new dependencies) (009-parallel-cpu-eval)

- C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>` + Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (001-core-api-baseline)

## Project Structure

```text
src/
  NeatSharp/                    # Core NEAT library (net8.0;net9.0)
  NeatSharp.Gpu/                # GPU acceleration via ILGPU (net8.0;net9.0)
tests/
  NeatSharp.Tests/              # Core tests (net8.0;net9.0)
  NeatSharp.Gpu.Tests/          # GPU tests (net8.0;net9.0)
samples/
  NeatSharp.Samples/            # Examples: XOR, Sine, Cart-Pole, benchmarks (net9.0)
benchmarks/
  NeatSharp.Benchmarks/         # BenchmarkDotNet suite (net9.0)
tools/
  benchmark-compare/            # Benchmark regression comparison tool (net9.0)
docs/                           # Conceptual documentation (Markdown)
.github/
  workflows/                    # CI workflows (ci.yml, docs.yml)
  pull_request_template.md      # PR template
```

## Commands

```bash
# Build
dotnet build NeatSharp.sln

# Test (exclude GPU-only tests)
dotnet test NeatSharp.sln --filter "Category!=GPU"

# Format check (CI gate)
dotnet format NeatSharp.sln --verify-no-changes --severity warn

# Apply formatting
dotnet format NeatSharp.sln

# Pack NuGet packages
dotnet pack NeatSharp.sln --configuration Release

# Run examples
dotnet run --project samples/NeatSharp.Samples                    # XOR + Sine
dotnet run --project samples/NeatSharp.Samples -- cart-pole        # Cart-Pole
dotnet run --project samples/NeatSharp.Samples -- benchmark        # GPU benchmark
dotnet run --project samples/NeatSharp.Samples -- hybrid-benchmark # Hybrid benchmark

# Run BenchmarkDotNet suite
dotnet run --project benchmarks/NeatSharp.Benchmarks -c Release -- --filter "*CPU*"
dotnet run --project benchmarks/NeatSharp.Benchmarks -c Release -- --filter "*CI*" --exporters json

# Compare benchmark results
dotnet run --project tools/benchmark-compare -- --baseline benchmarks/baseline.json --current results.json --threshold 10
```

## Code Style

- `.editorconfig` enforced via `dotnet format` CI gate and `EnforceCodeStyleInBuild`
- Run `dotnet format NeatSharp.sln` before committing
- File-scoped namespaces, `_camelCase` private fields, `PascalCase` public types
- See `.editorconfig` for full rule set

## Recent Changes
- 009-parallel-cpu-eval: Added C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted + Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (existing; no new dependencies)
- 008-release-readiness: Added C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>` + Existing — Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2, ILGPU 1.5.3, ILGPU.Algorithms 1.5.3; New — BenchmarkDotNet (benchmarks project only, not shipped in NuGet packages)
- 007-hybrid-eval-scheduler: Added C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>` + Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (shared with core and GPU); ILGPU 1.5.x (transitive via NeatSharp.Gpu)


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
