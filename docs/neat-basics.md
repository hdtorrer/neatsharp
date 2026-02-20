# NEAT Basics

This guide introduces the core concepts of **NEAT (NeuroEvolution of Augmenting Topologies)** and maps each concept to its NeatSharp implementation type.

## What is NEAT?

NEAT is a genetic algorithm that evolves the structure and weights of neural networks simultaneously. Unlike traditional neuroevolution approaches that fix the topology and only evolve weights, NEAT starts with minimal networks and grows them over time through structural mutations -- a process called **complexification**.

The original NEAT algorithm was introduced by Kenneth O. Stanley and Risto Miikkulainen in 2002. NeatSharp is a faithful implementation of NEAT for .NET, with GPU acceleration and hybrid CPU+GPU evaluation support.

## Genomes

A **genome** in NEAT encodes a neural network as two ordered lists: **node genes** and **connection genes**.

### Node Genes

Each node gene represents a neuron in the network. Every node has a unique ID, a type, and an activation function.

**NeatSharp type**: `NodeGene` (in `NeatSharp.Genetics`)

```csharp
// NodeGene is an immutable record:
// public sealed record NodeGene(int Id, NodeType Type, string ActivationFunction = "sigmoid");
```

Node types (`NodeType` enum in `NeatSharp.Genetics`):

| Type     | Description                                                        |
|----------|--------------------------------------------------------------------|
| `Input`  | Receives external input values during network activation.          |
| `Hidden` | Intermediate processing node, created by structural mutations.     |
| `Output` | Produces network output values after activation.                   |
| `Bias`   | Always outputs 1.0, providing a learnable offset when connected.   |

### Connection Genes

Each connection gene represents a directed, weighted edge between two nodes. Connections can be enabled or disabled.

**NeatSharp type**: `ConnectionGene` (in `NeatSharp.Genetics`)

```csharp
// ConnectionGene is an immutable record:
// public sealed record ConnectionGene(
//     int InnovationNumber,
//     int SourceNodeId,
//     int TargetNodeId,
//     double Weight,
//     bool IsEnabled);
```

Key properties:

- **InnovationNumber**: A globally unique identifier tracking the structural origin of this connection. Used for genome alignment during crossover and speciation.
- **SourceNodeId / TargetNodeId**: The directed edge from source to target node.
- **Weight**: The connection strength (numeric weight applied during signal propagation).
- **IsEnabled**: Whether this connection participates in phenotype evaluation. Disabled connections are excluded from signal propagation but remain in the genome for crossover alignment.

### Genome to Network

NeatSharp converts a `Genome` to an executable feed-forward network using `FeedForwardNetworkBuilder` (implements `INetworkBuilder`). The builder performs topological sorting to determine evaluation order and produces an `INetwork` that can be activated with input values.

```csharp
// The network builder is registered automatically via DI:
var network = networkBuilder.Build(genome);
double[] outputs = network.Activate(new double[] { 1.0, 0.0 });
```

## Innovation Numbers

**Innovation numbers** are the mechanism NEAT uses to align genomes during crossover without expensive topological analysis. Every new structural mutation (adding a connection or splitting a connection with a node) receives a unique innovation number from a global counter.

**NeatSharp type**: `InnovationTracker` (implements `IInnovationTracker`, in `NeatSharp.Genetics`)

The `InnovationTracker` ensures that:

1. Each new structural change gets a unique, monotonically increasing innovation ID.
2. The **same structural change within a generation** receives the **same ID** -- this is critical for correct crossover alignment when independent genomes discover the same structural innovation.
3. At each generation boundary, the deduplication caches are cleared (via `NextGeneration()`), but the counters continue incrementing.

```csharp
// Getting a connection innovation number:
int innovation = tracker.GetConnectionInnovation(sourceNodeId: 2, targetNodeId: 5);

// Splitting a connection creates a new node and two new connections:
NodeSplitResult split = tracker.GetNodeSplitInnovation(connectionInnovation: 7);
// split.NewNodeId, split.IncomingConnectionInnovation, split.OutgoingConnectionInnovation
```

## Species and Speciation

NEAT groups genomes into **species** based on structural and parametric similarity. This protects novel topological innovations from being eliminated before they have time to optimize their weights -- a concept called **speciation**.

### Compatibility Distance

The compatibility distance measures how different two genomes are. It is computed using the formula:

```
d = (c1 * E / N) + (c2 * D / N) + (c3 * W_avg)
```

Where:
- **E** = number of excess genes (connections in one genome beyond the range of the other)
- **D** = number of disjoint genes (non-matching connections within the shared range)
- **W_avg** = average weight difference of matching connection genes
- **N** = max(connection count of larger genome, 1) -- normalizes for genome size
- **c1**, **c2**, **c3** = configurable coefficients

