# Feature Specification: Evolution Operators (Mutation/Crossover) + Speciation + Selection

**Feature Branch**: `003-evolution-operators`
**Created**: 2026-02-13
**Status**: Draft
**Input**: User description: "Implement canonical NEAT evolutionary mechanics — mutation operators, crossover, speciation via compatibility distance, selection with elitism, and optional complexity controls — with correctness-focused invariants and full deterministic reproducibility."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mutate a Genome (Priority: P1)

The evolution engine (or a library consumer testing mutations in isolation) applies mutation operators to an existing genome to produce a new, structurally or parametrically modified genome. Available mutations include perturbing or replacing connection weights, adding a new connection between previously unconnected nodes, splitting an existing connection by inserting a new hidden node, and toggling a connection's enabled state. Each mutation produces a new immutable genome and uses the innovation tracker to assign deterministic innovation identifiers to any structural changes.

**Why this priority**: Mutation is the sole source of genetic variation in NEAT. Without mutation operators, the population cannot explore the solution space at all — no new topologies, no weight adjustments, no evolution. Every other feature (crossover, speciation, selection) operates on the diversity that mutation creates.

**Independent Test**: Can be fully tested by constructing a minimal genome, applying each mutation type individually with a fixed random seed, and verifying that the resulting genome has the expected structural change (new connection, new node, modified weight, toggled enable flag) with correct innovation identifiers.

**Acceptance Scenarios**:

1. **Given** a genome with existing connections, **When** weight perturbation is applied with a fixed seed, **Then** connection weights in the resulting genome are modified by small bounded amounts and the genome structure (nodes, topology) is unchanged.
2. **Given** a genome with existing connections, **When** weight replacement is applied, **Then** one or more connection weights in the resulting genome are replaced with new random values within the configured range.
3. **Given** a genome where two existing nodes are not directly connected, **When** an add-connection mutation is applied, **Then** the resulting genome contains a new connection between those nodes with a random weight and a new innovation identifier from the tracker.
4. **Given** a genome with an enabled connection from node A to node B, **When** an add-node mutation is applied to that connection, **Then** the resulting genome contains a new hidden node C, a connection from A to C with weight 1.0, a connection from C to B with the original weight, and the original A-to-B connection is disabled. Innovation identifiers are assigned via the tracker.
5. **Given** a genome with a disabled connection, **When** a toggle-enable mutation is applied, **Then** that connection becomes enabled in the resulting genome (and vice versa for an enabled connection).
6. **Given** a genome that has reached the configured maximum node or connection count, **When** a structural mutation (add-node or add-connection) is attempted, **Then** the mutation is skipped and the genome is returned unchanged.

---

### User Story 2 - Cross Two Genomes (Priority: P2)

The evolution engine combines two parent genomes into an offspring genome using NEAT's innovation-number-aligned crossover. Connection genes are aligned by innovation number: matching genes are inherited randomly from either parent, disjoint and excess genes are inherited from the more-fit parent. The offspring genome is a valid, immutable genome ready for further mutation or evaluation.

**Why this priority**: Crossover is the mechanism by which beneficial structural innovations from different lineages are combined. Without crossover, each genome's topology evolves in isolation and the algorithm cannot recombine successful sub-structures. It directly depends on mutation (P1) to produce the genomes being crossed.

**Independent Test**: Can be fully tested by constructing two parent genomes with known overlapping and non-overlapping innovation numbers, specifying which parent is more fit, performing crossover with a fixed seed, and verifying the offspring has the expected gene composition — matching genes from either parent, disjoint/excess from the fitter parent.

**Acceptance Scenarios**:

1. **Given** two parent genomes with some matching innovation numbers and some disjoint/excess genes, and parent A has higher fitness, **When** crossover is performed, **Then** the offspring contains all matching genes (randomly selected from either parent per gene) plus all disjoint and excess genes from parent A.
2. **Given** two parent genomes with equal fitness, **When** crossover is performed, **Then** disjoint and excess genes from both parents are included in the offspring.
3. **Given** two parent genomes, **When** crossover is performed with the same seed multiple times, **Then** the offspring is identical each time (deterministic).
4. **Given** two parent genomes where a matching gene is disabled in one parent and enabled in the other, **When** crossover inherits that gene, **Then** the gene has a configurable probability of being disabled in the offspring (default 75% chance of disabled).

---

### User Story 3 - Assign Genomes to Species (Priority: P3)

The evolution engine groups the population into species based on genomic similarity. Each genome is compared to existing species representatives using a compatibility distance metric (based on counts of disjoint genes, excess genes, and average weight difference of matching genes). If the distance is below a configurable threshold, the genome joins that species; otherwise, a new species is created. Species assignment is deterministic given the same ordering and configuration.

