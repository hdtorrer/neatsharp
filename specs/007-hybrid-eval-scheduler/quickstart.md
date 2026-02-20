# Quickstart: Hybrid CPU+GPU Evaluation

## Prerequisites

- .NET 8.0 or .NET 9.0 SDK
- NVIDIA GPU with CUDA compute capability >= 5.0 (GTX 750 Ti or newer)
- NVIDIA driver with CUDA runtime support (driver version >= 450.x)
- `NeatSharp` and `NeatSharp.Gpu` packages installed

## Installation

```bash
dotnet add package NeatSharp
dotnet add package NeatSharp.Gpu
```

## Basic Usage: Hybrid Evaluation with Adaptive Partitioning

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NeatSharp.Evaluation;
using NeatSharp.Evolution;
using NeatSharp.Extensions;
using NeatSharp.Gpu.Configuration;
using NeatSharp.Gpu.Extensions;
using NeatSharp.Gpu.Evaluation;

// 1. Define your fitness function
var fitness = new XorFitnessFunction();

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
services.AddSingleton<IGpuFitnessFunction>(fitness);
services.AddNeatSharpGpu();

// 3. Enable hybrid evaluation (adaptive partitioning is the default)
services.AddNeatSharpHybrid();

services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));

// 4. Build and run
using var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();
var evaluator = scope.ServiceProvider.GetRequiredService<IBatchEvaluator>();

var result = await evolver.RunAsync(evaluator);

Console.WriteLine($"Champion fitness: {result.Champion.Fitness}");
Console.WriteLine($"Generations: {result.GenerationsCompleted}");
```

## Configuration Examples

### Static Split (Fixed 70/30 GPU/CPU)

```csharp
services.AddNeatSharpHybrid(hybrid =>
{
    hybrid.SplitPolicy = SplitPolicyType.Static;
    hybrid.StaticGpuFraction = 0.7;  // 70% GPU, 30% CPU
});
```

### Cost-Based Partitioning (Complexity-Driven)

```csharp
services.AddNeatSharpHybrid(hybrid =>
{
    hybrid.SplitPolicy = SplitPolicyType.CostBased;
    hybrid.CostModel.NodeWeight = 1.0;
    hybrid.CostModel.ConnectionWeight = 1.0;
});
```

### Adaptive Partitioning with Custom PID Gains

```csharp
services.AddNeatSharpHybrid(hybrid =>
{
    hybrid.SplitPolicy = SplitPolicyType.Adaptive;
    hybrid.Adaptive.Kp = 0.5;
    hybrid.Adaptive.Ki = 0.1;
    hybrid.Adaptive.Kd = 0.05;
    hybrid.Adaptive.InitialGpuFraction = 0.5;
});
```

### Disable Hybrid (Passthrough to Inner Evaluator)

```csharp
services.AddNeatSharpHybrid(hybrid =>
{
    hybrid.EnableHybrid = false;  // Zero overhead passthrough
});
```

### Custom GPU Failure Recovery

```csharp
services.AddNeatSharpHybrid(hybrid =>
{
    hybrid.GpuReprobeInterval = 20;     // Wait 20 generations before re-probing
    hybrid.MinPopulationForSplit = 100;  // Only split populations >= 100
});
```

## How It Works

1. **Registration Order**: `AddNeatSharp()` → `AddNeatSharpGpu()` → `AddNeatSharpHybrid()`. The hybrid evaluator decorates the GPU evaluator, which itself decorates the CPU path.

2. **Partitioning**: Each generation, the active policy assigns genomes to CPU or GPU:
   - **Static**: Fixed ratio, no adaptation.
   - **Cost-Based**: Complex genomes → GPU, simple genomes → CPU.
   - **Adaptive**: PID controller targets zero idle-time difference between backends.

3. **Concurrent Dispatch**: CPU and GPU backends evaluate their partitions simultaneously via `Task.WhenAll`.

4. **Result Merging**: Each backend calls `setFitness(originalIndex, score)` with remapped indices, merging results back into the original order without additional synchronization.

5. **GPU Failure Handling**: If the GPU fails mid-generation, unevaluated genomes are rerouted to CPU. Subsequent generations use CPU-only until the GPU re-probe succeeds.

6. **Metrics**: Per-generation scheduling metrics are emitted via `ISchedulingMetricsReporter`, including throughput, latency, split ratio, and fallback events.

## Logging

The hybrid evaluator logs key events:

| Level | Event |
|-------|-------|
| Information | Hybrid evaluation enabled: policy, backend counts |
| Information | Per-generation metrics summary (throughput, split ratio) |
| Information | GPU re-probe succeeded; resuming hybrid evaluation |
| Warning | GPU failure detected; falling back to CPU (includes failure reason, genome count rerouted) |
| Warning | Population below split threshold; using single backend |
| Debug | PID controller state (error, integral, GPU fraction adjustment) |

## Accessing Scheduling Metrics Programmatically

```csharp
// Implement ISchedulingMetricsReporter for custom metrics handling
public class MyMetricsCollector : ISchedulingMetricsReporter
{
    public List<SchedulingMetrics> History { get; } = new();

    public void Report(SchedulingMetrics metrics)
    {
        History.Add(metrics);
        Console.WriteLine($"Gen {metrics.Generation}: " +
            $"CPU={metrics.CpuGenomeCount} ({metrics.CpuThroughput:F0} g/s), " +
            $"GPU={metrics.GpuGenomeCount} ({metrics.GpuThroughput:F0} g/s), " +
            $"Split={metrics.SplitRatio:P0}");
    }
}

// Register your custom reporter
var collector = new MyMetricsCollector();
services.AddSingleton<ISchedulingMetricsReporter>(collector);
services.AddNeatSharpHybrid();

// After training, analyze convergence
var ratios = collector.History.Select(m => m.SplitRatio).ToList();
var variance = /* compute variance over last 5 generations */;
Console.WriteLine($"Split ratio variance (last 5 gens): {variance:F4}");
```
