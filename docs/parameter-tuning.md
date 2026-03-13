# Parameter Tuning Guide

This guide covers the key configuration parameters in NeatSharp, recommended starting values for different problem types, and strategies for adjusting parameters during experimentation.

## Configuration Overview

All NeatSharp parameters are configured through `NeatSharpOptions`, which is registered via the `AddNeatSharp()` extension method using the .NET Options pattern.

```csharp
services.AddNeatSharp(options =>
{
    options.InputCount = 2;
    options.OutputCount = 1;
    options.PopulationSize = 150;
    // ... more options below
});
```

## Core Parameters

### Population Size

**Property**: `NeatSharpOptions.PopulationSize`
**Default**: 150
**Range**: 1 to 100,000

Population size determines how many genomes exist in each generation. Larger populations explore more of the search space per generation but take longer to evaluate.

**Guidelines**:
- Start with 150 for simple problems (XOR, basic classification).
- Use 300-500 for moderate problems (function approximation, simple control).
- Use 1,000+ for complex problems requiring significant structural diversity.
- When using GPU evaluation, larger populations (500+) amortize GPU overhead better.

### Seed

**Property**: `NeatSharpOptions.Seed`
**Default**: `null` (auto-generated)

Setting a fixed seed guarantees deterministic, reproducible runs on CPU. See the [Reproducibility Guide](reproducibility.md) for details.

```csharp
options.Seed = 42; // Fixed seed for reproducible experiments
```

## Stopping Criteria

**Property**: `NeatSharpOptions.Stopping` (`StoppingCriteria`)

At least one stopping criterion must be configured. Multiple criteria can be combined -- evolution stops when any one is satisfied.

| Property              | Type     | Description                                           |
|-----------------------|----------|-------------------------------------------------------|
| `MaxGenerations`      | `int?`   | Maximum number of generations to run.                 |
| `FitnessTarget`       | `double?`| Stop when any genome reaches this fitness.            |
| `StagnationThreshold` | `int?`   | Stop after this many generations with no improvement. |

```csharp
options.Stopping.MaxGenerations = 300;
options.Stopping.FitnessTarget = 0.99;
options.Stopping.StagnationThreshold = 50;
```

## Mutation Rates

**Property**: `NeatSharpOptions.Mutation` (`MutationOptions`)

Mutation rates control how genomes change between generations. All rates are probabilities in the range [0.0, 1.0].

| Property                  | Default | Description                                              |
|---------------------------|---------|----------------------------------------------------------|
| `WeightPerturbationRate`  | 0.8     | Probability of small random weight adjustment.           |
| `WeightReplacementRate`   | 0.1     | Probability of complete weight replacement.              |
| `AddConnectionRate`       | 0.05    | Probability of adding a new connection.                  |
| `AddNodeRate`             | 0.03    | Probability of splitting a connection with a new node.   |
| `ToggleEnableRate`        | 0.01    | Probability of toggling a connection's enabled state.    |
| `PerturbationPower`       | 0.5     | Maximum delta (uniform) or std dev (Gaussian) for perturbation. |
| `WeightMinValue`          | -4.0    | Minimum allowed weight value.                            |
| `WeightMaxValue`          | 4.0     | Maximum allowed weight value.                            |

**Note**: `WeightPerturbationRate` + `WeightReplacementRate` must not exceed 1.0 since they are mutually exclusive.

```csharp
options.Mutation.WeightPerturbationRate = 0.8;
options.Mutation.WeightReplacementRate = 0.1;
options.Mutation.AddConnectionRate = 0.05;
options.Mutation.AddNodeRate = 0.03;
options.Mutation.PerturbationPower = 0.5;
```

## Speciation Parameters

**Property**: `NeatSharpOptions.Speciation` (`SpeciationOptions`)

Speciation controls how genomes are grouped into species, which protects structural innovations from premature elimination.

| Property                     | Default | Description                                           |
|------------------------------|---------|-------------------------------------------------------|
| `ExcessCoefficient`          | 1.0     | Weight for excess genes (c1) in distance formula.     |
| `DisjointCoefficient`        | 1.0     | Weight for disjoint genes (c2) in distance formula.   |
| `WeightDifferenceCoefficient`| 0.4     | Weight for average weight difference (c3).            |
| `CompatibilityThreshold`     | 3.0     | Maximum distance for same-species assignment.         |

**Compatibility distance formula**: `d = (c1 * E / N) + (c2 * D / N) + (c3 * W_avg)`

