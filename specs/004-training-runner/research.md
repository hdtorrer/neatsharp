# Research: Training Runner + Evaluation Adapters + Reporting

**Feature Branch**: `004-training-runner`
**Date**: 2026-02-14

## R-001: Population Initialization Strategy

**Decision**: Create minimal-topology genomes with all input nodes directly connected to all output nodes (fully connected input-to-output), plus one bias node. Each genome is structurally identical but with randomized connection weights.

**Rationale**: This is the canonical NEAT initialization approach from Stanley's original paper. All genomes start with the simplest possible topology, and structural complexity emerges only through mutations. This ensures the innovation numbers for initial connections are shared across the entire population (same structural topology = same innovation numbers).

**Alternatives considered**:
- *Sparse random connections*: Would break innovation number alignment and make initial crossover unreliable. Rejected.
- *No initial connections*: Would make generation-0 evaluation meaningless (all outputs zero). Rejected.
- *Hidden nodes in initial topology*: Contradicts NEAT principle of minimal starting topology. Rejected.

**Implementation details**:
- Node layout: `InputCount` input nodes (IDs 0..N-1) + 1 bias node (ID N) + `OutputCount` output nodes (IDs N+1..N+O).
- Connections: Every (input, output) and (bias, output) pair gets a connection with innovation numbers assigned sequentially via the `IInnovationTracker`.
- Weights: Randomized from uniform distribution over `[WeightMinValue, WeightMaxValue]` using the seeded `Random`.
- The `IInnovationTracker` must be initialized *before* population creation so that innovation numbers start at 0 and the initial connections consume IDs 0..((I+1)*O - 1). The tracker's `NextGeneration()` is called after population creation and before the first generation loop begins.
- Node IDs are assigned sequentially: inputs 0..I-1, bias I, outputs I+1..I+O. The tracker's starting node ID is I+O+1.

## R-002: Innovation Tracker Initialization

**Decision**: The `InnovationTracker` (scoped, one per evolution run) is initialized with `startInnovationNumber = 0` and `startNodeId = 0`. Population creation consumes innovation numbers for the initial fully-connected topology. After population creation, `NextGeneration()` is called to clear the per-generation dedup cache before the first evolution generation.

**Rationale**: Since all initial genomes share the same topology, they all get the same innovation numbers for corresponding connections. The tracker's dedup cache handles this — the first genome's connections assign innovation numbers, and subsequent genomes reuse the same numbers for the same (source, target) pairs.

**Alternatives considered**:
- *Pre-compute innovation numbers outside the tracker*: Would duplicate logic and risk drift between initialization and mutation paths. Rejected.
- *Initialize tracker with pre-allocated IDs*: Unnecessary since the tracker's dedup cache handles identical connections naturally. Rejected.

## R-003: NeatSharpOptions Extension — InputCount and OutputCount

**Decision**: Add `InputCount` (default 2, range [1, 10000]) and `OutputCount` (default 1, range [1, 10000]) properties to `NeatSharpOptions`. These are required for population initialization (FR-001).

**Rationale**: The spec says "configured number of input and output nodes." These are fundamental run parameters that belong with all other configuration. They cannot be inferred from the evaluation strategy since the strategy is opaque to the training loop.

**Alternatives considered**:
- *Add to RunAsync parameters*: Would change the established `INeatEvolver` contract and require updating all extension methods. The interface is already defined as the public API. Rejected.
- *Separate NetworkTopologyOptions sub-object*: Unnecessary indirection for two simple properties. Rejected.

## R-004: Run-Level Stagnation Detection

**Decision**: After speciation, check if *all* species have `GenerationsSinceImprovement > StagnationThreshold` from `SelectionOptions`. If so, the run-level stagnation stopping criterion is triggered (per clarification: "All species simultaneously stagnant").

**Rationale**: This matches the spec clarification exactly. The per-species stagnation counters are already maintained by `CompatibilitySpeciation.Speciate()` (incrementing/resetting `GenerationsSinceImprovement` based on `BestFitnessEver`). The training loop only needs to read these values.

**Alternatives considered**:
- *Global best-fitness stagnation counter*: Doesn't match the spec's definition ("all species simultaneously stagnant per their individual species-level stagnation counters"). Rejected.
- *Configurable fraction of stagnant species*: Over-engineering beyond what's specified. Rejected.

