# GPU Setup Guide

This guide covers how to set up GPU-accelerated evaluation in NeatSharp, including prerequisites, package installation, configuration, device detection, performance tips, and hybrid CPU+GPU evaluation.

## Prerequisites

### Hardware

- **NVIDIA GPU** with CUDA compute capability 5.0 or higher (Maxwell architecture, circa 2014+).
- Examples of supported GPUs: GeForce GTX 750 Ti and newer, RTX series, Tesla K80 and newer, Quadro series.
- At least 2 GB of GPU VRAM is recommended. More VRAM allows larger populations.

### Software

- **NVIDIA GPU driver**: Version 450.0 or newer. Download from [NVIDIA Drivers](https://www.nvidia.com/drivers).
- **CUDA Toolkit**: Version 11.x or newer. Download from [CUDA Toolkit Downloads](https://developer.nvidia.com/cuda-downloads).
- **.NET SDK**: 8.0 or 9.0 (NeatSharp multi-targets both).

### Verifying Your Setup

After installing drivers and CUDA toolkit, verify with:

```bash
# Check NVIDIA driver and GPU info
nvidia-smi

# Check CUDA toolkit version
nvcc --version
```

## Package Installation

Install both the core NeatSharp package and the GPU package:

```bash
dotnet add package NeatSharp
dotnet add package NeatSharp.Gpu
```

The `NeatSharp.Gpu` package brings in `ILGPU` and `ILGPU.Algorithms` as transitive dependencies. No additional ILGPU packages need to be installed manually.

## Configuration

### Basic GPU Setup

Use `AddNeatSharpGpu()` to register GPU evaluation services alongside the core NeatSharp services:

```csharp
using Microsoft.Extensions.DependencyInjection;
using NeatSharp.Extensions;
using NeatSharp.Gpu.Extensions;

var services = new ServiceCollection();

// Register core NeatSharp services
services.AddNeatSharp(options =>
{
    options.InputCount = 2;
    options.OutputCount = 1;
    options.PopulationSize = 500;
    options.Seed = 42;
    options.Stopping.FitnessTarget = 0.99;
    options.Stopping.MaxGenerations = 300;
});

// Register GPU evaluation services
services.AddNeatSharpGpu(gpu =>
{
    gpu.EnableGpu = true;           // Default: true
    gpu.MinComputeCapability = 50;  // Default: 50 (compute capability 5.0)
});

// Register your GPU fitness function
services.AddSingleton<IGpuFitnessFunction, MyGpuFitnessFunction>();
```

### GpuOptions Reference

| Property                  | Type   | Default | Description                                              |
|---------------------------|--------|---------|----------------------------------------------------------|
| `EnableGpu`               | `bool` | `true`  | Enable/disable GPU evaluation.                           |
| `MinComputeCapability`    | `int`  | `50`    | Minimum CUDA compute capability (major*10 + minor).      |
| `BestEffortDeterministic` | `bool` | `false` | Reserved for future parallel accumulation kernels.       |
| `MaxPopulationSize`       | `int?` | `null`  | Pre-allocate GPU buffers for this population size.       |

### Pre-Allocating GPU Buffers

If you know your population size in advance, pre-allocating buffers avoids reallocation overhead:

```csharp
services.AddNeatSharpGpu(gpu =>
{
    gpu.MaxPopulationSize = 5000; // Pre-allocate for up to 5000 genomes
});
```

## Device Detection

NeatSharp provides `IGpuDeviceDetector` for programmatic GPU discovery. This is registered automatically by `AddNeatSharpGpu()`.

```csharp
var detector = provider.GetRequiredService<IGpuDeviceDetector>();
var device = detector.Detect();

if (device is null)
{
    Console.WriteLine("No compatible GPU detected. Falling back to CPU.");
}
else if (!device.IsCompatible)
{
    Console.WriteLine($"GPU found but not compatible: {device.Diagnostic}");
}
else
{
    Console.WriteLine($"GPU: {device.Name}");
    Console.WriteLine($"Compute Capability: {device.ComputeCapability}");
    Console.WriteLine($"Memory: {device.MemoryBytes / (1024 * 1024)} MB");
}
```

The `IGpuDeviceInfo` interface provides:

| Property            | Type      | Description                                             |
|---------------------|-----------|---------------------------------------------------------|
| `Name`              | `string`  | GPU device name (e.g., "NVIDIA GeForce RTX 3080").      |
| `ComputeCapability` | `Version` | CUDA compute capability (e.g., 8.6).                    |
| `MemoryBytes`       | `long`    | Total GPU memory in bytes.                               |
| `IsCompatible`      | `bool`    | Whether the device meets minimum requirements.           |
| `Diagnostic`        | `string?` | Diagnostic message if the device is not compatible.      |

## Hybrid CPU+GPU Evaluation

For larger populations, NeatSharp can split evaluation across both CPU and GPU simultaneously using hybrid evaluation.

### Setup

Hybrid evaluation requires both GPU and core services, plus the hybrid extension:

```csharp
var services = new ServiceCollection();

// 1. Core services (CPU evaluation)
services.AddNeatSharp(options =>
{
    options.InputCount = 4;
    options.OutputCount = 1;
    options.PopulationSize = 1000;
    options.Stopping.MaxGenerations = 500;
});

// 2. GPU services
services.AddNeatSharpGpu();

// 3. Hybrid services (must be called AFTER AddNeatSharpGpu)
services.AddNeatSharpHybrid(hybrid =>
{
    hybrid.EnableHybrid = true;
    hybrid.SplitPolicy = SplitPolicyType.Adaptive;
    hybrid.MinPopulationForSplit = 50;
});

// Register fitness functions for both CPU and GPU
services.AddSingleton<IFitnessFunction, MyFitnessFunction>();
services.AddSingleton<IGpuFitnessFunction, MyGpuFitnessFunction>();
```

### HybridOptions Reference

| Property                | Type              | Default    | Description                                          |
|-------------------------|-------------------|------------|------------------------------------------------------|
| `EnableHybrid`          | `bool`            | `true`     | Enable/disable hybrid evaluation.                    |
| `SplitPolicy`           | `SplitPolicyType` | `Adaptive` | How to partition genomes between CPU and GPU.        |
| `StaticGpuFraction`     | `double`          | `0.7`      | GPU fraction for static split (0.0=all CPU, 1.0=all GPU). |
| `MinPopulationForSplit` | `int`             | `50`       | Below this threshold, use single backend.            |
| `GpuReprobeInterval`    | `int`             | `10`       | Generations between GPU availability re-probes.      |

### Split Policies

NeatSharp offers three partitioning strategies:

**Static** (`SplitPolicyType.Static`): Fixed ratio split. Simple and predictable.

```csharp
hybrid.SplitPolicy = SplitPolicyType.Static;
hybrid.StaticGpuFraction = 0.7; // 70% GPU, 30% CPU
```

**CostBased** (`SplitPolicyType.CostBased`): Routes complex genomes (many nodes/connections) to GPU and simple genomes to CPU. Best when genome complexity varies significantly.

```csharp
hybrid.SplitPolicy = SplitPolicyType.CostBased;
```

**Adaptive** (`SplitPolicyType.Adaptive`): A PID controller dynamically adjusts the split ratio each generation to balance CPU and GPU completion times. This is the default and recommended for most workloads.

```csharp
hybrid.SplitPolicy = SplitPolicyType.Adaptive;
// Optional: tune PID controller parameters
hybrid.Adaptive.Kp = 0.5;   // Proportional gain
hybrid.Adaptive.Ki = 0.1;   // Integral gain
hybrid.Adaptive.Kd = 0.05;  // Derivative gain
hybrid.Adaptive.InitialGpuFraction = 0.5; // Starting split
```

## Performance Tips

### When GPU Outperforms CPU

GPU evaluation has a fixed overhead for data transfer and kernel launch. The GPU advantage grows with population size:

| Population Size | Recommended Approach    | Notes                                       |
|-----------------|------------------------|---------------------------------------------|
| < 100           | CPU only               | GPU overhead dominates at small sizes.      |
| 100 - 500       | GPU or Hybrid          | GPU starts to provide benefit.              |
| 500 - 2,000     | GPU or Hybrid          | Clear GPU advantage for batch evaluation.   |
| > 2,000         | Hybrid (Adaptive)      | CPU handles overflow while GPU handles bulk.|

### Batch Sizing

- The GPU evaluates all genomes in a single batch (one kernel launch per generation).
- Larger populations amortize the fixed kernel launch cost better.
- If your fitness function is very lightweight, a population of at least 500 is recommended for GPU benefit.

### Memory Considerations

- Each genome requires GPU memory proportional to its node and connection count.
- Monitor GPU memory usage with `nvidia-smi` during runs.
- If running out of GPU memory, reduce `PopulationSize` or use `MaxPopulationSize` to set an upper bound.
- Hybrid evaluation can help: route fewer genomes to GPU, more to CPU.

### Network Complexity

- GPU evaluation provides the greatest speedup for networks with many connections, as the forward propagation kernel parallelizes across genomes.
- For very simple networks (few connections), CPU may be faster due to lower overhead.

## Graceful Fallback

If no compatible GPU is detected at runtime:

- `AddNeatSharpGpu()`: The `GpuBatchEvaluator` will throw a `GpuDeviceException` if GPU evaluation is attempted without a compatible device.
- `AddNeatSharpHybrid()`: The hybrid evaluator automatically falls back to CPU-only evaluation when the GPU is unavailable, with periodic re-probes every `GpuReprobeInterval` generations.

To check GPU availability before starting:

```csharp
var detector = provider.GetRequiredService<IGpuDeviceDetector>();
var device = detector.Detect();

if (device is null || !device.IsCompatible)
{
    Console.WriteLine("No GPU available. Using CPU-only evaluation.");
    // Reconfigure without GPU, or let hybrid handle the fallback
}
```

## Further Reading

- [NEAT Basics](neat-basics.md) -- core NEAT concepts
- [Parameter Tuning Guide](parameter-tuning.md) -- optimizing parameters for GPU workloads
- [Reproducibility Guide](reproducibility.md) -- GPU floating-point considerations
- [Troubleshooting Guide](troubleshooting.md) -- GPU detection and driver issues
