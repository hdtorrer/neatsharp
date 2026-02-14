# Quickstart: Genome Model + Innovation Tracking + Feed-Forward Phenotype

**Goal**: Construct a genome, build a feed-forward phenotype, and run inference.

## 1. Build a Simple Genome and Run Inference

```csharp
using Microsoft.Extensions.DependencyInjection;
using NeatSharp.Extensions;
using NeatSharp.Genetics;

// Set up DI
var services = new ServiceCollection();
services.AddNeatSharp();
var provider = services.BuildServiceProvider();

// Define node genes: 2 inputs + 1 bias + 1 output
var nodes = new NodeGene[]
{
    new(Id: 0, Type: NodeType.Input),
    new(Id: 1, Type: NodeType.Input),
    new(Id: 2, Type: NodeType.Bias),
    new(Id: 3, Type: NodeType.Output),
};

// Define connection genes with known weights
var connections = new ConnectionGene[]
{
    new(InnovationNumber: 1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true),
    new(InnovationNumber: 2, SourceNodeId: 1, TargetNodeId: 3, Weight: 0.8, IsEnabled: true),
    new(InnovationNumber: 3, SourceNodeId: 2, TargetNodeId: 3, Weight: -0.3, IsEnabled: true),
};

// Create the immutable genome
var genome = new Genome(nodes, connections);

// Build the feed-forward phenotype
var builder = provider.GetRequiredService<INetworkBuilder>();
var network = builder.Build(genome);

// Run inference
ReadOnlySpan<double> inputs = [1.0, 0.5];
Span<double> outputs = stackalloc double[1];
network.Activate(inputs, outputs);

// Output: sigmoid(1.0*0.5 + 0.5*0.8 + 1.0*(-0.3)) = sigmoid(0.6) ≈ 0.6457
Console.WriteLine($"Output: {outputs[0]:F4}");
```

## 2. Genome with Hidden Layers and Custom Activation

```csharp
var nodes = new NodeGene[]
{
    new(0, NodeType.Input),
    new(1, NodeType.Input),
    new(2, NodeType.Bias),
    new(3, NodeType.Hidden, ActivationFunctions.Tanh),  // Hidden node with tanh
    new(4, NodeType.Output),                             // Output uses default sigmoid
};

var connections = new ConnectionGene[]
{
    new(1, SourceNodeId: 0, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
    new(2, SourceNodeId: 1, TargetNodeId: 3, Weight: -1.0, IsEnabled: true),
    new(3, SourceNodeId: 2, TargetNodeId: 3, Weight: 0.0, IsEnabled: true),
    new(4, SourceNodeId: 3, TargetNodeId: 4, Weight: 1.0, IsEnabled: true),
};

var genome = new Genome(nodes, connections);
var network = builder.Build(genome);

Span<double> outputs = stackalloc double[1];
network.Activate([1.0, 0.0], outputs);

// Hidden node: tanh(1.0*1.0 + 0.0*(-1.0) + 1.0*0.0) = tanh(1.0) ≈ 0.7616
// Output node: sigmoid(0.7616*1.0) ≈ 0.6819
Console.WriteLine($"Output: {outputs[0]:F4}");
```

## 3. Disabled Connections

```csharp
var connections = new ConnectionGene[]
{
    new(1, SourceNodeId: 0, TargetNodeId: 3, Weight: 0.5, IsEnabled: true),
    new(2, SourceNodeId: 1, TargetNodeId: 3, Weight: 0.8, IsEnabled: false),  // Disabled!
    new(3, SourceNodeId: 2, TargetNodeId: 3, Weight: -0.3, IsEnabled: true),
};

var genome = new Genome(nodes, connections);
var network = builder.Build(genome);

network.Activate([1.0, 0.5], outputs);
// Only connections 1 and 3 contribute; connection 2 is ignored
// sigmoid(1.0*0.5 + 1.0*(-0.3)) = sigmoid(0.2) ≈ 0.5498
```

## 4. Innovation Tracking

```csharp
var tracker = provider.GetRequiredService<IInnovationTracker>();

// Same structural change in the same generation → same innovation ID
int id1 = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);
int id2 = tracker.GetConnectionInnovation(sourceNodeId: 0, targetNodeId: 5);
// id1 == id2 (deterministic deduplication)

// Different structural change → different innovation ID
int id3 = tracker.GetConnectionInnovation(sourceNodeId: 1, targetNodeId: 5);
// id3 != id1

// Node split: splitting a connection produces deterministic IDs
var split = tracker.GetNodeSplitInnovation(connectionInnovation: 1);
// split.NewNodeId — ID for the new hidden node
// split.IncomingConnectionInnovation — innovation for source→newNode
// split.OutgoingConnectionInnovation — innovation for newNode→target

// Advance generation (clears dedup cache, preserves counters)
tracker.NextGeneration();
```

## 5. Custom Activation Functions

```csharp
var registry = provider.GetRequiredService<IActivationFunctionRegistry>();

// Register a custom activation function before evolution
registry.Register("leaky_relu", x => x > 0.0 ? x : 0.01 * x);

// Use it in a node gene
var hiddenNode = new NodeGene(Id: 5, Type: NodeType.Hidden, ActivationFunction: "leaky_relu");
```

## 6. Error Handling

```csharp
using NeatSharp.Exceptions;

// Cycle detection
try
{
    var cyclicNodes = new NodeGene[]
    {
        new(0, NodeType.Input),
        new(3, NodeType.Hidden),
        new(4, NodeType.Hidden),
        new(5, NodeType.Output),
    };
    var cyclicConnections = new ConnectionGene[]
    {
        new(1, SourceNodeId: 0, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),
        new(2, SourceNodeId: 3, TargetNodeId: 4, Weight: 1.0, IsEnabled: true),
        new(3, SourceNodeId: 4, TargetNodeId: 3, Weight: 1.0, IsEnabled: true),  // Cycle!
        new(4, SourceNodeId: 4, TargetNodeId: 5, Weight: 1.0, IsEnabled: true),
    };
    var cyclicGenome = new Genome(cyclicNodes, cyclicConnections);
    builder.Build(cyclicGenome);
}
catch (CycleDetectedException ex)
{
    Console.WriteLine(ex.Message);
}

// Input dimension mismatch
try
{
    network.Activate([1.0], outputs);  // Expected 2 inputs, got 1
}
catch (InputDimensionMismatchException ex)
{
    Console.WriteLine($"Expected {ex.Expected} inputs, got {ex.Actual}");
}

// Invalid genome construction
try
{
    var badConnection = new ConnectionGene(1, SourceNodeId: 0, TargetNodeId: 99, Weight: 1.0, IsEnabled: true);
    new Genome(nodes, [badConnection]);  // Node 99 doesn't exist
}
catch (InvalidGenomeException ex)
{
    Console.WriteLine(ex.Message);
}
```

## 7. Champion Inference (Post-Evolution)

```csharp
// After evolution completes (future spec), the champion is an IGenome
IGenome champion = result.Champion.Genome;

// Activate repeatedly with different inputs — deterministic results
Span<double> outputs = stackalloc double[1];

champion.Activate([0.0, 0.0], outputs);
Console.WriteLine($"[0,0] → {outputs[0]:F4}");

champion.Activate([1.0, 1.0], outputs);
Console.WriteLine($"[1,1] → {outputs[0]:F4}");

// Same inputs always produce same outputs (FR-014)
champion.Activate([0.0, 0.0], outputs);
// outputs[0] is identical to the first activation
```
