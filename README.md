# NeatSharp

A GPU-accelerated NEAT (NeuroEvolution of Augmenting Topologies) library for .NET, developed and maintained entirely using AI coding agents ([Claude Code](https://docs.anthropic.com/en/docs/claude-code) + [Speckit](docs/ai-development-workflow.md)).

## Features

- **Parallel CPU evaluation** across multiple cores for faster fitness scoring
- **GPU-accelerated fitness evaluation** via CUDA (ILGPU) for high-throughput training
- **Multi-targeted** .NET 8.0 (LTS) and .NET 9.0
- **Deterministic CPU runs** with seed control for reproducible experiments
- **Hybrid CPU+GPU evaluation** with adaptive partitioning for optimal hardware utilization
- **Versioned checkpoint serialization** for experiment resumption and long-running training
- **Configurable via dependency injection** with sensible defaults using `Microsoft.Extensions.DependencyInjection`

## Installation

**CPU-only:**

```bash
dotnet add package NeatSharp
```

**With GPU acceleration:**

```bash
dotnet add package NeatSharp.Gpu
```

## Quickstart

A complete XOR example that trains a NEAT network to learn the XOR function:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Genetics;

double[][] xorInputs = [[0, 0], [0, 1], [1, 0], [1, 1]];
double[] xorExpected = [0, 1, 1, 0];

var services = new ServiceCollection();
services.AddNeatSharp(options =>
{
    options.InputCount = 2;
    options.OutputCount = 1;
    options.PopulationSize = 150;
    options.Seed = 42;
    options.Stopping.MaxGenerations = 150;
    options.Stopping.FitnessTarget = 3.9;
});
services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();

var result = await evolver.RunAsync(genome =>
{
    double fitness = 0;
    Span<double> output = stackalloc double[1];
    for (int i = 0; i < 4; i++)
    {
        genome.Activate(xorInputs[i], output);
        double error = Math.Abs(xorExpected[i] - output[0]);
        fitness += 1.0 - error;
    }
    return fitness;
});

Console.WriteLine($"Solved: {result.SolvedAtGeneration is not null}");
Console.WriteLine($"Best fitness: {result.Champion.Fitness:F4}");
```

### GPU Quickstart

To enable GPU-accelerated fitness evaluation, add the `NeatSharp.Gpu` package and register GPU services:

```csharp
using NeatSharp.Gpu.Extensions;

services.AddNeatSharp(options =>
{
    options.InputCount = 2;
    options.OutputCount = 1;
    options.PopulationSize = 150;
});
services.AddNeatSharpGpu();
```

> **Note:** GPU acceleration requires a CUDA-compatible GPU and the [CUDA Toolkit](https://developer.nvidia.com/cuda-toolkit) installed on your system. See [GPU Setup](docs/gpu-setup.md) for details.

### Parallel CPU Evaluation

Speed up fitness evaluation by distributing genome evaluations across all CPU cores:

```csharp
using NeatSharp.Configuration;

var result = await evolver.RunAsync(
    genome => MyFitnessFunction(genome),
    new EvaluationOptions { MaxDegreeOfParallelism = null }); // null = all cores
```

> **Note:** Your fitness function must be thread-safe when using parallel evaluation. See [Parallel Evaluation](docs/parallel-evaluation.md) for details.

## Examples

Three runnable examples are included in the `samples/` directory:

| Example | Description | Command |
|---------|-------------|---------|
| **XOR** | Classic boolean function learning | `dotnet run --project samples/NeatSharp.Samples` |
| **Sine Approximation** | Continuous function approximation | `dotnet run --project samples/NeatSharp.Samples` |
| **Cart-Pole** | Inverted pendulum balancing (control task) | `dotnet run --project samples/NeatSharp.Samples -- cart-pole` |
| **Cart-Pole (parallel)** | Cart-Pole with multi-core evaluation | `dotnet run --project samples/NeatSharp.Samples -- cart-pole --parallel` |

The default command runs both XOR and Sine Approximation examples. Use `cart-pole` for the inverted pendulum example, and add `--parallel` to enable multi-core fitness evaluation.

## Documentation

Detailed guides are available in the [`docs/`](docs/) directory:

- [NEAT Basics](docs/neat-basics.md) -- Genomes, species, innovation numbers, and the training loop
- [Parallel Evaluation](docs/parallel-evaluation.md) -- Multi-core CPU evaluation setup and thread-safety
- [Parameter Tuning](docs/parameter-tuning.md) -- Key parameters and recommended starting values
- [Reproducibility](docs/reproducibility.md) -- Determinism guarantees and seed usage patterns
- [Checkpointing](docs/checkpointing.md) -- Save/restore workflow and versioned checkpoint format
- [GPU Setup](docs/gpu-setup.md) -- CUDA prerequisites, configuration, and performance tips
- [Troubleshooting](docs/troubleshooting.md) -- Step-by-step resolutions for common issues
- [Offline Usage](docs/offline-usage.md) -- Running NeatSharp without network connectivity
- [AI Development Workflow](docs/ai-development-workflow.md) -- How this project is built and maintained using Claude Code and Speckit

## Troubleshooting

Common issues and quick pointers:

- **GPU not detected** -- Verify your CUDA Toolkit installation and GPU driver version. See [Troubleshooting](docs/troubleshooting.md) for driver/toolkit compatibility guidance.
- **Training stalls** -- Check your fitness function for flat regions, try increasing mutation rates or adjusting speciation thresholds. See [Troubleshooting](docs/troubleshooting.md) for detailed diagnosis steps.
- **.NET version issues** -- NeatSharp targets .NET 8.0 and .NET 9.0. Ensure you have a compatible SDK installed (`dotnet --list-sdks`). See [Troubleshooting](docs/troubleshooting.md).

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, code style requirements, and PR submission guidelines.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
