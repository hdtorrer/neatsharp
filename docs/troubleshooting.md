# Troubleshooting Guide

This guide provides step-by-step resolutions for common issues encountered when using NeatSharp.

## GPU Not Detected

**Symptom**: `IGpuDeviceDetector.Detect()` returns `null`, or GPU evaluation throws a `GpuDeviceException`.

### Step 1: Verify NVIDIA Driver Installation

```bash
nvidia-smi
```

If this command fails or is not found:

1. Download the latest driver from [NVIDIA Drivers](https://www.nvidia.com/drivers).
2. Install the driver and reboot.
3. Run `nvidia-smi` again to confirm.

Expected output includes your GPU name, driver version, and CUDA version.

### Step 2: Verify CUDA Toolkit Installation

```bash
nvcc --version
```

If this command fails:

1. Download CUDA Toolkit 11.x or newer from [CUDA Downloads](https://developer.nvidia.com/cuda-downloads).
2. Install with default options.
3. Ensure the CUDA `bin` directory is in your system `PATH`.
4. Restart your terminal and retry.

### Step 3: Check GPU Compute Capability

NeatSharp requires compute capability 5.0 or higher (configurable via `GpuOptions.MinComputeCapability`).

Use `IGpuDeviceDetector` to get diagnostic information:

```csharp
var detector = provider.GetRequiredService<IGpuDeviceDetector>();
var device = detector.Detect();

if (device is not null && !device.IsCompatible)
{
    Console.WriteLine(device.Diagnostic);
    // Example: "GPU device 'GeForce GT 730' has compute capability 3.5,
    //           but NeatSharp requires at least 5.0."
}
```

If your GPU's compute capability is below 5.0, you need a newer GPU (Maxwell architecture or later, circa 2014+).

### Step 4: Check for Missing CUDA Runtime Libraries

If `Detect()` returns a device with a diagnostic about missing runtime libraries:

```
CUDA runtime libraries not found. Install the NVIDIA CUDA toolkit
from https://developer.nvidia.com/cuda-downloads and ensure
compatible NVIDIA GPU drivers are installed.
```

This means the CUDA DLLs (`nvcuda.dll` on Windows, `libcuda.so` on Linux) are not accessible. Ensure:

1. The CUDA Toolkit is installed.
2. The CUDA library paths are in your system's library search path.
3. On Linux: check `ldconfig -p | grep cuda` to see if CUDA libraries are indexed.

## Driver/Toolkit Version Mismatch

**Symptom**: GPU is detected but evaluation fails, or `nvidia-smi` shows a different CUDA version than `nvcc --version`.

### Compatibility Matrix

| CUDA Toolkit | Minimum Driver (Windows) | Minimum Driver (Linux) |
|-------------|--------------------------|------------------------|
| 11.0        | 451.22                   | 450.36                 |
| 11.8        | 452.39                   | 450.80                 |
| 12.0        | 525.60                   | 525.60                 |
| 12.3        | 545.23                   | 545.23                 |
| 12.4+       | 550.54                   | 550.54                 |

### Resolution

1. Run `nvidia-smi` to check your installed driver version.
2. Run `nvcc --version` to check your CUDA Toolkit version.
3. Consult the compatibility table above or the [CUDA Compatibility Guide](https://docs.nvidia.com/deploy/cuda-compatibility/).
4. If the driver is too old for your toolkit, update the driver.
5. If you need a specific CUDA toolkit version, install it alongside your current driver (toolkit and driver can be updated independently).

## Out-of-Memory Errors

**Symptom**: `GpuOutOfMemoryException` during evaluation, or `nvidia-smi` shows GPU memory fully utilized.

### Step 1: Check GPU VRAM

```bash
nvidia-smi
```

Look at the "Memory-Usage" column. If it is near the maximum:

### Step 2: Reduce Population Size

The primary driver of GPU memory usage is the number of genomes evaluated simultaneously:

```csharp
options.PopulationSize = 500; // Reduce from larger values
```

### Step 3: Use Hybrid Evaluation

Split the population between CPU and GPU to reduce GPU memory pressure:

```csharp
services.AddNeatSharpHybrid(hybrid =>
{
    hybrid.SplitPolicy = SplitPolicyType.Static;
    hybrid.StaticGpuFraction = 0.5; // Only 50% of genomes on GPU
});
```

### Step 4: Pre-Allocate with a Cap

Prevent unexpected buffer growth by setting a maximum:

```csharp
services.AddNeatSharpGpu(gpu =>
{
    gpu.MaxPopulationSize = 1000; // Cap GPU buffer allocation
});
```

### Step 5: Close Other GPU Applications

Other applications (browsers with hardware acceleration, other ML workloads, video encoding) may be consuming GPU memory. Check `nvidia-smi` for other processes using the GPU.

## Training Stalls

### Premature Convergence

**Symptom**: Population collapses to 1-2 species early, fitness plateaus, and no further improvement occurs.

**Causes and Solutions**:

1. **Speciation threshold too high**: Lower `CompatibilityThreshold` to create more species.
   ```csharp
   options.Speciation.CompatibilityThreshold = 1.5; // Down from 3.0
   ```

2. **Speciation coefficients too low**: Increase structural distance sensitivity.
   ```csharp
   options.Speciation.ExcessCoefficient = 2.0;   // Up from 1.0
   options.Speciation.DisjointCoefficient = 2.0;  // Up from 1.0
   ```

3. **Population too small**: Increase population for more diversity.
   ```csharp
   options.PopulationSize = 300; // Up from 150
   ```

4. **Survival threshold too high**: Restrict parent selection to top performers.
   ```csharp
   options.Selection.SurvivalThreshold = 0.1; // Down from 0.2
   ```

### No Fitness Improvement

**Symptom**: Best fitness does not improve across many generations, or improves extremely slowly.

**Check 1: Fitness Function**

The most common cause is a poorly designed fitness function. Ensure:

- **Smooth gradient**: The fitness function provides partial credit, not just pass/fail. For example, for XOR:
  ```csharp
  // Good: smooth gradient
  fitness = 1.0 - (sumOfErrors / numTestCases);

  // Bad: binary pass/fail
  fitness = allCorrect ? 1.0 : 0.0;
  ```

- **Scale is appropriate**: Fitness values should have meaningful differences between mediocre and good solutions.

**Check 2: Structural Mutation Rates**

If the problem requires hidden nodes but mutation rates are too low:

```csharp
options.Mutation.AddNodeRate = 0.05;       // Up from 0.03
options.Mutation.AddConnectionRate = 0.10; // Up from 0.05
```

**Check 3: Weight Perturbation Power**

If weights need finer adjustment:

```csharp
options.Mutation.PerturbationPower = 0.3; // Down from 0.5
```

If weights need larger exploration:

```csharp
options.Mutation.PerturbationPower = 1.0; // Up from 0.5
options.Mutation.WeightMinValue = -8.0;   // Wider range
options.Mutation.WeightMaxValue = 8.0;
```

**Check 4: Stagnation Detection**

Enable global stagnation stopping to prevent wasting compute on stuck runs:

```csharp
options.Stopping.StagnationThreshold = 50; // Stop after 50 gens with no improvement
```

### Species Explosion

**Symptom**: Species count grows rapidly (50+ species for population 150), many species have only 1-2 members.

**Solution**: Raise the compatibility threshold:

```csharp
options.Speciation.CompatibilityThreshold = 5.0; // Up from 3.0
```

Or reduce the weight difference coefficient so weight differences contribute less to distance:

```csharp
options.Speciation.WeightDifferenceCoefficient = 0.2; // Down from 0.4
```

## .NET Runtime Version Issues

### Symptom: Build Errors

NeatSharp targets both .NET 8.0 and .NET 9.0. Ensure you have at least one installed:

```bash
dotnet --list-sdks
```

If neither 8.x nor 9.x is listed, download from [.NET Downloads](https://dotnet.microsoft.com/download).

### Symptom: Runtime Exceptions on Startup

If you see `TypeLoadException`, `MissingMethodException`, or `FileNotFoundException` for NeatSharp assemblies:

1. Ensure your project targets `net8.0` or `net9.0`:
   ```xml
   <TargetFramework>net8.0</TargetFramework>
   ```

2. Ensure all NeatSharp package versions match:
   ```bash
   dotnet list package
   ```

3. Clean and rebuild:
   ```bash
   dotnet clean
   dotnet build
   ```

### Symptom: Reproducibility Differences Across Runtimes

Different .NET runtime versions may produce slightly different floating-point results in edge cases. For strict reproducibility, pin the runtime version:

```xml
<!-- In your .csproj -->
<RuntimeFrameworkVersion>8.0.x</RuntimeFrameworkVersion>
```

Or use a global.json to pin the SDK:

```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestPatch"
  }
}
```

## Diagnostic Checklist

When reporting an issue, include the following information:

- [ ] .NET SDK version (`dotnet --version`)
- [ ] NeatSharp package version (`dotnet list package`)
- [ ] Operating system and version
- [ ] GPU model and driver version (`nvidia-smi`, if applicable)
- [ ] CUDA Toolkit version (`nvcc --version`, if applicable)
- [ ] Full exception message and stack trace
- [ ] `NeatSharpOptions` configuration used
- [ ] Seed value (for reproducible issues)

## Further Reading

- [GPU Setup Guide](gpu-setup.md) -- detailed GPU configuration
- [Parameter Tuning Guide](parameter-tuning.md) -- adjusting parameters for better results
- [Reproducibility Guide](reproducibility.md) -- deterministic execution
- [Offline Usage Guide](offline-usage.md) -- working without network connectivity
