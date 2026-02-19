# Checkpointing Guide

This guide covers how to save and restore NeatSharp training runs using the checkpointing API, including the checkpoint data model, versioned format, integrity validation, and patterns for resuming interrupted training.

## Overview

NeatSharp provides a stream-based checkpointing API that serializes the complete state of an evolution run to JSON. Checkpoints capture everything needed to resume training from the exact point where it was saved, including the population, species assignments, RNG state, and configuration.

## Key Types

| Type                    | Namespace                | Description                                                |
|-------------------------|--------------------------|------------------------------------------------------------|
| `ICheckpointSerializer` | `NeatSharp.Serialization`| Saves and loads `TrainingCheckpoint` instances.             |
| `TrainingCheckpoint`    | `NeatSharp.Serialization`| Complete snapshot of an evolution run at a generation boundary. |
| `ICheckpointValidator`  | `NeatSharp.Serialization`| Validates structural integrity of loaded checkpoints.      |
| `SchemaVersion`         | `NeatSharp.Serialization`| Schema version constants and compatibility checks.         |

## ICheckpointSerializer API

The `ICheckpointSerializer` interface provides two methods:

```csharp
public interface ICheckpointSerializer
{
    Task SaveAsync(Stream stream, TrainingCheckpoint checkpoint,
                   CancellationToken cancellationToken = default);

    Task<TrainingCheckpoint> LoadAsync(Stream stream,
                                       CancellationToken cancellationToken = default);
}
```

Both methods work with `System.IO.Stream`, so they are agnostic to the underlying storage (file system, network stream, in-memory buffer, cloud blob storage, etc.).

## TrainingCheckpoint Contents

A `TrainingCheckpoint` captures the full state of an evolution run:

| Field                | Type                          | Description                                      |
|----------------------|-------------------------------|--------------------------------------------------|
| `Population`         | `IReadOnlyList<Genome>`       | All genomes in the current generation.           |
| `Species`            | `IReadOnlyList<SpeciesCheckpoint>` | Species snapshots with genome references.   |
| `NextInnovationNumber` | `int`                      | Innovation tracker counter state.                |
| `NextNodeId`         | `int`                         | Node ID counter state.                           |
| `NextSpeciesId`      | `int`                         | Species ID counter state.                        |
| `ChampionGenome`     | `Genome`                      | Best genome found so far.                        |
| `ChampionFitness`    | `double`                      | Fitness of the champion.                         |
| `ChampionGeneration` | `int`                         | Generation when champion was found.              |
| `Generation`         | `int`                         | Current generation number.                       |
| `Seed`               | `int`                         | Random seed used for the run.                    |
| `RngState`           | `RngState`                    | Internal RNG state for deterministic resumption. |
| `Configuration`      | `NeatSharpOptions`            | Full configuration used for the run.             |
| `ConfigurationHash`  | `string`                      | SHA-256 hash of the serialized configuration.    |
| `History`            | `RunHistory`                  | Generation-by-generation statistics.             |
| `Metadata`           | `ArtifactMetadata`            | Schema version, timestamps, environment info.    |

## Save Workflow

### Saving to a File

```csharp
using Microsoft.Extensions.DependencyInjection;
using NeatSharp.Extensions;
using NeatSharp.Serialization;

// Set up DI (AddNeatSharp registers ICheckpointSerializer automatically)
var services = new ServiceCollection();
services.AddNeatSharp(options =>
{
    options.InputCount = 2;
    options.OutputCount = 1;
    options.PopulationSize = 150;
    options.Seed = 42;
    options.Stopping.MaxGenerations = 1000;
});

var provider = services.BuildServiceProvider();
var serializer = provider.GetRequiredService<ICheckpointSerializer>();

// After obtaining a TrainingCheckpoint (e.g., from a callback or after evolution):
async Task SaveCheckpointAsync(TrainingCheckpoint checkpoint, string path)
{
    using var stream = File.Create(path);
    await serializer.SaveAsync(stream, checkpoint);
    Console.WriteLine($"Checkpoint saved: generation {checkpoint.Generation}, " +
                      $"champion fitness {checkpoint.ChampionFitness}");
}
```

### Saving to a Memory Stream

```csharp
using var memoryStream = new MemoryStream();
await serializer.SaveAsync(memoryStream, checkpoint);

byte[] checkpointBytes = memoryStream.ToArray();
// Store bytes anywhere: database, cloud blob, network transfer, etc.
```

## Load Workflow

### Loading from a File

```csharp
async Task<TrainingCheckpoint> LoadCheckpointAsync(string path)
{
    using var stream = File.OpenRead(path);
    var checkpoint = await serializer.LoadAsync(stream);
    Console.WriteLine($"Checkpoint loaded: generation {checkpoint.Generation}, " +
                      $"population size {checkpoint.Population.Count}");
    return checkpoint;
}
```

### Loading with Error Handling