**Important detail**: Run-level stagnation uses `StoppingCriteria.StagnationThreshold`, which is independent from `SelectionOptions.StagnationThreshold` (the latter controls per-species offspring penalty). Both may have the same value but serve different purposes.

## R-005: Structured Logging Approach

**Decision**: Use `[LoggerMessage]` source-generated methods in a `static partial class TrainingLog` for all training events. Events: generation completed, new best fitness, species extinct, stagnation detected, run completed.

**Rationale**: Source-generated `[LoggerMessage]` methods avoid boxing and string allocations when the log level is not enabled. This achieves the "zero overhead when disabled" requirement (FR-019) for logging. The `ILogger<NeatEvolver>` is injected via constructor.

**Log levels**:
- `Information`: Generation completed, run completed
- `Information`: New best fitness discovered
- `Warning`: Stagnation detected, species gone extinct
- `Debug`: Detailed per-generation metrics (only when metrics enabled)

**Alternatives considered**:
- *Manual `ILogger.Log()` calls*: Higher allocation overhead and no compile-time validation. Rejected.
- *Custom event system*: Would add a new abstraction layer. The existing `ILogger` infrastructure covers all needs. Rejected.

## R-006: Error Handling in Evaluation

**Decision**: Wrap individual genome evaluation in a try-catch within the evaluation loop. On exception, assign fitness 0.0 to the failed genome and log a warning. The run continues.

**Rationale**: FR-021 explicitly requires this behavior. The evaluation strategy's `EvaluatePopulationAsync` uses an `Action<int, double> setFitness` callback, so the training loop controls when fitness is assigned. For the sync/async/environment adapters, the loop iterates genome-by-genome. For the batch adapter, the entire batch call may fail — in that case, all genomes get 0.0 fitness.

**Implementation**: The error handling wraps the `IEvaluationStrategy.EvaluatePopulationAsync` call. Since the evaluation strategies already iterate internally, and the `setFitness` callback is how fitness is assigned, the approach is:
1. Initialize all fitness values to 0.0 (default safe value)
2. Call `EvaluatePopulationAsync` with a try-catch
3. If the entire evaluation throws, all genomes keep 0.0 fitness
4. Individual genome failures within adapters (sync/async/environment) are caught by the adapter's iteration — but since the existing adapters don't catch per-genome exceptions, the training loop wraps the whole call

**Note**: For more granular per-genome error handling, the evaluation adapters could be enhanced in a future spec. The current approach handles the common case.

## R-007: Complexity Limits Enforcement

**Decision**: After reproduction produces offspring, filter any genome that exceeds `ComplexityLimits.MaxNodes` or `ComplexityLimits.MaxConnections`. Replace over-limit offspring with a clone of a random parent from the same species (un-mutated).

**Rationale**: FR-007 says "System MUST enforce configured complexity limits during reproduction, preventing networks from exceeding maximum node or connection counts." The most natural enforcement point is post-reproduction filtering, since mutations (especially `AddNodeMutation` and `AddConnectionMutation`) are what push genomes over limits.

**Alternatives considered**:
- *Pre-mutation check in CompositeMutationOperator*: Would require the mutation operator to know about complexity limits, coupling it to configuration it currently doesn't reference. The mutation operators are stateless singletons. Rejected.
- *Reject and retry*: Non-deterministic and could loop indefinitely near the limit. Rejected.
- *Skip structural mutations when near limit*: More efficient but requires deep integration into mutation pipeline. Could be a future optimization.

**Implementation**: The `NeatEvolver` checks each offspring genome's `Nodes.Count` and `Connections.Count` against the configured limits after `ReproductionOrchestrator.Reproduce()` returns. Over-limit genomes are replaced with un-mutated clones from their parent species.

## R-008: Metrics Collection and Zero-Overhead Disable

**Decision**: When `EnableMetrics` is `true`, collect `GenerationStatistics` after each generation (evaluation, speciation, and reproduction timing via `Stopwatch`; fitness/complexity/species stats from the current population). When `false`, skip all collection — no `GenerationStatistics` objects allocated, no `Stopwatch` started.

**Rationale**: FR-019 requires "zero measurable overhead when disabled." The toggle check happens once at the top of the metrics collection block, gating all allocation and computation behind it.

