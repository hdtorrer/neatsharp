# Quickstart: GPU-Accelerated NEAT Evaluation

## Prerequisites

- .NET 8.0 or .NET 9.0 SDK
- NVIDIA GPU with CUDA compute capability >= 5.0 (GTX 750 Ti or newer)
- NVIDIA driver with CUDA runtime support (driver version >= 450.x)
- No build-time CUDA SDK required — ILGPU compiles kernels at runtime

## Installation

```bash
dotnet add package NeatSharp
dotnet add package NeatSharp.Gpu
```

## Basic Usage: XOR with GPU Evaluation

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeatSharp.Evaluation;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Gpu.Extensions;
using NeatSharp.Gpu.Evaluation;

// 1. Define your fitness function for GPU evaluation
var xorFitness = new XorFitnessFunction();

// 2. Configure services
var services = new ServiceCollection();
services.AddNeatSharp(options =>
{
    options.InputCount = 2;
    options.OutputCount = 1;
    options.PopulationSize = 500;
    options.Seed = 42;
    options.Stopping.MaxGenerations = 200;
    options.Stopping.FitnessTarget = 3.9;
});
services.AddSingleton<IGpuFitnessFunction>(xorFitness);  // Register your fitness function
services.AddNeatSharpGpu();  // Auto-detects GPU; falls back to CPU if unavailable
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));

// 3. Build and run
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();
var batchEvaluator = scope.ServiceProvider.GetRequiredService<IBatchEvaluator>();

var result = await evolver.RunAsync(batchEvaluator);

Console.WriteLine($"Champion fitness: {result.Champion.Fitness}");
Console.WriteLine($"Generations: {result.GenerationsCompleted}");
```

## Defining a GPU Fitness Function

Implement `IGpuFitnessFunction` to define your evaluation protocol:

```csharp
using NeatSharp.Gpu.Evaluation;

public class XorFitnessFunction : IGpuFitnessFunction
{
    // XOR truth table: 4 test cases, 2 inputs each
    private static readonly float[] Inputs =
    [
        0f, 0f,   // Case 0: inputs (0, 0)
        0f, 1f,   // Case 1: inputs (0, 1)
        1f, 0f,   // Case 2: inputs (1, 0)
        1f, 1f    // Case 3: inputs (1, 1)
    ];

    private static readonly float[] Expected = [0f, 1f, 1f, 0f];

    public int CaseCount => 4;

    public int OutputCount => 1;

    public ReadOnlyMemory<float> InputCases => Inputs;

    public double ComputeFitness(ReadOnlySpan<float> outputs)
    {
        // outputs: [caseCount * outputCount] = [4 * 1] = 4 values
        double fitness = 0;
        for (int i = 0; i < CaseCount; i++)
        {
            double error = Math.Abs(Expected[i] - outputs[i]);
            fitness += 1.0 - error;
        }
        return fitness;  // Max: 4.0 (perfect XOR)
    }
}
```

## GPU Configuration Options

```csharp
services.AddNeatSharpGpu(gpu =>
{
    gpu.EnableGpu = true;                    // Default: true (auto-detect)
    gpu.MinComputeCapability = 50;           // Default: 50 (CC 5.0, Maxwell)
    gpu.BestEffortDeterministic = false;     // Default: false
    gpu.MaxPopulationSize = 2000;            // Optional: preallocate for known size
});
```

## Force CPU-Only (Disable GPU)

```csharp
services.AddNeatSharpGpu(gpu =>
{
    gpu.EnableGpu = false;  // GPU package loaded but GPU evaluation disabled
});
```

## How It Works

1. **GPU Detection**: On first evaluation, the system detects available GPUs and checks compatibility. Results are cached.
2. **Network Building**: `GpuNetworkBuilder` produces `GpuFeedForwardNetwork` instances that carry both CPU-fallback phenotypes and GPU-ready flat topology arrays.
3. **Batch Upload**: All genome topologies + test case inputs are uploaded to the GPU as contiguous flat arrays.
4. **Parallel Evaluation**: A GPU kernel evaluates all genomes across all test cases simultaneously (one thread per genome).
5. **Fitness Computation**: Outputs are downloaded and fitness is computed on the CPU via your `IGpuFitnessFunction.ComputeFitness()`.
6. **Automatic Fallback**: If the GPU fails or is unavailable, evaluation transparently falls back to CPU for that generation.

## Logging

The GPU evaluator logs key events:

| Level | Event |
|-------|-------|
| Information | GPU detected: device name, compute capability, memory |
| Information | GPU evaluation selected / CPU fallback selected |
| Warning | GPU evaluation failed; falling back to CPU for this generation |
| Warning | GPU device incompatible: diagnostic details |
| Error | GPU out of memory: population size, memory estimate |

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| "No compatible GPU detected" | No NVIDIA GPU or CC < 5.0 | Install NVIDIA GPU with CC >= 5.0 or set `EnableGpu = false` |
| "CUDA runtime not found" | Missing NVIDIA driver | Install latest NVIDIA driver from [nvidia.com/drivers](https://www.nvidia.com/drivers) |
| GPU OOM exception | Population too large for GPU memory | Reduce `PopulationSize` or use CPU evaluation |
| Results differ from CPU | Expected: fp32 vs fp64 precision | Differences within 1e-4 are normal; check `IGpuFitnessFunction` for numeric stability |