**Why this priority**: Speciation is the mechanism that protects structural innovation in NEAT. Without species-based grouping, novel topologies compete directly against established solutions and are eliminated before they can optimize their weights. Speciation depends on having genomes (P1 mutation produces variation) and crossover (P2, as species influence mating).

**Independent Test**: Can be fully tested by constructing a set of genomes with known structural differences, running speciation with a fixed threshold, and verifying that genomes with similar structure are grouped together while structurally different genomes are placed in separate species.

**Acceptance Scenarios**:

1. **Given** a population of genomes and an empty species list, **When** speciation runs, **Then** the first genome becomes the representative of a new species and subsequent genomes are assigned to existing species or create new species based on compatibility distance.
2. **Given** two genomes that are identical, **When** compatibility distance is calculated, **Then** the distance is 0.0.
3. **Given** two genomes with many disjoint genes and different weights, **When** compatibility distance is calculated, **Then** the distance exceeds the compatibility threshold and they are placed in different species.
4. **Given** the same population processed in the same order with the same configuration, **When** speciation runs twice, **Then** the species assignments are identical.
5. **Given** a species with a representative from the previous generation, **When** a new generation's genomes are assigned, **Then** species representatives are updated (e.g., to the best-performing member) for the next generation's comparisons.

---

### User Story 4 - Select Parents and Apply Elitism (Priority: P4)

The evolution engine selects parent genomes for reproduction based on fitness within their species. High-fitness genomes within each species are preferentially selected as parents. The champion of each species above a configurable size threshold is carried forward unchanged into the next generation (elitism). Species reproduction allocation is proportional to average species fitness, ensuring that higher-performing species contribute more offspring.

**Why this priority**: Selection drives the evolutionary pressure toward better solutions. Without fitness-proportional selection and elitism, the algorithm degenerates into random search. This depends on speciation (P3) for fitness sharing and species-level allocation.

**Independent Test**: Can be fully tested by constructing a population with known fitness scores grouped into species, running selection, and verifying that higher-fitness genomes are selected more often as parents, champions are preserved, and reproduction counts match expected proportions.

**Acceptance Scenarios**:

1. **Given** a species with 10 members of varying fitness, **When** parent selection occurs, **Then** higher-fitness members are selected as parents more frequently than lower-fitness members.
2. **Given** a species with more members than the elitism threshold, **When** the next generation is created, **Then** the species champion (highest fitness member) appears in the next generation with unchanged genome.
3. **Given** three species with average fitnesses of 10.0, 5.0, and 2.5, **When** reproduction allocation is calculated for a population of 100, **Then** species receive offspring counts roughly proportional to their average fitness (approximately 57, 29, 14).
4. **Given** a species that has had no fitness improvement for more than the stagnation threshold generations, **When** reproduction allocation is calculated, **Then** that species receives zero or minimal offspring allocation (stagnation penalty).
5. **Given** the same population, fitness scores, and configuration, **When** selection runs twice with the same seed, **Then** the selected parents and offspring allocation are identical.

---

### User Story 5 - Control Complexity Growth (Priority: P5)

A library consumer configures optional complexity controls to prevent unbounded network growth during evolution. Controls include hard caps on the maximum number of nodes and connections per genome, and an optional complexity penalty that reduces the effective fitness of overly complex genomes. When a genome reaches a hard cap, structural mutations that would exceed the cap are skipped.

**Why this priority**: Complexity controls are an optimization and tuning concern rather than a core algorithmic requirement. NEAT can function without them, but they prevent runaway growth in long evolution runs. This depends on all prior stories being functional.

**Independent Test**: Can be fully tested by configuring complexity limits, evolving a population that tends to grow, and verifying that no genome exceeds the configured node/connection caps, and that the complexity penalty reduces effective fitness as expected.

**Acceptance Scenarios**:

1. **Given** a maximum node limit of 20 configured, **When** an add-node mutation is attempted on a genome with 20 nodes, **Then** the mutation is skipped and the genome is returned unchanged.
2. **Given** a maximum connection limit of 50 configured, **When** an add-connection mutation is attempted on a genome with 50 connections, **Then** the mutation is skipped and the genome is returned unchanged.
3. **Given** a complexity penalty coefficient of 0.01, **When** a genome with 15 nodes has a raw fitness of 10.0, **Then** its adjusted fitness is reduced by 0.01 * 15 = 0.15, yielding an effective fitness of 9.85.
4. **Given** no complexity limits are configured (defaults), **When** mutations are applied, **Then** there are no restrictions on genome growth.

