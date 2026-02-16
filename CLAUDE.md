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

- C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>` + Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (001-core-api-baseline)

## Project Structure

```text
src/
tests/
```

## Commands

# Add commands for C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>`

## Code Style

C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>`: Follow standard conventions

## Recent Changes
- 006-cuda-evaluator: Added C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>` + ILGPU 1.5.x, ILGPU.Algorithms 1.5.x (GPU package only); Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2 (shared with core)
- 005-versioned-serialization: Added C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted via `<TargetFrameworks>net8.0;net9.0</TargetFrameworks>` + System.Text.Json (in-box), Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2
- 004-training-runner: Added C# 13 / .NET 8.0 (LTS) + .NET 9.0 (Current) — multi-targeted + Microsoft.Extensions.DependencyInjection.Abstractions 8.0.2, Microsoft.Extensions.Options 8.0.2, Microsoft.Extensions.Logging.Abstractions 8.0.2


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
