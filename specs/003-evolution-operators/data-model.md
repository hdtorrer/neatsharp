# Data Model: Evolution Operators (Mutation/Crossover) + Speciation + Selection

**Feature Branch**: `003-evolution-operators`
**Date**: 2026-02-13

## Existing Entities (unchanged)

### Genome (`NeatSharp.Genetics`)

```csharp
public sealed class Genome
{
    public IReadOnlyList<NodeGene> Nodes { get; }
    public IReadOnlyList<ConnectionGene> Connections { get; }
    public int InputCount { get; }
    public int OutputCount { get; }
}
```

### NodeGene (`NeatSharp.Genetics`)

```csharp
public sealed record NodeGene(int Id, NodeType Type, string ActivationFunction = "sigmoid");
```

### ConnectionGene (`NeatSharp.Genetics`)

```csharp
public sealed record ConnectionGene(int InnovationNumber, int SourceNodeId, int TargetNodeId, double Weight, bool IsEnabled);
```

### NodeType (`NeatSharp.Genetics`)

```csharp
public enum NodeType { Input, Hidden, Output, Bias }
```

### InnovationTracker / IInnovationTracker (`NeatSharp.Genetics`)

```csharp
public interface IInnovationTracker
{
    int GetConnectionInnovation(int sourceNodeId, int targetNodeId);
    NodeSplitResult GetNodeSplitInnovation(int connectionInnovation);
    void NextGeneration();
}
```

---

## New Entities

### Configuration Types (`NeatSharp.Configuration`)

#### WeightDistributionType

```csharp
public enum WeightDistributionType
{
    Uniform,
    Gaussian
}
```

- Selects the distribution used for weight perturbation.
- `Uniform`: weight += uniform(-power, +power)
- `Gaussian`: weight += gaussian(0, power)

#### ComplexityPenaltyMetric

```csharp
public enum ComplexityPenaltyMetric
{
    NodeCount,
    ConnectionCount,
    Both
}
```

#### MutationOptions

```csharp
public class MutationOptions
{
    public double WeightPerturbationRate { get; set; } = 0.8;
    public double WeightReplacementRate { get; set; } = 0.1;
    public double AddConnectionRate { get; set; } = 0.05;
    public double AddNodeRate { get; set; } = 0.03;
    public double ToggleEnableRate { get; set; } = 0.01;
    public double PerturbationPower { get; set; } = 0.5;
    public WeightDistributionType PerturbationDistribution { get; set; } = WeightDistributionType.Uniform;
    public double WeightMinValue { get; set; } = -4.0;
    public double WeightMaxValue { get; set; } = 4.0;
    public int MaxAddConnectionAttempts { get; set; } = 20;
}
```

- All rates are probabilities in [0.0, 1.0].
- `PerturbationPower`: max absolute delta for uniform, std dev for Gaussian.
- `WeightMinValue`/`WeightMaxValue`: range for weight replacement and clamping after perturbation.
- `MaxAddConnectionAttempts`: max random node-pair attempts before skipping add-connection.

#### CrossoverOptions

```csharp
public class CrossoverOptions
{
    public double CrossoverRate { get; set; } = 0.75;
    public double InterspeciesCrossoverRate { get; set; } = 0.001;
    public double DisabledGeneInheritanceProbability { get; set; } = 0.75;
}
```

- `CrossoverRate`: fraction of offspring produced via crossover (rest are cloned + mutated).
- `InterspeciesCrossoverRate`: probability that second parent comes from a different species.
- `DisabledGeneInheritanceProbability`: when a matching gene is disabled in either parent, probability it's disabled in offspring.

#### SpeciationOptions

```csharp
public class SpeciationOptions
{
    public double ExcessCoefficient { get; set; } = 1.0;
    public double DisjointCoefficient { get; set; } = 1.0;
    public double WeightDifferenceCoefficient { get; set; } = 0.4;
    public double CompatibilityThreshold { get; set; } = 3.0;
}
```

- c1, c2, c3 in the compatibility distance formula.
- `CompatibilityThreshold`: maximum distance for same-species assignment.

#### SelectionOptions

```csharp
public class SelectionOptions
{
    public int ElitismThreshold { get; set; } = 5;
    public int StagnationThreshold { get; set; } = 15;
    public double SurvivalThreshold { get; set; } = 0.2;
    public int TournamentSize { get; set; } = 2;
}
```

- `ElitismThreshold`: minimum species size for champion preservation.
- `StagnationThreshold`: generations without improvement before penalty.
- `SurvivalThreshold`: fraction of species eligible for reproduction.
- `TournamentSize`: default tournament selector parameter.