**NeatSharp type**: `CompatibilityDistance` (implements `ICompatibilityDistance`, in `NeatSharp.Evolution.Speciation`)

The coefficients are configured via `SpeciationOptions`:

```csharp
services.AddNeatSharp(options =>
{
    options.Speciation.ExcessCoefficient = 1.0;       // c1
    options.Speciation.DisjointCoefficient = 1.0;     // c2
    options.Speciation.WeightDifferenceCoefficient = 0.4; // c3
    options.Speciation.CompatibilityThreshold = 3.0;
});
```

### Species Assignment

Each generation, genomes are assigned to species by comparing them against species representatives. If the compatibility distance between a genome and a representative is below the `CompatibilityThreshold`, the genome joins that species. Otherwise, a new species is created.

**NeatSharp type**: `CompatibilitySpeciation` (implements `ISpeciationStrategy`, in `NeatSharp.Evolution.Speciation`)

## Complexification

NEAT starts with **minimal networks** (direct input-to-output connections) and grows them through structural mutations. This is the key differentiator from fixed-topology approaches.

### Add Node Mutation

Splits an existing enabled connection into two connections with a new hidden node in between. The original connection is disabled.

**NeatSharp type**: `AddNodeMutation` (in `NeatSharp.Evolution.Mutation`)

```
Before: A --[w]--> B        (original connection disabled)
After:  A --[1.0]--> C --[w]--> B   (two new connections through new hidden node C)
```

The incoming connection gets weight 1.0 and the outgoing connection inherits the original weight. This preserves the behavior of the original network as closely as possible.

### Add Connection Mutation

Adds a new connection between two previously unconnected nodes, with a random initial weight.

**NeatSharp type**: `AddConnectionMutation` (in `NeatSharp.Evolution.Mutation`)

### Weight Mutations

In addition to structural mutations, NEAT evolves connection weights through:

- **Weight perturbation**: Small random changes to existing weights (NeatSharp type: `WeightPerturbationMutation`)
- **Weight replacement**: Complete replacement of a weight with a new random value (NeatSharp type: `WeightReplacementMutation`)

Both are in the `NeatSharp.Evolution.Mutation` namespace.

## The Training Loop

The NEAT training loop follows a standard evolutionary cycle: evaluate, speciate, select, reproduce, and mutate.

**NeatSharp type**: `NeatEvolver` (implements `INeatEvolver`, in `NeatSharp.Evolution`)

### Cycle Overview

```
1. Initialize population (minimal genomes)
       |
       v
2. Evaluate fitness (user-provided fitness function)
       |
       v
3. Speciate (assign genomes to species via compatibility distance)
       |
       v
4. Select parents (tournament selection within species)
       |
       v
5. Reproduce (crossover + mutation to create next generation)
       |
       v
6. Check stopping criteria (max generations, fitness target, stagnation)
       |
       v
   If not stopped, go to step 2
```

### Running an Evolution

```csharp
using Microsoft.Extensions.DependencyInjection;
using NeatSharp.Configuration;
using NeatSharp.Extensions;

var services = new ServiceCollection();
services.AddNeatSharp(options =>
{
    options.InputCount = 2;
    options.OutputCount = 1;
    options.PopulationSize = 150;
    options.Seed = 42;
    options.Stopping.FitnessTarget = 0.99;
    options.Stopping.MaxGenerations = 300;
});

// Register your fitness function
services.AddSingleton<IFitnessFunction, MyFitnessFunction>();

var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var evolver = scope.ServiceProvider.GetRequiredService<INeatEvolver>();
var result = await evolver.EvolveAsync(CancellationToken.None);

Console.WriteLine($"Champion fitness: {result.ChampionFitness}");
Console.WriteLine($"Generations: {result.Generation}");
Console.WriteLine($"Seed: {result.Seed}");
```

### Key Components in the Loop

| Step           | NeatSharp Type                | Description                                    |
|----------------|-------------------------------|------------------------------------------------|
| Initialization | `PopulationFactory`           | Creates the initial population of minimal genomes |
| Evaluation     | `IFitnessFunction`           | User-provided fitness scoring (you implement this) |
| Speciation     | `CompatibilitySpeciation`     | Groups genomes into species                    |
| Selection      | `TournamentSelector`          | Picks parents via tournament selection          |
| Crossover      | `NeatCrossover`               | Combines two parent genomes                    |
| Mutation       | `CompositeMutationOperator`   | Applies structural and weight mutations         |
| Stopping       | `StoppingCriteria`            | Checks termination conditions                  |

## Further Reading

- [Parameter Tuning Guide](parameter-tuning.md) -- recommended parameter values for different problem types
- [Reproducibility Guide](reproducibility.md) -- how to reproduce experiments deterministically
- [Checkpointing Guide](checkpointing.md) -- saving and resuming training runs
- [GPU Setup Guide](gpu-setup.md) -- accelerating evaluation with CUDA
