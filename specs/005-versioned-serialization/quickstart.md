# Quickstart: Versioned Serialization

**Feature**: 005-versioned-serialization
**Date**: 2026-02-15

## Setup

No additional registration is needed — serialization services are registered automatically by `AddNeatSharp()`.

```csharp
var services = new ServiceCollection();
services.AddNeatSharp(options =>
{
    options.InputCount = 2;
    options.OutputCount = 1;
    options.PopulationSize = 150;
    options.Seed = 42;
    options.Stopping.MaxGenerations = 100;
});
services.AddLogging(); // For migration/corruption warnings

var provider = services.BuildServiceProvider();
var evolver = provider.GetRequiredService<INeatEvolver>();
var serializer = provider.GetRequiredService<ICheckpointSerializer>();
var exporter = provider.GetRequiredService<IChampionExporter>();
var diagnostics = provider.GetRequiredService<IDiagnosticsBundleCreator>();
```

## Save a Checkpoint Mid-Run

Use the `OnCheckpoint` callback in `EvolutionRunOptions` to save checkpoints at generation boundaries.

```csharp
TrainingCheckpoint? latestCheckpoint = null;

var result = await evolver.RunAsync(
    EvaluationStrategy.FromFunction(genome => EvaluateXor(genome)),
    new EvolutionRunOptions
    {
        OnCheckpoint = async (checkpoint, ct) =>
        {
            // Save every 10 generations
            if (checkpoint.Generation % 10 == 0)
            {
                using var stream = File.Create($"checkpoint-gen{checkpoint.Generation}.json");
                await serializer.SaveAsync(stream, checkpoint, ct);
            }
            // Always keep the latest in memory
            latestCheckpoint = checkpoint;
        }
    },
    cancellationToken);
```

## Load and Resume from Checkpoint

```csharp
// Load the checkpoint
TrainingCheckpoint checkpoint;
using (var stream = File.OpenRead("checkpoint-gen50.json"))
{
    checkpoint = await serializer.LoadAsync(stream);
}

// Resume training — results are identical to an uninterrupted run
var result = await evolver.RunAsync(
    EvaluationStrategy.FromFunction(genome => EvaluateXor(genome)),
    new EvolutionRunOptions { ResumeFrom = checkpoint },
    cancellationToken);
```

**Important**: The configuration used for resume must be identical to the one used when the checkpoint was saved. The system verifies this via configuration hash comparison and throws `CheckpointException` on mismatch.

## Export Champion for Interoperability

```csharp
// After a completed run
using var stream = File.Create("champion.json");
await exporter.ExportAsync(stream, result);
```

The resulting JSON is self-describing and parseable by any JSON reader:

```json
{
  "schemaVersion": "1.0.0",
  "metadata": {
    "libraryVersion": "1.0.0.0",
    "fitness": 3.98,
    "generationFound": 42,
    "seed": 12345
  },
  "nodes": [
    { "id": 0, "type": "input", "activationFunction": "identity" },
    { "id": 2, "type": "output", "activationFunction": "sigmoid" }
  ],
  "edges": [
    { "source": 0, "target": 2, "weight": 0.75, "enabled": true }
  ]
}
```

## Create a Diagnostics Bundle

```csharp
// From a checkpoint (captured mid-run or after completion)
using var stream = File.Create("diagnostics-bundle.json");
await diagnostics.CreateAsync(stream, latestCheckpoint);
```

The bundle contains everything needed to reproduce a training run: checkpoint, configuration, environment metadata, and run history — all in a single JSON document.

## Stream-Based I/O (No Filesystem Required)

All operations work with any `Stream` — not just files:

```csharp
// In-memory round-trip
using var memoryStream = new MemoryStream();
await serializer.SaveAsync(memoryStream, checkpoint);

memoryStream.Position = 0;
var restored = await serializer.LoadAsync(memoryStream);
```

## Error Handling

```csharp
try
{
    using var stream = File.OpenRead("old-checkpoint.json");
    var checkpoint = await serializer.LoadAsync(stream);
}
catch (SchemaVersionException ex)
{
    // "Artifact schema version 0.9.0 is not compatible with current version 1.0.0.
    //  No automatic migration is available for this version."
    Console.WriteLine(ex.Message);
    Console.WriteLine($"Artifact version: {ex.ArtifactVersion}");
    Console.WriteLine($"Expected version: {ex.ExpectedVersion}");
}
catch (CheckpointCorruptionException ex)
{
    // "Checkpoint validation failed: Connection (innovation 42) references
    //  non-existent node ID 99; Species 3 references genome not in population."
    Console.WriteLine(ex.Message);
    foreach (var error in ex.ValidationErrors)
    {
        Console.WriteLine($"  - {error}");
    }
}
catch (CheckpointException ex)
{
    // "Configuration hash mismatch: checkpoint was saved with configuration
    //  hash 'a1b2c3...' but current configuration hash is 'd4e5f6...'."
    Console.WriteLine(ex.Message);
}
```

## Atomic File Writes (Recommended Pattern)

Since the serializer writes to streams, use a temporary-then-rename pattern for safe file writes:

```csharp
var targetPath = "checkpoint.json";
var tempPath = targetPath + ".tmp";

using (var stream = File.Create(tempPath))
{
    await serializer.SaveAsync(stream, checkpoint);
}

File.Move(tempPath, targetPath, overwrite: true);
```

This ensures a partially-written file is never mistaken for a valid checkpoint.