**Implementation**:
- `Stopwatch` instances for evaluation, speciation, and reproduction phases — only created when metrics enabled
- `GenerationStatistics` record constructed from current population state — only when metrics enabled
- `RunHistory` receives either the populated list or an empty `Array.Empty<GenerationStatistics>()` list

## R-009: Champion Tracking Across Generations

**Decision**: Maintain a running `Champion` record (`Genome`, `Fitness`, `Generation`) across all generations. After each evaluation phase, compare the best genome of the current generation against the running champion. Update if the current generation's best exceeds the running champion's fitness.

**Rationale**: FR-016 and FR-020 require the champion to be the highest fitness found during the *entire* run (not just the final generation) and to track which generation it was first discovered.

**Implementation**:
- After evaluation, find the genome with maximum fitness in the current population
- If its fitness > current champion fitness (or this is generation 0), update the champion
- The champion genome reference is preserved (not copied), since `Genome` is immutable
- The champion needs to be converted to `IGenome` via `INetworkBuilder.Build()` for the `Champion` record (which holds `IGenome`)

**Note**: Looking at the `Champion` record: `public record Champion(IGenome Genome, double Fitness, int Generation)` — it stores `IGenome`, not `Genome`. The champion tracking during the loop works with `Genome` objects (raw genotype), and the final result converts the champion `Genome` to `IGenome` via `INetworkBuilder.Build()`.

## R-010: Cancellation Handling

**Decision**: Check `CancellationToken.IsCancellationRequested` at the start of each generation loop iteration (between generations). On cancellation, break the loop and return the current best result with `WasCancelled = true`. Do NOT throw `OperationCanceledException`.

**Rationale**: FR-004 and the `INeatEvolver` interface docs explicitly state: "Upon cancellation, the system returns the best result from the last fully completed generation rather than throwing an exception."

**Edge case — cancellation before any generation completes**: Return a result with the champion from the initial evaluated population (generation 0 evaluation happens before the first cancellation check point). If cancelled before even generation 0's evaluation completes, the evaluation strategy may throw `OperationCanceledException` — this is caught and the run returns whatever partial state exists.

## R-011: Example Selection — Function Approximation

**Decision**: Use sine wave approximation (sin(x) over [0, 2π]) as the second canonical problem.

**Rationale**: Sine approximation is a well-understood continuous function that requires:
- Continuous output (exercises non-binary activation)
- Non-trivial network topology (hidden nodes needed for good approximation)
- Clear success metric (MSE against known values)
- Different from XOR (continuous vs. discrete, single output regression vs. classification)

**Fitness function**: `fitness = 1.0 / (1.0 + MSE)` where MSE is computed over evenly-spaced sample points in [0, 2π]. This ensures fitness is in (0, 1] with higher being better.

**Alternatives considered**:
- *Quadratic (x²)*: Too simple — almost linear over small ranges. Rejected.
- *Polynomial (x³ - x)*: Viable but sine is more recognizable as a benchmark. Rejected.
- *Two-input function*: Increases complexity without additional validation value. Rejected.

## R-012: Genome-to-IGenome Conversion in Evaluation

**Decision**: The training loop builds `IGenome` (phenotype) for each `Genome` (genotype) before evaluation, using `INetworkBuilder.Build()`. The phenotype list is passed to the evaluation strategy. Fitness scores are mapped back to genotypes by index.

**Rationale**: The `IEvaluationStrategy.EvaluatePopulationAsync` accepts `IReadOnlyList<IGenome>`, which represents the phenotype (activatable network). The training loop works with `Genome` (genotype) for crossover, mutation, and speciation. The conversion is a necessary bridge.

**Performance note**: `Build()` creates a new `FeedForwardNetwork` per genome per generation. This is the expected cost for the CPU path. Future GPU acceleration could batch this conversion.

## R-013: Population Snapshot Construction

**Decision**: Build `PopulationSnapshot` from the current species list at run completion. Each species becomes a `SpeciesSnapshot` with its members' fitness, node count, and connection count as `GenomeInfo` records.

**Rationale**: FR-016 requires the final population snapshot in the result. The existing `PopulationSnapshot`, `SpeciesSnapshot`, and `GenomeInfo` records define the shape.
