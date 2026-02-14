# Contract: Crossover Operator

**Feature Branch**: `003-evolution-operators`
**Namespace**: `NeatSharp.Evolution.Crossover`

## Interfaces

### ICrossoverOperator

```csharp
/// <summary>
/// Combines two parent genomes into a single offspring genome using
/// innovation-number-aligned gene inheritance.
/// </summary>
public interface ICrossoverOperator
{
    /// <summary>
    /// Performs crossover between two parent genomes.
    /// </summary>
    /// <param name="parent1">First parent genome.</param>
    /// <param name="parent1Fitness">Fitness score of the first parent.</param>
    /// <param name="parent2">Second parent genome.</param>
    /// <param name="parent2Fitness">Fitness score of the second parent.</param>
    /// <param name="random">Seeded RNG for deterministic behavior.</param>
    /// <returns>A new offspring genome.</returns>
    Genome Cross(
        Genome parent1, double parent1Fitness,
        Genome parent2, double parent2Fitness,
        Random random);
}
```

## Implementation: NeatCrossover

**Constructor dependencies**: `IOptions<NeatSharpOptions>`

### Gene Alignment Algorithm

1. **Identify the fitter parent**: Compare `parent1Fitness` vs `parent2Fitness`. If equal, both are considered equally fit (disjoint/excess from both are included).
2. **Sort connections by innovation number** (already ordered by construction, but verify via linear scan).
3. **Two-pointer merge**:
   - Both pointers at same innovation number: **matching gene** — randomly inherit from either parent (50/50).
   - One pointer's innovation is lower: **disjoint gene** — inherit from fitter parent only (or both if equal fitness).
   - One parent exhausted, other has remaining: **excess genes** — inherit from fitter parent only (or both if equal fitness).

### Disabled Gene Rule

For matching genes where the gene is disabled in either parent:
- With probability `DisabledGeneInheritanceProbability` (default 0.75), the offspring gene is disabled.
- Otherwise, the offspring gene is enabled.

### Node Inheritance

- Collect all node IDs referenced by inherited connections.
- Include all input, output, and bias nodes from the fitter parent (or parent1 if equal fitness).
- For each node ID, prefer the node definition from the parent whose connection was inherited (preserving activation functions).
- If a node is referenced by connections from both parents, prefer the fitter parent's node definition.

### Invariants

- The offspring is a valid `Genome`: all connection source/target IDs reference nodes in the offspring.
- The offspring is immutable.
- Deterministic given the same inputs and random seed.
- The innovation numbers in the offspring are a subset of the union of both parents' innovation numbers (no new innovations created during crossover).
