# Reproducibility Guide

This guide explains how to produce deterministic, reproducible results with NeatSharp, including CPU determinism guarantees, GPU floating-point considerations, seed management, and combining seeds with checkpointing.

## CPU Determinism

NeatSharp guarantees **bit-exact reproducibility** on CPU when you set a fixed seed. Given the same seed, input count, output count, population size, and all other configuration parameters, the evolution run will produce identical results every time.

### Setting a Fixed Seed

```csharp
services.AddNeatSharp(options =>
{
    options.Seed = 42;  // Fixed seed for deterministic execution
    options.InputCount = 2;
    options.OutputCount = 1;
    options.PopulationSize = 150;
    options.Stopping.FitnessTarget = 0.99;
    options.Stopping.MaxGenerations = 300;
});
```

When `Seed` is set to a non-null value, the internal random number generator is initialized with that exact seed. All stochastic operations -- mutation, crossover, selection, initial weight generation -- use this single deterministic RNG, producing identical sequences across runs.

### Auto-Generated Seeds

When `Seed` is `null` (the default), NeatSharp generates a seed automatically using `Random.Shared.Next()`. The generated seed is recorded in the `EvolutionResult.Seed` property, so you can reproduce the run later:

```csharp
var result = await evolver.EvolveAsync(CancellationToken.None);

// Record the seed for future reproduction
Console.WriteLine($"Seed used: {result.Seed}");
// Output: "Seed used: 1836274951"

// Later, to reproduce:
options.Seed = 1836274951;
```

## GPU Floating-Point Considerations

When using the GPU evaluator (`NeatSharp.Gpu`), network evaluation happens on the NVIDIA GPU via ILGPU. The current NeatSharp GPU kernel implementation uses sequential accumulation (one thread per genome, fixed topological order), which is inherently deterministic.

However, be aware of these general GPU floating-point considerations:

### Floating-Point Precision

GPU and CPU may produce slightly different results for the same computation due to:

- **Different floating-point rounding modes**: GPU hardware may use fused multiply-add (FMA) instructions that combine operations differently from CPU.
- **Precision differences**: While both use IEEE 754 double-precision, instruction scheduling and optimization may cause minor differences.

### Epsilon Tolerance for Comparisons

When comparing CPU and GPU results for the same genome, use an epsilon tolerance rather than exact equality:

```csharp
const double Epsilon = 1e-4; // Recommended tolerance per output

double cpuOutput = cpuNetwork.Activate(inputs)[0];
double gpuOutput = gpuNetwork.Activate(inputs)[0];

bool areClose = Math.Abs(cpuOutput - gpuOutput) < Epsilon;
```

The recommended tolerance of approximately 1e-4 per output accommodates accumulated floating-point differences across multiple network layers.

### GPU-to-GPU Reproducibility

Runs on the **same GPU** with the same seed and configuration will produce identical results, as the current kernel is deterministic. Different GPU models may produce slightly different results due to hardware-level floating-point behavior.

## Seed Usage Patterns

### Fixed Seed for Research

When conducting experiments where reproducibility is critical:

```csharp
// Each experiment gets a unique fixed seed
var seeds = new[] { 42, 123, 456, 789, 1024 };

foreach (var seed in seeds)
{
    services.AddNeatSharp(options =>
    {
        options.Seed = seed;
        // ... other options stay the same ...
    });

    var result = await evolver.EvolveAsync(CancellationToken.None);
    Console.WriteLine($"Seed {seed}: fitness={result.ChampionFitness}, gen={result.Generation}");
}
```

This allows you to:
- Report results with specific seeds for others to verify.
- Debug issues by reproducing the exact run that exhibited a problem.
- Compare parameter changes fairly (same seeds, different configurations).

### Null Seed for Exploration

When exploring the solution space or running production workloads where diversity matters:

```csharp
services.AddNeatSharp(options =>
{
    options.Seed = null;  // Default -- auto-generated
    // ...
});

var result = await evolver.EvolveAsync(CancellationToken.None);

// Always record the auto-generated seed for potential reproduction
Console.WriteLine($"Run completed with seed: {result.Seed}");
```

### Seed Ranges for Batch Experiments

For systematic experimentation across multiple configurations:

```csharp
var configurations = new[]
{
    ("baseline", new Action<NeatSharpOptions>(o => { /* defaults */ })),
    ("high-mutation", new Action<NeatSharpOptions>(o => { o.Mutation.AddNodeRate = 0.08; })),
    ("large-pop", new Action<NeatSharpOptions>(o => { o.PopulationSize = 500; })),
};

var seeds = Enumerable.Range(1, 10).ToArray(); // Seeds 1 through 10

foreach (var (name, configure) in configurations)
{
    foreach (var seed in seeds)
    {
        services.AddNeatSharp(options =>
        {
            options.Seed = seed;
            options.InputCount = 2;
            options.OutputCount = 1;
            options.Stopping.MaxGenerations = 300;
            configure(options);
        });

        var result = await evolver.EvolveAsync(CancellationToken.None);
        Console.WriteLine($"{name}, seed={seed}: fitness={result.ChampionFitness}");
    }
}
```

## Combining Seeds with Checkpointing

Checkpointing and seeds work together to enable **fully resumable, deterministic experiments**. When a checkpoint is saved, it captures the complete RNG state along with the seed and all evolution state.

### Saving a Deterministic Checkpoint

```csharp
// Configure with a fixed seed
services.AddNeatSharp(options =>
{
    options.Seed = 42;
    options.Stopping.MaxGenerations = 1000;
    // ...
});

// After some generations, save a checkpoint
using var stream = File.Create("checkpoint-gen100.json");
await serializer.SaveAsync(stream, checkpoint);
```

The checkpoint includes the `RngState` (internal state of the random number generator), so resuming from this checkpoint with the same configuration will produce the same results as if the run had continued without interruption.

### Resuming Deterministically

```csharp
// Load the checkpoint
using var stream = File.OpenRead("checkpoint-gen100.json");
var checkpoint = await serializer.LoadAsync(stream);

// The checkpoint contains the seed, RNG state, and configuration
// Resume evolution -- it will continue deterministically from where it left off
```

### Important Notes for Deterministic Resumption

1. **Same library version**: Use the same version of NeatSharp that created the checkpoint. Schema version compatibility is enforced automatically.
2. **Same configuration**: The loaded checkpoint contains the original configuration. Modifying configuration after loading may change the evolution trajectory.
3. **CPU only**: Deterministic resumption is guaranteed only for CPU evaluation. GPU evaluation may have minor floating-point differences as described above.
4. **Same .NET runtime**: Different .NET runtime versions may have different floating-point behavior in edge cases. For strict reproducibility, use the same runtime version.

## Checklist for Reproducible Experiments

- [ ] Set `options.Seed` to a fixed value for each run.
- [ ] Record the full `NeatSharpOptions` configuration alongside results.
- [ ] Use CPU evaluation for bit-exact reproducibility.
- [ ] When using GPU, apply epsilon tolerance (~1e-4) for cross-platform comparisons.
- [ ] Save checkpoints at regular intervals for long-running experiments.
- [ ] Report the NeatSharp version, .NET runtime version, and OS in experiment logs.
- [ ] When comparing configurations, keep seeds constant and vary only the parameter under test.

## Further Reading

- [NEAT Basics](neat-basics.md) -- core NEAT concepts and NeatSharp types
- [Parameter Tuning Guide](parameter-tuning.md) -- recommended parameters for different problems
- [Checkpointing Guide](checkpointing.md) -- save and resume training runs
- [GPU Setup Guide](gpu-setup.md) -- GPU evaluation configuration