**Adjusting the threshold**:
- **Lower threshold** = more species, more protection for innovations, slower convergence
- **Higher threshold** = fewer species, less protection, faster convergence (risk of premature convergence)

```csharp
options.Speciation.CompatibilityThreshold = 3.0;
options.Speciation.ExcessCoefficient = 1.0;
options.Speciation.DisjointCoefficient = 1.0;
options.Speciation.WeightDifferenceCoefficient = 0.4;
```

## Selection Parameters

**Property**: `NeatSharpOptions.Selection` (`SelectionOptions`)

| Property              | Default | Description                                               |
|-----------------------|---------|-----------------------------------------------------------|
| `TournamentSize`      | 2       | Number of candidates compared in tournament selection.    |
| `ElitismThreshold`    | 5       | Minimum species size for champion preservation.           |
| `SurvivalThreshold`   | 0.2     | Fraction of species members eligible for reproduction.    |
| `StagnationThreshold` | 15      | Generations without improvement before species is stagnant.|

```csharp
options.Selection.TournamentSize = 2;
options.Selection.ElitismThreshold = 5;
options.Selection.SurvivalThreshold = 0.2;
options.Selection.StagnationThreshold = 15;
```

## Complexity Penalty

**Property**: `NeatSharpOptions.ComplexityPenalty` (`ComplexityPenaltyOptions`)

An optional soft penalty that reduces effective fitness based on genome structural size. This encourages simpler solutions when complexity grows too large.

| Property      | Default                      | Description                                   |
|---------------|------------------------------|-----------------------------------------------|
| `Coefficient` | 0.0 (disabled)               | Multiplier for complexity subtracted from fitness. |
| `Metric`      | `ComplexityPenaltyMetric.Both`| Which metric to use (nodes, connections, or both). |

```csharp
options.ComplexityPenalty.Coefficient = 0.001;
options.ComplexityPenalty.Metric = ComplexityPenaltyMetric.Both;
```

## Evaluation Options

**Property**: `NeatSharpOptions.Evaluation` (`EvaluationOptions`)

Controls how fitness evaluation is performed, including parallel execution and error handling.

| Property                  | Type                   | Default          | Description                                              |
|---------------------------|------------------------|------------------|----------------------------------------------------------|
| `MaxDegreeOfParallelism`  | `int?`                 | `null` (all cores) | Max concurrent evaluations. `1` = sequential, `null` = all cores. |
| `ErrorMode`               | `EvaluationErrorMode`  | `AssignFitness`  | How to handle evaluation exceptions.                     |
| `ErrorFitnessValue`       | `double`               | `0.0`            | Fitness assigned to failed genomes (when `ErrorMode = AssignFitness`). |

```csharp
options.Evaluation.MaxDegreeOfParallelism = null; // Use all CPU cores
options.Evaluation.ErrorMode = EvaluationErrorMode.AssignFitness;
options.Evaluation.ErrorFitnessValue = 0.0;
```

**Guidelines**:
- For CPU-bound fitness functions with populations of 100+, parallel evaluation (the default) provides significant speedup.
- For very lightweight fitness functions (microsecond-scale), set `MaxDegreeOfParallelism = 1` to avoid thread scheduling overhead.
- Your fitness function must be thread-safe when using parallel evaluation. See the [Parallel Evaluation Guide](parallel-evaluation.md) for details.

## Recommended Starting Values

### Classification (XOR-like problems)

Small input/output spaces, binary or categorical outputs.

```csharp
services.AddNeatSharp(options =>
{
    options.InputCount = 2;  // Adjust to your problem
    options.OutputCount = 1; // Adjust to your problem
    options.PopulationSize = 150;
    options.Seed = 42;

    options.Stopping.FitnessTarget = 0.98;
    options.Stopping.MaxGenerations = 300;

    options.Mutation.WeightPerturbationRate = 0.8;
    options.Mutation.WeightReplacementRate = 0.1;
    options.Mutation.AddConnectionRate = 0.05;
    options.Mutation.AddNodeRate = 0.03;
    options.Mutation.PerturbationPower = 0.5;

    options.Speciation.CompatibilityThreshold = 3.0;

    options.Selection.TournamentSize = 2;
    options.Selection.ElitismThreshold = 5;
    options.Selection.SurvivalThreshold = 0.2;
});
```

### Function Approximation (sine-like problems)

Continuous output, requires precise weight tuning.

