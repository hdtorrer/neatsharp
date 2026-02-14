# Research: Evolution Operators (Mutation/Crossover) + Speciation + Selection

**Feature Branch**: `003-evolution-operators`
**Date**: 2026-02-13

## Research Summary

No NEEDS CLARIFICATION items remained after the spec clarification session. All technical decisions below were resolved by referencing the NEAT paper (Stanley & Miikkulainen, 2002), the existing codebase (Specs 001-002), and the feature spec clarifications.

---

## R-001: Random Number Generation Strategy

**Decision**: Use `System.Random` with seed-based construction.

**Rationale**: .NET's `System.Random` supports deterministic seeding via `new Random(seed)`. The spec mandates CPU deterministic reproducibility (FR-028) but explicitly excludes GPU and multi-threading. `System.Random` is sufficient for single-threaded, CPU-only operations and avoids external dependencies.

**Alternatives considered**:
- **Custom Xoshiro256** PRNG: Higher quality randomness, but adds unnecessary complexity. `System.Random` in .NET 8+ uses Xoshiro256** internally, so we get the same quality.
- **`RandomNumberGenerator` (crypto)**: Not seedable. Incompatible with reproducibility requirement.

**Integration**: A single `Random` instance per evolution run, seeded from `NeatSharpOptions.Seed`. Passed to all operators. When `Seed` is null, use a random seed and record it in `EvolutionResult.Seed` for replay.

---

## R-002: Mutation Operator Architecture

**Decision**: Each mutation type is a separate class implementing `IMutationOperator`. A `CompositeMutationOperator` applies them probabilistically based on configured rates.

**Rationale**: Single Responsibility (Constitution VI). Each operator has one reason to change. The composite pattern allows rate configuration without modifying individual operators. Users can inject custom mutation operators via DI.

**Alternatives considered**:
- **Single `MutationService` with switch/case**: Violates SRP and OCP. Adding a new mutation type requires modifying the service.
- **Extension method approach**: Not injectable, not testable with mocks.

**Interface design**:
```csharp
public interface IMutationOperator
{
    Genome Mutate(Genome genome, Random random, IInnovationTracker tracker);
}
```

The `CompositeMutationOperator` rolls the dice per-mutation-type using configured rates and applies matching operators. It does NOT implement `IMutationOperator` itself — it is an orchestrator that the reproduction logic calls directly.

---

## R-003: Crossover Gene Alignment Algorithm

**Decision**: Sort both parents' connections by innovation number, then two-pointer merge to classify matching/disjoint/excess genes.

**Rationale**: Innovation numbers are the canonical alignment mechanism in NEAT. A two-pointer merge on sorted arrays is O(n+m) and straightforward. Both parents' `Connections` lists are already `IReadOnlyList<ConnectionGene>` ordered by insertion (which tracks innovation number order from `InnovationTracker`).

**Alternatives considered**:
- **HashMap lookup**: Build a dictionary from one parent, iterate the other. Same complexity but more allocation. Two-pointer is cleaner for the three-way classification (matching/disjoint/excess).

**Key behaviors**:
1. Matching genes: randomly inherit from either parent (50/50 per gene).
2. Disabled gene rule: if a matching gene is disabled in either parent, 75% chance it's disabled in offspring.
3. Disjoint/excess: from the fitter parent. Equal fitness: from both parents.
4. Nodes: collect all unique nodes referenced by inherited connections, plus all input/output/bias nodes from the fitter parent.

---

## R-004: Compatibility Distance Computation

**Decision**: Implement the standard NEAT formula: `d = (c1 * E / N) + (c2 * D / N) + (c3 * W)`.

**Rationale**: This is the canonical formula from the NEAT paper, mandated by FR-015. N = max(connection gene count of larger genome, 1) to avoid division by zero.

**Implementation detail**: Use the same two-pointer merge as crossover to count excess (E), disjoint (D), and compute average weight difference of matching genes (W) in a single pass. Extract this shared logic into a helper used by both crossover and compatibility distance.

**Alternatives considered**:
- **Omit N normalization for small genomes** (as suggested in some NEAT implementations): The spec explicitly includes N in the formula. We use it as-is with the minimum-1 guard.

---

## R-005: Speciation Assignment Strategy

**Decision**: Sequential comparison against species representatives. Genome joins the first species with distance below threshold.