#### ComplexityPenaltyOptions

```csharp
public class ComplexityPenaltyOptions
{
    public double Coefficient { get; set; } = 0.0;
    public ComplexityPenaltyMetric Metric { get; set; } = ComplexityPenaltyMetric.Both;
}
```

- `Coefficient = 0.0` means penalty is disabled by default.

#### NeatSharpOptions (extended)

```csharp
public class NeatSharpOptions
{
    // Existing properties unchanged
    public int PopulationSize { get; set; } = 150;
    public int? Seed { get; set; }
    public StoppingCriteria Stopping { get; set; } = new();
    public ComplexityLimits Complexity { get; set; } = new();
    public bool EnableMetrics { get; set; } = true;

    // New properties for Spec 003
    public MutationOptions Mutation { get; set; } = new();
    public CrossoverOptions Crossover { get; set; } = new();
    public SpeciationOptions Speciation { get; set; } = new();
    public SelectionOptions Selection { get; set; } = new();
    public ComplexityPenaltyOptions ComplexityPenalty { get; set; } = new();
}
```

---

### Evolution Domain Types

#### Species (`NeatSharp.Evolution.Speciation`)

```csharp
public sealed class Species
{
    public int Id { get; }
    public Genome Representative { get; private set; }
    public List<(Genome Genome, double Fitness)> Members { get; }
    public double BestFitnessEver { get; private set; }
    public int GenerationsSinceImprovement { get; private set; }
}
```

- Mutable within a generation cycle (members added/cleared, representative updated).
- `Id`: stable across generations.
- `Representative`: updated each generation (default: best-performing member).
- `BestFitnessEver`: peak fitness across all generations.
- `GenerationsSinceImprovement`: incremented when current best doesn't exceed `BestFitnessEver`; reset to 0 on improvement.

**State transitions**:
1. Generation start: clear members, keep representative and metadata.
2. Assignment: genomes added to members list.
3. Post-assignment: update representative to best member, update stagnation counters.
4. Empty species (no members assigned): removed from active species list.

---

## Entity Relationships

```text
NeatSharpOptions
├── MutationOptions
├── CrossoverOptions
├── SpeciationOptions
├── SelectionOptions
├── ComplexityPenaltyOptions
├── ComplexityLimits          (existing)
└── StoppingCriteria          (existing)

IMutationOperator ──uses──> Genome, Random, IInnovationTracker
  ├── WeightPerturbationMutation ──reads──> MutationOptions
  ├── WeightReplacementMutation  ──reads──> MutationOptions
  ├── AddConnectionMutation      ──reads──> MutationOptions, ComplexityLimits
  ├── AddNodeMutation            ──reads──> MutationOptions, ComplexityLimits
  └── ToggleEnableMutation

ICrossoverOperator ──uses──> Genome, Random
  └── NeatCrossover ──reads──> CrossoverOptions

ICompatibilityDistance ──uses──> Genome
  └── CompatibilityDistance ──reads──> SpeciationOptions

ISpeciationStrategy ──uses──> Species, Genome
  └── CompatibilitySpeciation ──reads──> SpeciationOptions
      └── uses ──> ICompatibilityDistance

IParentSelector ──uses──> (Genome, Fitness)[], Random
  ├── TournamentSelector     ──reads──> SelectionOptions
  ├── RouletteWheelSelector
  └── StochasticUniversalSamplingSelector

ReproductionAllocator ──uses──> Species[], SelectionOptions
  └── produces offspring counts per species

CompositeMutationOperator ──uses──> IMutationOperator[], MutationOptions, Random
  └── orchestrates mutation pipeline
```

---

## Validation Rules

### MutationOptions
- All rates: [0.0, 1.0]
- `PerturbationPower`: > 0.0
- `WeightMinValue` < `WeightMaxValue`
- `MaxAddConnectionAttempts`: >= 1

### CrossoverOptions
- `CrossoverRate`: [0.0, 1.0]
- `InterspeciesCrossoverRate`: [0.0, 1.0]
- `DisabledGeneInheritanceProbability`: [0.0, 1.0]

### SpeciationOptions
- All coefficients: >= 0.0
- `CompatibilityThreshold`: > 0.0

### SelectionOptions
- `ElitismThreshold`: >= 1
- `StagnationThreshold`: >= 1
- `SurvivalThreshold`: (0.0, 1.0]
- `TournamentSize`: >= 1