```csharp
services.AddNeatSharp(options =>
{
    options.InputCount = 1;
    options.OutputCount = 1;
    options.PopulationSize = 200;  // Slightly larger for continuous search
    options.Seed = 42;

    options.Stopping.FitnessTarget = 0.95;
    options.Stopping.MaxGenerations = 500;

    // Higher weight perturbation for fine-grained weight tuning
    options.Mutation.WeightPerturbationRate = 0.85;
    options.Mutation.WeightReplacementRate = 0.05;
    options.Mutation.PerturbationPower = 0.3; // Smaller steps for precision
    options.Mutation.AddConnectionRate = 0.05;
    options.Mutation.AddNodeRate = 0.03;

    options.Speciation.CompatibilityThreshold = 4.0;

    options.Selection.SurvivalThreshold = 0.3; // Broader parent pool
});
```

### Control Tasks (Cart-Pole-like problems)

Sequential decision-making, requires network structure and sustained performance.

```csharp
services.AddNeatSharp(options =>
{
    options.InputCount = 4;   // e.g., x, x_dot, theta, theta_dot
    options.OutputCount = 1;  // e.g., force direction
    options.PopulationSize = 150;
    options.Seed = 42;

    options.Stopping.FitnessTarget = 0.99;
    options.Stopping.MaxGenerations = 500;
    options.Stopping.StagnationThreshold = 50;

    options.Mutation.WeightPerturbationRate = 0.8;
    options.Mutation.WeightReplacementRate = 0.1;
    options.Mutation.AddConnectionRate = 0.05;
    options.Mutation.AddNodeRate = 0.03;

    options.Speciation.CompatibilityThreshold = 3.0;

    options.Selection.StagnationThreshold = 20;

    // Optional: penalize overly complex solutions
    options.ComplexityPenalty.Coefficient = 0.0005;
});
```

## Adjustment Strategies

### Problem: No Fitness Improvement

If fitness plateaus early and no genomes are improving:

1. **Increase structural mutation rates**: Raise `AddNodeRate` to 0.05-0.08 and `AddConnectionRate` to 0.08-0.15 to explore more network structures.
2. **Increase population size**: Double the population to explore more of the search space.
3. **Check your fitness function**: Ensure it provides a smooth gradient rather than a binary pass/fail. Partial credit helps evolution find intermediate solutions.
4. **Increase stagnation threshold**: Allow species more time before being pruned (e.g., 25-30 generations).

### Problem: Premature Convergence

If the population collapses into one or two species and stops improving:

1. **Lower `CompatibilityThreshold`**: Try 1.5-2.0 to create more species and protect innovation.
2. **Increase speciation coefficients**: Raise `ExcessCoefficient` and `DisjointCoefficient` to 1.5-2.0 to make structural differences matter more.
3. **Lower `SurvivalThreshold`**: Try 0.1 to restrict parent selection to the very best performers within each species.

### Problem: Overly Complex Networks

If networks grow very large but fitness does not improve proportionally:

1. **Enable complexity penalty**: Set `ComplexityPenalty.Coefficient` to 0.001-0.01.
2. **Lower structural mutation rates**: Reduce `AddNodeRate` to 0.01-0.02 and `AddConnectionRate` to 0.02-0.03.
3. **Set complexity limits**: Use `options.Complexity.MaxNodes` and `options.Complexity.MaxConnections` to cap growth.

### Problem: Too Many Species

If the species count grows very large with many containing only 1-2 genomes:

1. **Raise `CompatibilityThreshold`**: Try 4.0-6.0 to merge similar species.
2. **Lower speciation coefficients**: Reduce `WeightDifferenceCoefficient` to 0.2-0.3 so weight differences matter less.

## Iterative Tuning Workflow

1. **Start with defaults**: Use the recommended values for your problem type above.
2. **Run a baseline**: Execute a full run and note final fitness, generation count, species count, and network complexity.
3. **Identify the bottleneck**: Is fitness stalling? Are networks too complex? Is there too little diversity?
4. **Adjust one parameter at a time**: Change one parameter, re-run, and compare results.
5. **Use fixed seeds**: Set `options.Seed` to compare runs fairly. Change only the parameter under test.
6. **Use checkpoints**: Save checkpoints every N generations to analyze intermediate states. See the [Checkpointing Guide](checkpointing.md).

## Further Reading

- [NEAT Basics](neat-basics.md) -- how NEAT works and NeatSharp type mappings
- [Parallel Evaluation](parallel-evaluation.md) -- multi-core CPU evaluation and thread-safety
- [Reproducibility Guide](reproducibility.md) -- deterministic experiments with seeds
- [Checkpointing Guide](checkpointing.md) -- save/resume for long runs