**Rationale**: FR-016 mandates "joins the first species whose representative has compatibility distance below the configurable compatibility threshold." This is the canonical NEAT approach and ensures determinism given the same genome ordering (FR-019).

**Implementation**:
1. Clear species membership at generation start (keep species metadata).
2. For each genome in order: compute distance to each species representative.
3. If distance < threshold for any species, assign to the first match.
4. Otherwise, create a new species with this genome as representative.
5. After all assignments: update representatives (default: best-performing member per FR-017).

**Species tracking**: A `Species` class tracks: ID, representative genome, member list, best fitness ever, stagnation generation counter.

---

## R-006: Cycle Detection for Add-Connection Mutation

**Decision**: Reuse DFS-based reachability check from the source node to detect if adding a connection from A to B would create a cycle (i.e., B can already reach A via existing enabled connections).

**Rationale**: The existing `FeedForwardNetworkBuilder` uses Kahn's algorithm for topological sort and detects cycles post-hoc. For the mutation, we need a pre-check before adding the connection. A simple DFS from B following enabled connections to see if A is reachable is O(V+E) and sufficient for single-threaded use.

**Alternatives considered**:
- **Full topological sort then check**: More expensive and allocates more. DFS is simpler for a boolean reachability query.
- **Maintain topological order incrementally**: Over-engineering for single-threaded CPU.

**Edge case**: If no valid (non-cycle-creating, not-already-connected) pair is found after a configurable number of random attempts, the mutation is skipped (genome returned unchanged).

---

## R-007: Parent Selection Implementations

**Decision**: Three implementations behind `IParentSelector`, registered via DI. Tournament (default), roulette wheel, and SUS.

**Rationale**: FR-023 mandates all three with tournament as default. The `IParentSelector` interface allows user injection of custom strategies per Constitution VI (OCP, DIP).

**Interface design**:
```csharp
public interface IParentSelector
{
    Genome Select(IReadOnlyList<(Genome Genome, double Fitness)> candidates, Random random);
}
```

**Tournament selection**: Pick `k` random candidates, return the one with highest fitness. Default k=2.
**Roulette wheel**: Probability proportional to fitness. Requires positive fitness; shift to minimum if needed.
**SUS**: Single random start, evenly spaced pointers. More uniform than roulette for batch selection.

---

## R-008: Reproduction Allocation Formula

**Decision**: Each species' offspring count = round(species_average_fitness / total_average_fitness * population_size). Stagnant species get 0. Top 2 by peak fitness exempt from elimination.

**Rationale**: FR-020 through FR-022 mandate this. Rounding may not sum to exactly population_size, so adjust the largest species' allocation to compensate (standard NEAT practice).

**Stagnation logic**: Track `bestFitnessEver` and `generationsSinceImprovement` per species. When `generationsSinceImprovement > stagnationThreshold`, set offspring allocation to 0. Exception: the top 2 species by `bestFitnessEver` are never fully eliminated.

---

## R-009: Configuration Options Design

**Decision**: Nested options classes under `NeatSharpOptions` following the existing pattern (`Stopping`, `Complexity`).

**Rationale**: Consistent with existing configuration design (Spec 01). The Options pattern with `IOptions<T>` and `ValidateDataAnnotations` is already established.

**New options classes**:
- `MutationOptions`: rates for each mutation type, weight range, perturbation power, distribution type, max add-connection attempts
- `CrossoverOptions`: disabled gene inheritance probability, crossover rate, interspecies crossover rate
- `SpeciationOptions`: c1, c2, c3 coefficients, compatibility threshold, representative strategy
- `SelectionOptions`: elitism threshold, stagnation threshold, survival threshold (fraction)

All exposed as properties on `NeatSharpOptions` with sensible NEAT-paper defaults.

---

## R-010: Complexity Penalty Integration

**Decision**: Optional fitness modifier applied before selection. Formula: `adjustedFitness = rawFitness - coefficient * complexityMeasure`.

**Rationale**: FR-026 mandates configurable penalty. Applying it before selection ensures it affects reproduction allocation. The complexity measure can be node count, connection count, or both (configurable).

**Configuration**: A `ComplexityPenaltyOptions` nested class with:
- `Coefficient`: double (default 0.0 = disabled)
- `Metric`: enum { NodeCount, ConnectionCount, Both } (default Both)
- When `Both`: penalty = coefficient * (nodeCount + connectionCount)

This integrates naturally with the existing `ComplexityLimits` for hard caps (FR-027).
