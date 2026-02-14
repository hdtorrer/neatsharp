# Contract: Selection & Reproduction

**Feature Branch**: `003-evolution-operators`
**Namespace**: `NeatSharp.Evolution.Selection`

## Interfaces

### IParentSelector

```csharp
/// <summary>
/// Selects a parent genome from a pool of candidates for reproduction.
/// </summary>
public interface IParentSelector
{
    /// <summary>
    /// Selects a single parent from the given candidates.
    /// </summary>
    /// <param name="candidates">
    /// Eligible members of a species with their fitness scores.
    /// Guaranteed to have at least one member.
    /// </param>
    /// <param name="random">Seeded RNG for deterministic behavior.</param>
    /// <returns>The selected parent genome.</returns>
    Genome Select(IReadOnlyList<(Genome Genome, double Fitness)> candidates, Random random);
}
```

## Implementations

### TournamentSelector (default)

**Constructor dependencies**: `IOptions<NeatSharpOptions>`

**Algorithm**:
1. Pick `TournamentSize` random candidates from the pool (with replacement).
2. Return the candidate with the highest fitness.

**Properties**:
- Default tournament size: 2
- Selection pressure increases with tournament size.
- O(k) per selection where k = tournament size.

### RouletteWheelSelector

**Constructor dependencies**: None

**Algorithm**:
1. Compute total fitness of all candidates. If any fitness <= 0, shift all fitnesses by `|min| + epsilon` to ensure all positive.
2. Generate a random number in `[0, totalFitness)`.
3. Walk candidates accumulating fitness until the accumulated value exceeds the random number.
4. Return the current candidate.

**Properties**:
- Selection probability proportional to fitness.
- O(n) per selection.

### StochasticUniversalSamplingSelector

**Constructor dependencies**: None

**Algorithm**:
1. Compute total fitness (shift if needed, same as roulette).
2. Compute pointer spacing: `totalFitness / numberOfSelections`.
3. Generate a single random start in `[0, spacing)`.
4. Place evenly spaced pointers from start.
5. Walk candidates, assigning each pointer to the candidate whose cumulative fitness range covers it.

**Properties**:
- More uniform distribution than roulette wheel.
- O(n) for batch selection of multiple parents.
- Note: `Select()` method selects one parent at a time (consistent with interface). For SUS to provide its batch benefit, the reproduction loop should call it in sequence with the same random instance, which naturally spaces selections.

## ReproductionAllocator

**Constructor dependencies**: `IOptions<NeatSharpOptions>`

### Offspring Allocation Algorithm

```csharp
public IReadOnlyDictionary<int, int> AllocateOffspring(
    IReadOnlyList<Species> species,
    int populationSize);
```

**Algorithm**:
1. **Identify stagnant species**: species with `GenerationsSinceImprovement > StagnationThreshold`.
2. **Protect top 2**: Sort all species by `BestFitnessEver` descending. The top 2 are never fully eliminated.
3. **Compute adjusted average fitness** per species:
   - Non-stagnant species: `adjustedAvg = species.AverageFitness`
   - Stagnant species (not in top 2): `adjustedAvg = 0.0`
   - Stagnant species (in top 2): `adjustedAvg = species.AverageFitness` (preserved)
4. **Compute total adjusted fitness**: sum of all species' adjusted averages.
5. **Allocate proportionally**: `offspring[i] = round(adjustedAvg[i] / totalAdjusted * populationSize)`
6. **Account for elitism**: For each species with membership >= `ElitismThreshold`, 1 slot is reserved for the champion (subtracted from offspring count, champion copied unchanged).
7. **Adjust for rounding**: If sum of offspring != `populationSize`, add/subtract from the species with the largest fractional remainder.

**Returns**: Dictionary mapping species ID to offspring count (including the elite champion).

### Reproduction Workflow (orchestrated by evolution engine)

For each species with allocated offspring:
1. If species size >= `ElitismThreshold`: copy champion unchanged to next generation (1 offspring).
2. For remaining offspring slots:
   a. Filter species members to top `SurvivalThreshold` fraction.
   b. Roll crossover vs clone: `random.NextDouble() < CrossoverRate`?
      - **Crossover**: Select two parents via `IParentSelector`.
        - Roll interspecies: `random.NextDouble() < InterspeciesCrossoverRate`?
          - Yes: second parent from a random different species.
          - No: both parents from same species.
        - Produce offspring via `ICrossoverOperator.Cross()`.
      - **Clone**: Select one parent via `IParentSelector`. Clone (create identical genome).
   c. Apply mutations to offspring via `CompositeMutationOperator`.
   d. Add offspring to next generation.

### Invariants

- Total offspring count exactly equals `PopulationSize`.
- Champions are genome-identical to their source (not mutated).
- Stagnant species (except top 2) receive 0 offspring.
- At least the top 2 species by peak fitness always receive offspring.
- Deterministic given the same seed, population, and configuration.
