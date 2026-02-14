# Contract: Mutation Operators

**Feature Branch**: `003-evolution-operators`
**Namespace**: `NeatSharp.Evolution.Mutation`

## Interfaces

### IMutationOperator

```csharp
/// <summary>
/// Applies a single type of mutation to a genome, producing a new immutable genome.
/// The original genome is never modified.
/// </summary>
public interface IMutationOperator
{
    /// <summary>
    /// Applies this mutation to the given genome.
    /// </summary>
    /// <param name="genome">The source genome. Not modified.</param>
    /// <param name="random">Seeded RNG for deterministic behavior.</param>
    /// <param name="tracker">Innovation tracker for structural mutations.</param>
    /// <returns>
    /// A new genome with the mutation applied, or the original genome unchanged
    /// if the mutation was not applicable (e.g., complexity limits reached, fully connected).
    /// </returns>
    Genome Mutate(Genome genome, Random random, IInnovationTracker tracker);
}
```

## Implementations

### WeightPerturbationMutation

**Behavior**: For each connection in the genome, adjusts its weight by a small random value drawn from the configured distribution (uniform or Gaussian). Clamps result to `[WeightMinValue, WeightMaxValue]`.

**Constructor dependencies**: `IOptions<NeatSharpOptions>`

**Contract**:
- Input: genome with 0+ connections
- Output: new genome with all same structure, perturbed weights
- The genome node list is identical (same references via record equality)
- Each weight is modified by `delta` where:
  - Uniform: `delta = random.NextDouble() * 2 * power - power` (range [-power, +power])
  - Gaussian: `delta = NormalSample(random, 0, power)`
- Result weights clamped to `[WeightMinValue, WeightMaxValue]`

### WeightReplacementMutation

**Behavior**: Replaces the weight of a randomly selected connection with a new random value drawn uniformly from `[WeightMinValue, WeightMaxValue]`.

**Constructor dependencies**: `IOptions<NeatSharpOptions>`

**Contract**:
- Input: genome with 1+ connections
- Output: new genome with one connection's weight replaced
- If genome has 0 connections, returns genome unchanged

### AddConnectionMutation

**Behavior**: Selects two random nodes that are not already directly connected and adds a new connection between them with a random weight. Rejects connections that would create a cycle in feed-forward mode.

**Constructor dependencies**: `IOptions<NeatSharpOptions>`

**Contract**:
- Input: genome with 2+ nodes
- Output: new genome with one additional connection, or original genome unchanged if:
  - Genome connection count >= `ComplexityLimits.MaxConnections`
  - No valid (non-cycle-creating, non-duplicate) node pair found after `MaxAddConnectionAttempts` attempts
  - All possible pairs are already connected
- The new connection has:
  - `InnovationNumber` from `IInnovationTracker.GetConnectionInnovation(source, target)`
  - `Weight` from `random.NextDouble() * (max - min) + min`
  - `IsEnabled = true`
- Source node must not be an output node targeting itself
- Target node must not be an input or bias node
- Cycle detection: DFS from target node following enabled connections; if source is reachable, reject pair

### AddNodeMutation

**Behavior**: Selects a random enabled connection, disables it, inserts a new hidden node, and creates two new connections: source-to-new (weight 1.0) and new-to-target (original weight).

**Constructor dependencies**: `IOptions<NeatSharpOptions>`

**Contract**:
- Input: genome with 1+ enabled connections
- Output: new genome with one additional node and two additional connections, or original genome unchanged if:
  - Genome node count >= `ComplexityLimits.MaxNodes`
  - No enabled connections exist
- The original connection is disabled (IsEnabled = false)
- New node: `NodeGene(splitResult.NewNodeId, NodeType.Hidden, ActivationFunctions.Sigmoid)`
- New connection 1 (source to new): weight = 1.0, innovation from tracker
- New connection 2 (new to target): weight = original connection's weight, innovation from tracker
- Innovation IDs obtained via `IInnovationTracker.GetNodeSplitInnovation(connectionInnovation)`

### ToggleEnableMutation

**Behavior**: Selects a random connection and flips its `IsEnabled` state.

**Constructor dependencies**: None

**Contract**:
- Input: genome with 1+ connections
- Output: new genome with one connection's enabled state toggled
- If genome has 0 connections, returns genome unchanged

### CompositeMutationOperator

**Behavior**: Orchestrates the application of individual mutations based on configured rates. For each mutation type, rolls a random number against the configured rate and applies the mutation if triggered. Multiple mutations can apply to the same genome in sequence within a single call.

**Constructor dependencies**: `IOptions<NeatSharpOptions>`, `WeightPerturbationMutation`, `WeightReplacementMutation`, `AddConnectionMutation`, `AddNodeMutation`, `ToggleEnableMutation`

**Contract**:
- Applies mutations in a fixed order: weight perturbation/replacement, then structural (add-node, add-connection), then toggle-enable
- Weight perturbation and weight replacement are mutually exclusive per application: roll once, if < perturbation rate apply perturbation, else if < perturbation + replacement rate apply replacement
- Structural mutations are independent: each rolls separately against its rate
- Returns the final genome after all applicable mutations