```csharp
try
{
    using var stream = File.OpenRead("checkpoint.json");
    var checkpoint = await serializer.LoadAsync(stream);
}
catch (SchemaVersionException ex)
{
    Console.WriteLine($"Incompatible checkpoint version: {ex.Message}");
    Console.WriteLine($"Checkpoint version: {ex.ActualVersion}, " +
                      $"Required: {ex.ExpectedVersion}");
}
catch (CheckpointCorruptionException ex)
{
    Console.WriteLine($"Checkpoint is corrupted: {ex.Message}");
}
```

## Versioned Format

### Schema Version

Checkpoints use a versioned JSON schema. The current schema version is:

```csharp
SchemaVersion.Current   // "1.0.0"
SchemaVersion.MinimumSupported  // "1.0.0"
```

When loading a checkpoint:
1. The schema version is read from the JSON.
2. If it matches `SchemaVersion.Current`, it is deserialized directly.
3. If it falls between `MinimumSupported` and `Current`, migration is attempted.
4. If it is outside the supported range, a `SchemaVersionException` is thrown.

### Version Compatibility Checks

```csharp
// Check if a version string is compatible
bool compatible = SchemaVersion.IsCompatible("1.0.0"); // true

// Check if migration is needed
bool needsMigration = SchemaVersion.NeedsMigration("1.0.0"); // false (already current)
```

## Checkpoint Validation

The `ICheckpointValidator` validates the structural integrity of a loaded checkpoint by checking cross-references between population, species, and counters.

```csharp
var validator = provider.GetRequiredService<ICheckpointValidator>();

var result = validator.Validate(checkpoint);

if (result.IsValid)
{
    Console.WriteLine("Checkpoint integrity verified.");
}
else
{
    Console.WriteLine("Checkpoint validation failed:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}
```

Validation checks include:
- Population is not empty.
- All species reference valid genomes in the population.
- Innovation number and node ID counters are consistent with the population's gene data.
- Champion genome exists in the population.
- Configuration hash matches the stored configuration.

## Resuming Interrupted Training

### Pattern: Periodic Checkpointing

Save checkpoints at regular intervals during a long training run. If the process is interrupted, resume from the latest checkpoint.

```csharp
// This is a conceptual example showing the checkpoint-resume pattern.
// The actual NeatEvolver API handles the training loop internally.
// You would use callbacks or event hooks to save checkpoints during evolution.

// Resuming from a checkpoint:
using var stream = File.OpenRead("latest-checkpoint.json");
var checkpoint = await serializer.LoadAsync(stream);

Console.WriteLine($"Resuming from generation {checkpoint.Generation}");
Console.WriteLine($"Population size: {checkpoint.Population.Count}");
Console.WriteLine($"Champion fitness: {checkpoint.ChampionFitness}");
Console.WriteLine($"Seed: {checkpoint.Seed}");

// Use the checkpoint data to restore evolution state and continue training
```

### Pattern: Checkpoint Naming Convention

Use a naming convention that makes it easy to find the latest checkpoint:

```csharp
string CheckpointPath(int generation) =>
    $"checkpoints/run-seed{seed}-gen{generation:D6}.json";

// Saves: checkpoints/run-seed42-gen000100.json
//        checkpoints/run-seed42-gen000200.json
//        checkpoints/run-seed42-gen000300.json
```

### Pattern: Keep N Latest Checkpoints

To manage disk space, keep only the N most recent checkpoints:

```csharp
async Task SaveWithRotation(TrainingCheckpoint checkpoint, int maxCheckpoints = 5)
{
    var path = CheckpointPath(checkpoint.Generation);
    using var stream = File.Create(path);
    await serializer.SaveAsync(stream, checkpoint);

    // Clean up old checkpoints
    var checkpointFiles = Directory.GetFiles("checkpoints", "*.json")
        .OrderByDescending(f => f)
        .Skip(maxCheckpoints);

    foreach (var oldFile in checkpointFiles)
    {
        File.Delete(oldFile);
    }
}
```

## Deterministic Resumption

When resuming from a checkpoint with a fixed seed, the training will continue deterministically from the saved point. The checkpoint captures the `RngState` (internal state of the random number generator), so the sequence of random operations continues exactly where it left off.

Requirements for deterministic resumption:
1. Same NeatSharp version that created the checkpoint.
2. Same .NET runtime version (for floating-point consistency).
3. CPU evaluation (GPU may have minor floating-point variance).
4. Unmodified configuration (the checkpoint stores the original configuration).

See the [Reproducibility Guide](reproducibility.md) for more details on deterministic execution.

## Further Reading

- [NEAT Basics](neat-basics.md) -- core NEAT concepts
- [Reproducibility Guide](reproducibility.md) -- deterministic experiments and seeds
- [Parameter Tuning Guide](parameter-tuning.md) -- configuration reference
- [Troubleshooting Guide](troubleshooting.md) -- common issues and resolutions
