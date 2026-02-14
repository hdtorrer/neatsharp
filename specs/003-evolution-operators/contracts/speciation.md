# Contract: Speciation

**Feature Branch**: `003-evolution-operators`
**Namespace**: `NeatSharp.Evolution.Speciation`

## Interfaces

### ICompatibilityDistance

```csharp
/// <summary>
/// Computes the structural and parametric distance between two genomes.
/// </summary>
public interface ICompatibilityDistance
{
    /// <summary>
    /// Computes the compatibility distance between two genomes.
    /// </summary>
    /// <param name="genome1">First genome.</param>
    /// <param name="genome2">Second genome.</param>
    /// <returns>A non-negative distance value.</returns>
    double Compute(Genome genome1, Genome genome2);
}
```

### ISpeciationStrategy

```csharp
/// <summary>
/// Assigns genomes to species based on structural similarity.
/// </summary>
public interface ISpeciationStrategy
{
    /// <summary>
    /// Assigns all genomes in the population to species.
    /// Modifies the species list in place (adds/removes species, updates members).
    /// </summary>
    /// <param name="population">Genomes to assign, each with its fitness score.</param>
    /// <param name="species">Current species list. Modified in place.</param>
    void Speciate(
        IReadOnlyList<(Genome Genome, double Fitness)> population,
        List<Species> species);
}
```

## Implementation: CompatibilityDistance

**Constructor dependencies**: `IOptions<NeatSharpOptions>`

### Formula

```
d = (c1 * E / N) + (c2 * D / N) + (c3 * W)
```

Where:
- `E` = excess gene count (genes beyond the range of the other genome's innovations)
- `D` = disjoint gene count (non-matching genes within the innovation range of both genomes)
- `W` = average absolute weight difference of matching genes (0.0 if no matching genes)
- `N` = max(connection count of larger genome, 1) (prevents division by zero)
- `c1` = `SpeciationOptions.ExcessCoefficient` (default 1.0)
- `c2` = `SpeciationOptions.DisjointCoefficient` (default 1.0)
- `c3` = `SpeciationOptions.WeightDifferenceCoefficient` (default 0.4)

### Algorithm

Two-pointer merge on connections sorted by innovation number:
1. Initialize: `excess = 0, disjoint = 0, weightDiffSum = 0.0, matchCount = 0`
2. While both pointers valid:
   - If innovations match: `matchCount++`, `weightDiffSum += |w1 - w2|`, advance both
   - If genome1's innovation < genome2's: `disjoint++`, advance genome1
   - Else: `disjoint++`, advance genome2
3. Remaining genes in either pointer: all are excess genes, add to `excess`
4. `W = matchCount > 0 ? weightDiffSum / matchCount : 0.0`
5. `N = Math.Max(genome1.Connections.Count, genome2.Connections.Count, 1)`
6. Return `(c1 * excess / N) + (c2 * disjoint / N) + (c3 * W)`

## Implementation: CompatibilitySpeciation

**Constructor dependencies**: `IOptions<NeatSharpOptions>`, `ICompatibilityDistance`

### Speciation Algorithm

1. **Clear members**: For each existing species, clear the member list but preserve metadata (ID, representative, best fitness, stagnation counter).
2. **Assign genomes**: For each genome in population order:
   a. Compute compatibility distance to each existing species' representative.
   b. If distance < `CompatibilityThreshold` for any species, assign to the **first** match.
   c. Otherwise, create a new species with this genome as both member and representative.
3. **Remove empty species**: Any species with 0 members is removed.
4. **Update representatives**: For each species, set representative to the best-performing member (highest fitness).
5. **Update stagnation**: For each species:
   - If current best fitness > `BestFitnessEver`: update `BestFitnessEver`, reset `GenerationsSinceImprovement = 0`
   - Otherwise: increment `GenerationsSinceImprovement`

### Species Entity

```csharp
public sealed class Species
{
    public int Id { get; }
    public Genome Representative { get; internal set; }
    public List<(Genome Genome, double Fitness)> Members { get; } = [];
    public double BestFitnessEver { get; internal set; }
    public int GenerationsSinceImprovement { get; internal set; }

    public Species(int id, Genome representative);
    public double AverageFitness { get; } // computed from Members
}
```

### Invariants

- Every genome in the population is assigned to exactly one species.
- Species IDs are stable across generations (same ID reused if species survives).
- Deterministic given the same genome ordering and configuration.
- New species IDs are monotonically increasing.