---

### Edge Cases

- What happens when an add-connection mutation cannot find two unconnected nodes (genome is fully connected)? The mutation is skipped and the genome is returned unchanged. No error is thrown.
- What happens when an add-node mutation is applied but there are no enabled connections to split? The mutation is skipped and the genome is returned unchanged.
- What happens when crossover is performed between a genome and itself? The offspring is a clone of the parent (all genes match, one parent's copy is selected for each gene). This is valid and produces a functional genome.
- What happens when all species go stagnant simultaneously? At least the top two species (by peak fitness) are preserved from stagnation penalties to prevent population collapse.
- What happens when a species shrinks to a single member? That member can still reproduce (via mutation only, no crossover partner) and serves as both representative and champion if the species is above the elitism size threshold. If below, it still participates but is not guaranteed elitism protection.
- What happens when the compatibility threshold is set very low (e.g., 0.0)? Every genome becomes its own species. This is valid but degrades performance; the library does not prevent it.
- What happens when the compatibility threshold is very high? All genomes cluster into a single species. This is valid but eliminates the protective effect of speciation.
- What happens when a mutation produces a connection that creates a cycle in a feed-forward genome? The add-connection mutation must check for cycles before adding the connection. If the proposed connection would create a cycle, it is rejected and a different pair of nodes is tried (up to a configurable number of attempts).
- What happens when crossover produces an offspring with disabled connections that leave output nodes unreachable? The offspring is still valid — unreachable nodes are ignored during phenotype evaluation (consistent with Spec 02 behavior). Natural selection will penalize poorly-performing genomes.

## Clarifications

### Session 2026-02-13

- Q: What is the default crossover rate for offspring production (crossover vs mutation-only)? → A: 75% crossover, 25% mutation-only (NEAT paper default).
- Q: What should be the default species representative update strategy? → A: Best-performing member (most stable, most common default).
- Q: Should interspecies crossover be supported? → A: Yes, with configurable rate (default 0.1%, per NEAT paper).
- Q: Which default parent selection method should be used within species? → A: All three (tournament size 2 as default, roulette wheel, SUS) with an injectable `IParentSelector` interface.
- Q: What does "perturbation power" mean for weight mutation? → A: Both distributions supported via config; uniform uses power as max absolute delta, gaussian uses power as standard deviation. Default power = 0.5.

## Requirements *(mandatory)*

### Functional Requirements

#### Mutation

- **FR-001**: System MUST provide a weight perturbation mutation that adjusts existing connection weights by small random amounts drawn from a configurable distribution. Two distributions MUST be supported: uniform (weight += uniform(-power, +power)) and Gaussian (weight += gaussian(0, power)), where "power" is a configurable parameter (default 0.5). Uniform is the default distribution. The distribution type is selectable via configuration.
- **FR-002**: System MUST provide a weight replacement mutation that replaces one or more connection weights with new random values drawn from a configurable weight range.
- **FR-003**: System MUST provide an add-connection mutation that creates a new connection between two previously unconnected nodes, assigns it a random weight, and obtains a deterministic innovation identifier from the innovation tracker. The mutation MUST reject connections that would create cycles in a feed-forward genome.
- **FR-004**: System MUST provide an add-node mutation that splits an existing enabled connection by disabling it and inserting a new hidden node with two new connections (incoming with weight 1.0, outgoing with the original connection's weight). All new structural elements MUST receive deterministic innovation identifiers from the tracker.
- **FR-005**: System MUST provide a toggle-enable mutation that flips the enabled/disabled state of a randomly selected connection.
- **FR-006**: All mutation operators MUST produce new immutable genome instances — the original genome MUST NOT be modified.
- **FR-007**: All mutation operators MUST be deterministic given the same random seed and genome state.
- **FR-008**: Structural mutations (add-node, add-connection) MUST be skipped (returning the original genome unchanged) when the genome has reached the configured maximum node or connection count from `ComplexityLimits`.

#### Crossover

- **FR-009**: System MUST provide a crossover operator that combines two parent genomes into a single offspring genome by aligning connection genes on innovation number.
- **FR-010**: For matching genes (same innovation number in both parents), the crossover operator MUST randomly inherit from either parent with equal probability.
- **FR-011**: For disjoint and excess genes, the crossover operator MUST inherit all such genes from the more-fit parent. When parents have equal fitness, disjoint and excess genes from both parents MUST be included.
- **FR-012**: When a matching gene is disabled in either parent, the offspring gene MUST have a configurable probability of being disabled (default 75%).
- **FR-013**: The crossover operator MUST produce a valid, immutable genome.
- **FR-014**: The crossover operator MUST be deterministic given the same random seed, parent genomes, and fitness values.

#### Speciation

- **FR-015**: System MUST compute a compatibility distance between two genomes using the formula: `d = (c1 * E / N) + (c2 * D / N) + (c3 * W)`, where E = excess gene count, D = disjoint gene count, W = average weight difference of matching genes, N = number of connection genes in the larger genome (minimum 1), and c1/c2/c3 are configurable coefficients.
- **FR-016**: System MUST assign each genome in a population to a species by comparing it to existing species representatives. A genome joins the first species whose representative has compatibility distance below the configurable compatibility threshold; otherwise, a new species is created.
- **FR-017**: System MUST update species representatives each generation by setting the representative to the best-performing member (highest fitness) of the species. Alternative strategies can be achieved by implementing a custom `ISpeciationStrategy`.
- **FR-018**: System MUST track species identity across generations using stable species identifiers. A species retains its identifier as long as it has at least one member.
- **FR-019**: Speciation MUST be deterministic given the same genome ordering, configuration, and representatives.

#### Selection & Reproduction

- **FR-020**: System MUST allocate offspring counts to each species proportionally to the species' average fitness relative to total population average fitness.
- **FR-021**: System MUST support elitism: the champion (highest-fitness member) of each species with membership above a configurable threshold MUST be copied unchanged into the next generation.
- **FR-022**: System MUST apply a stagnation penalty: species that have not improved their best fitness for more than a configurable number of generations MUST receive reduced or zero offspring allocation. At least the top two species by peak fitness MUST be exempt from total elimination to prevent population collapse.
- **FR-023**: System MUST provide a parent selection mechanism within each species via an injectable `IParentSelector` interface. Three built-in implementations MUST be provided: (1) tournament selection (default, configurable tournament size, default 2), (2) fitness-proportional roulette wheel selection, and (3) stochastic universal sampling (SUS). The default registration is tournament selection. Users may inject custom implementations via the DI container.
- **FR-024**: System MUST support configuring the fraction of each species that is eligible for reproduction (survival threshold). Only the top-performing fraction of a species' members are selected as potential parents.
- **FR-025**: Selection and reproduction MUST be deterministic given the same random seed, population state, and configuration.
- **FR-025a**: System MUST support a configurable crossover rate (default 75%) that determines the fraction of offspring produced via crossover of two parents. The remaining offspring (default 25%) are produced by cloning a single parent. All offspring are subsequently mutated according to configured mutation rates.
- **FR-025b**: System MUST support interspecies crossover with a configurable probability (default 0.1%). When interspecies crossover is triggered, the second parent is selected from a different species. When not triggered, both parents are selected from the same species.

#### Complexity Controls

- **FR-026**: System MUST support an optional complexity penalty that reduces a genome's effective fitness based on its structural size (node count, connection count, or both). The penalty coefficient and complexity metric MUST be configurable. The penalty is computed as `adjustedFitness = rawFitness - coefficient * complexityMeasure`.
- **FR-027**: System MUST enforce the hard caps defined in `ComplexityLimits` (from Spec 01) per FR-008.

#### General

- **FR-028**: All evolution operators (mutation, crossover, speciation, selection) MUST produce identical results across runs when given the same random seed and starting state (deterministic reproducibility on CPU).
- **FR-029**: Innovation identifiers assigned during mutation MUST be monotonically increasing within a generation and globally across the run.

### Key Entities

- **Mutation Operator**: A function that takes a genome and random state and produces a new genome with a specific type of modification (weight change, structural addition, enable toggle). Each operator respects complexity limits and uses the innovation tracker for structural changes.
- **Crossover Operator**: A function that takes two parent genomes with their fitness scores and random state, aligns them by innovation number, and produces an offspring genome inheriting genes based on fitness-informed rules.
- **Species**: A group of structurally similar genomes identified by a stable ID and a representative genome. Tracks membership count, best fitness, and stagnation age across generations. Determines reproduction allocation and mating pools.
- **Compatibility Distance**: A numeric measure of structural and parametric difference between two genomes, computed from excess gene count, disjoint gene count, and average weight difference of matching genes, weighted by configurable coefficients.
- **Selection Strategy (`IParentSelector`)**: An injectable interface that determines which members of a species are chosen as parents for reproduction. Three built-in implementations are provided: tournament selection (default, size 2), fitness-proportional roulette wheel, and stochastic universal sampling (SUS). Custom implementations can be registered via the DI container.
- **Reproduction Allocator**: The logic that determines how many offspring each species produces based on relative average fitness, stagnation penalties, and elitism rules.
- **Complexity Penalty**: An optional fitness adjustment that penalizes genomes based on their structural size, discouraging unnecessary complexity growth.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Each mutation operator (weight perturbation, weight replacement, add-connection, add-node, toggle-enable) applied to a known genome with a fixed seed produces the expected structural result in 100% of test cases.
- **SC-002**: Innovation identifiers assigned during any mutation are monotonically increasing — verified across 1,000 mutations in a single run with zero violations.
- **SC-003**: An add-node mutation that splits a connection produces a genome whose phenotype output is functionally equivalent to the original genome's output (within epsilon 1e-6) for the same inputs — verified for 100 random input vectors.
- **SC-004**: Crossover of two known parent genomes with a fixed seed produces an offspring with the expected gene composition — verified for matching, disjoint, and excess gene inheritance in 100% of test cases.
- **SC-005**: Speciation assigns identical genomes to the same species and structurally different genomes (compatibility distance above threshold) to different species — verified for 100% of test genome pairs.
- **SC-006**: Two complete runs of all evolution operators (mutation, crossover, speciation, selection) with the same seed, configuration, and starting population produce byte-identical populations at every generation.
- **SC-007**: Elitism preserves species champions unchanged — the champion genome in generation N+1 is structurally identical to the champion in generation N for every qualifying species, verified over 50 generations.
- **SC-008**: Species that stagnate beyond the configured threshold receive zero or reduced offspring allocation in 100% of test cases, while the top two species by peak fitness are never completely eliminated.

## Assumptions

- **Random number generation**: All stochastic operations (mutation probabilities, weight sampling, parent selection, gene inheritance) use a seeded random number generator to ensure deterministic reproducibility. The specific RNG implementation is an implementation detail but must support seed-based determinism.
- **Default mutation rates**: Sensible defaults are provided for all mutation probabilities (e.g., weight perturbation ~80%, add-connection ~5%, add-node ~3%, toggle-enable ~1%) based on published NEAT literature. All rates are configurable.
- **Compatibility distance coefficients**: Default values of c1=1.0, c2=1.0, c3=0.4 are used, consistent with the original NEAT paper. All coefficients are configurable.
- **Compatibility threshold**: Default value of 3.0 is used, consistent with the original NEAT paper. The threshold is configurable.
- **Elitism threshold**: By default, species with 5 or more members have their champion preserved. The threshold is configurable.
- **Stagnation threshold**: By default, species that have not improved for 15 generations are penalized. The threshold is configurable.
- **Survival threshold**: By default, only the top 20% of a species' members are eligible for reproduction. The fraction is configurable.
- **Default crossover rate**: 75% of offspring are produced via crossover of two parents; 25% are produced by cloning a single parent and mutating. The crossover rate is configurable.
- **Default interspecies crossover rate**: 0.1% probability that a crossover selects the second parent from a different species, consistent with the original NEAT paper. The rate is configurable.
- **Species representative default**: The best-performing member of the species becomes the representative each generation. The strategy is configurable.
- **Default parent selection**: Tournament selection with tournament size 2 is the default. Roulette wheel and stochastic universal sampling are also provided. Selection strategy is injectable via `IParentSelector`.
- **Feed-forward only**: Cycle detection in add-connection mutation applies to feed-forward mode only. Recurrent network support is out of scope.
- **Single-threaded execution**: All operators assume single-threaded execution within a generation. Thread safety is not required for this spec.
- **Disabled gene inheritance probability**: Default 75% chance a matching gene is disabled if either parent has it disabled, per the original NEAT paper.
- **Weight perturbation distribution**: Both uniform and Gaussian distributions are supported. For uniform: weight is adjusted by a value drawn from `[-power, +power]`. For Gaussian: weight is adjusted by a value drawn from `N(0, power)`. Default distribution is uniform, default power is 0.5. The distribution type is selectable via configuration.

## Non-Goals

- Checkpointing, serialization, or persistence of evolution state to disk (Spec 05).
- GPU/CUDA-accelerated evaluation or mutation (Spec 06).
- Advanced NEAT variants (HyperNEAT, CPPN, ES-HyperNEAT).
- Novelty search, MAP-Elites, or other quality-diversity algorithms.
- Recurrent network topology support.
- Multi-threaded or concurrent operator execution.
- Dynamic compatibility threshold adjustment (can be added as a future enhancement).
- Adaptive mutation rates (can be added as a future enhancement).
