# Tasks: Evolution Operators (Mutation/Crossover) + Speciation + Selection

**Input**: Design documents from `/specs/003-evolution-operators/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Required — constitution mandates TDD (Red-Green-Refactor) with fixed-seed deterministic tests for all stochastic operations.

**Organization**: Tasks grouped by user story. Each story is independently implementable and testable after Phase 2 completes.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create new configuration types and extend existing options for evolution operators.

- [x] T001 [P] Create WeightDistributionType enum (Uniform, Gaussian) in src/NeatSharp/Configuration/WeightDistributionType.cs
- [x] T002 [P] Create ComplexityPenaltyMetric enum (NodeCount, ConnectionCount, Both) in src/NeatSharp/Configuration/ComplexityPenaltyMetric.cs
- [x] T003 [P] Create MutationOptions class with NEAT-paper defaults (WeightPerturbationRate=0.8, WeightReplacementRate=0.1, AddConnectionRate=0.05, AddNodeRate=0.03, ToggleEnableRate=0.01, PerturbationPower=0.5, PerturbationDistribution=Uniform, WeightMinValue=-4.0, WeightMaxValue=4.0, MaxAddConnectionAttempts=20) and validation attributes per data-model.md in src/NeatSharp/Configuration/MutationOptions.cs
- [x] T004 [P] Create CrossoverOptions class with NEAT-paper defaults (CrossoverRate=0.75, InterspeciesCrossoverRate=0.001, DisabledGeneInheritanceProbability=0.75) and validation attributes per data-model.md in src/NeatSharp/Configuration/CrossoverOptions.cs
- [x] T005 [P] Create SpeciationOptions class with NEAT-paper defaults (ExcessCoefficient=1.0, DisjointCoefficient=1.0, WeightDifferenceCoefficient=0.4, CompatibilityThreshold=3.0) and validation attributes per data-model.md in src/NeatSharp/Configuration/SpeciationOptions.cs
- [x] T006 [P] Create SelectionOptions class with defaults (ElitismThreshold=5, StagnationThreshold=15, SurvivalThreshold=0.2, TournamentSize=2) and validation attributes per data-model.md in src/NeatSharp/Configuration/SelectionOptions.cs
- [x] T007 [P] Create ComplexityPenaltyOptions class with defaults (Coefficient=0.0 disabled, Metric=Both) per data-model.md in src/NeatSharp/Configuration/ComplexityPenaltyOptions.cs
- [x] T008 Extend NeatSharpOptions with Mutation, Crossover, Speciation, Selection, and ComplexityPenalty nested properties in src/NeatSharp/Configuration/NeatSharpOptions.cs
- [x] T009 Update NeatSharpOptionsValidator with cross-field validation for all new configuration types (rates in [0,1], PerturbationPower>0, WeightMin<WeightMax, MaxAddConnectionAttempts>=1, all coefficients>=0, CompatibilityThreshold>0, ElitismThreshold>=1, StagnationThreshold>=1, SurvivalThreshold in (0,1], TournamentSize>=1) in src/NeatSharp/Extensions/ServiceCollectionExtensions.cs
- [x] T010 Add tests for new configuration defaults and validation rules (verify NEAT-paper defaults, valid/invalid option combinations, validator rejection messages) in tests/NeatSharp.Tests/Configuration/NeatSharpOptionsTests.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Create interfaces and shared types that all user stories depend on.

**CRITICAL**: No user story work can begin until this phase is complete.

- [x] T011 [P] Create IMutationOperator interface with `Genome Mutate(Genome genome, Random random, IInnovationTracker tracker)` method per contracts/mutation.md in src/NeatSharp/Evolution/Mutation/IMutationOperator.cs
- [x] T012 [P] Create ICrossoverOperator interface with `Genome Cross(Genome parent1, double parent1Fitness, Genome parent2, double parent2Fitness, Random random)` method per contracts/crossover.md in src/NeatSharp/Evolution/Crossover/ICrossoverOperator.cs
- [x] T013 [P] Create ICompatibilityDistance interface with `double Compute(Genome genome1, Genome genome2)` method per contracts/speciation.md in src/NeatSharp/Evolution/Speciation/ICompatibilityDistance.cs
- [x] T014 [P] Create ISpeciationStrategy interface with `void Speciate(IReadOnlyList<(Genome Genome, double Fitness)> population, List<Species> species)` method per contracts/speciation.md in src/NeatSharp/Evolution/Speciation/ISpeciationStrategy.cs
- [x] T015 [P] Create IParentSelector interface with `Genome Select(IReadOnlyList<(Genome Genome, double Fitness)> candidates, Random random)` method per contracts/selection.md in src/NeatSharp/Evolution/Selection/IParentSelector.cs
- [x] T016 [P] Create Species class with Id, Representative, Members list of (Genome, double Fitness) tuples, BestFitnessEver, GenerationsSinceImprovement, computed AverageFitness property, and constructor(int id, Genome representative) per contracts/speciation.md in src/NeatSharp/Evolution/Speciation/Species.cs

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 — Mutate a Genome (Priority: P1) MVP

**Goal**: Apply five mutation operators to produce structurally or parametrically modified genomes while respecting complexity limits and innovation tracking. All mutations produce new immutable genomes and are deterministic with fixed seeds.

**Independent Test**: Construct minimal genomes, apply each mutation type individually with a fixed random seed, verify the resulting genome has the expected structural change with correct innovation identifiers.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T017 [P] [US1] Write WeightPerturbationMutation tests: verify uniform delta in [-power,+power] and Gaussian delta ~N(0,power), weight clamping to [WeightMinValue,WeightMaxValue], genome structure unchanged, original genome not modified, determinism with fixed seed in tests/NeatSharp.Tests/Evolution/Mutation/WeightPerturbationMutationTests.cs
- [x] T018 [P] [US1] Write WeightReplacementMutation tests: verify one connection weight replaced within [WeightMinValue,WeightMaxValue], genome with 0 connections returned unchanged, original genome not modified, determinism in tests/NeatSharp.Tests/Evolution/Mutation/WeightReplacementMutationTests.cs
- [x] T019 [P] [US1] Write AddConnectionMutation tests: verify new connection between unconnected nodes with innovation ID from tracker, cycle-creating connections rejected, complexity limit MaxConnections skip, fully-connected genome skip, target node not input/bias, max attempts exhaustion returns unchanged genome, determinism in tests/NeatSharp.Tests/Evolution/Mutation/AddConnectionMutationTests.cs
- [x] T020 [P] [US1] Write AddNodeMutation tests: verify enabled connection disabled + new hidden node + connection source-to-new weight=1.0 + connection new-to-target weight=original, innovation IDs from tracker.GetNodeSplitInnovation(), complexity limit MaxNodes skip, genome with no enabled connections returned unchanged, determinism, phenotype equivalence (SC-003: build phenotype via INetworkBuilder for original and mutated genomes, activate with 100 random input vectors using fixed seed, assert outputs match within 1e-6 epsilon) in tests/NeatSharp.Tests/Evolution/Mutation/AddNodeMutationTests.cs
- [x] T021 [P] [US1] Write ToggleEnableMutation tests: verify enabled connection becomes disabled and disabled becomes enabled, genome with 0 connections returned unchanged, original genome not modified, determinism in tests/NeatSharp.Tests/Evolution/Mutation/ToggleEnableMutationTests.cs
- [x] T022 [P] [US1] Write CompositeMutationOperator tests: verify mutation application order (weight perturbation/replacement mutually exclusive first, then structural add-node/add-connection independently, then toggle-enable), rate-based probabilistic triggering, multiple mutations can apply in one call, determinism with fixed seed in tests/NeatSharp.Tests/Evolution/Mutation/CompositeMutationOperatorTests.cs

### Implementation for User Story 1

- [x] T023 [P] [US1] Implement WeightPerturbationMutation: for each connection, adjust weight by uniform or Gaussian delta based on PerturbationDistribution config, clamp to [WeightMinValue,WeightMaxValue], return new immutable genome with constructor dependency IOptions<NeatSharpOptions> in src/NeatSharp/Evolution/Mutation/WeightPerturbationMutation.cs
- [x] T024 [P] [US1] Implement WeightReplacementMutation: select one random connection, replace weight with random.NextDouble() * (max-min) + min, return new immutable genome with constructor dependency IOptions<NeatSharpOptions> in src/NeatSharp/Evolution/Mutation/WeightReplacementMutation.cs
- [x] T025 [P] [US1] Implement AddConnectionMutation: randomly select node pairs up to MaxAddConnectionAttempts, reject if already connected or would create cycle (DFS from target checking if source is reachable via enabled connections), skip if at MaxConnections limit, target must not be input/bias, get innovation from tracker.GetConnectionInnovation(), weight from random in [WeightMinValue,WeightMaxValue], with constructor dependency IOptions<NeatSharpOptions> in src/NeatSharp/Evolution/Mutation/AddConnectionMutation.cs
- [x] T026 [P] [US1] Implement AddNodeMutation: select random enabled connection, disable it, create new hidden node (identity activation for phenotype equivalence) via tracker.GetNodeSplitInnovation(), create connection source-to-new weight=1.0 and new-to-target weight=original weight, skip if at MaxNodes limit or no enabled connections, with constructor dependency IOptions<NeatSharpOptions> in src/NeatSharp/Evolution/Mutation/AddNodeMutation.cs
- [x] T027 [P] [US1] Implement ToggleEnableMutation: select random connection, flip IsEnabled, return new immutable genome; if 0 connections return unchanged in src/NeatSharp/Evolution/Mutation/ToggleEnableMutation.cs
- [x] T028 [US1] Implement CompositeMutationOperator: orchestrate mutations in fixed order — roll once for weight perturbation vs replacement (mutually exclusive), roll independently for add-node and add-connection, roll for toggle-enable; apply matching mutations sequentially to genome; constructor takes IOptions<NeatSharpOptions> and explicit concrete mutation operator types (WeightPerturbationMutation, WeightReplacementMutation, AddConnectionMutation, AddNodeMutation, ToggleEnableMutation) in src/NeatSharp/Evolution/Mutation/CompositeMutationOperator.cs

**Checkpoint**: All 5 mutation operators and composite orchestrator are functional and tested. Each mutation produces immutable genomes with correct innovation IDs. Deterministic with fixed seeds.

---

## Phase 4: User Story 2 — Cross Two Genomes (Priority: P2)

**Goal**: Combine two parent genomes via NEAT innovation-number-aligned crossover to produce a valid immutable offspring genome with correct gene composition based on parent fitness.

**Independent Test**: Construct two parent genomes with known overlapping and non-overlapping innovation numbers, specify which parent is fitter, cross with fixed seed, verify offspring has matching genes from either parent, disjoint/excess from fitter parent.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T029 [US2] Write NeatCrossover tests: verify matching genes randomly inherited 50/50, disjoint/excess from fitter parent only, equal fitness includes disjoint/excess from both parents, disabled gene in either parent gives 75% disabled in offspring, offspring node list includes all nodes referenced by inherited connections plus all input/output/bias from fitter parent, offspring is immutable, determinism with fixed seed, self-crossover produces clone in tests/NeatSharp.Tests/Evolution/Crossover/NeatCrossoverTests.cs

### Implementation for User Story 2

- [x] T030 [US2] Implement NeatCrossover: two-pointer merge on connections sorted by innovation number, classify matching/disjoint/excess genes, inherit matching from random parent (50/50), inherit disjoint/excess from fitter parent (both if equal fitness), apply DisabledGeneInheritanceProbability for matching genes disabled in either parent, collect nodes from inherited connections + all input/output/bias from fitter parent, constructor dependency IOptions<NeatSharpOptions> in src/NeatSharp/Evolution/Crossover/NeatCrossover.cs

**Checkpoint**: Crossover produces valid immutable offspring with correct gene composition from two parents. Deterministic with fixed seeds.

---

## Phase 5: User Story 3 — Assign Genomes to Species (Priority: P3)

**Goal**: Group genomes by structural similarity using compatibility distance metric, maintaining stable species identifiers and tracking stagnation across generations.

**Independent Test**: Construct genomes with known structural differences, run speciation with fixed threshold, verify similar genomes grouped together and different genomes in separate species.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T031 [P] [US3] Write CompatibilityDistance tests: verify formula d=(c1*E/N)+(c2*D/N)+(c3*W), identical genomes return 0.0, genomes with only disjoint genes count correctly, genomes with only excess genes count correctly, average weight difference of matching genes computed correctly, N=max(connection count of larger genome, 1), configurable c1/c2/c3 coefficients in tests/NeatSharp.Tests/Evolution/Speciation/CompatibilityDistanceTests.cs
- [x] T032 [P] [US3] Write CompatibilitySpeciation tests: verify first genome creates new species, genome within threshold joins existing species (first match), genome above threshold creates new species, empty species removed after assignment, representative updated to best-performing member, BestFitnessEver and GenerationsSinceImprovement tracked correctly, stagnation counter incremented when no improvement and reset on improvement, determinism with same genome ordering in tests/NeatSharp.Tests/Evolution/Speciation/CompatibilitySpeciationTests.cs

### Implementation for User Story 3

- [x] T033 [US3] Implement CompatibilityDistance: two-pointer merge on connections sorted by innovation number, count excess (beyond other genome's max innovation), disjoint (non-matching within range), compute average |weight difference| of matching genes, apply formula d=(c1*E/N)+(c2*D/N)+(c3*W) with N=max(larger connection count, 1), constructor dependency IOptions<NeatSharpOptions> in src/NeatSharp/Evolution/Speciation/CompatibilityDistance.cs
- [x] T034 [US3] Implement CompatibilitySpeciation: clear species members but keep metadata, assign each genome to first species with distance < CompatibilityThreshold or create new species, remove empty species, update representative to best member (highest fitness), update BestFitnessEver and stagnation counter, constructor dependencies IOptions<NeatSharpOptions> and ICompatibilityDistance in src/NeatSharp/Evolution/Speciation/CompatibilitySpeciation.cs

**Checkpoint**: Genomes correctly grouped by structural similarity with stable species identifiers. Stagnation tracking functional. Deterministic with same genome ordering.

---

## Phase 6: User Story 4 — Select Parents and Apply Elitism (Priority: P4)

**Goal**: Select parent genomes via injectable strategies, allocate offspring proportionally to species fitness, preserve champions via elitism, penalize stagnant species while protecting top two.

**Independent Test**: Construct species with known fitness scores, run selection and allocation, verify proportional offspring counts, champion preservation, stagnation penalties, and top-2 protection.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T035 [P] [US4] Write TournamentSelector tests: verify picks TournamentSize random candidates and returns highest-fitness winner, single-candidate pool returns that candidate, determinism with fixed seed in tests/NeatSharp.Tests/Evolution/Selection/TournamentSelectorTests.cs
- [x] T036 [P] [US4] Write RouletteWheelSelector tests: verify fitness-proportional selection probability (higher fitness selected more often over many runs), negative/zero fitness shifted by |min|+epsilon, single-candidate returns that candidate, determinism with fixed seed in tests/NeatSharp.Tests/Evolution/Selection/RouletteWheelSelectorTests.cs
- [x] T037 [P] [US4] Write StochasticUniversalSamplingSelector tests: verify more uniform distribution than roulette over batch selections, fitness-proportional spacing, single-candidate returns that candidate, determinism with fixed seed in tests/NeatSharp.Tests/Evolution/Selection/StochasticUniversalSamplingSelectorTests.cs
- [x] T038 [P] [US4] Write ReproductionAllocator tests: verify proportional offspring allocation (species with avg fitness 10,5,2.5 get ~57,29,14 of 100), stagnant species (> StagnationThreshold generations without improvement) get 0 offspring, top 2 species by BestFitnessEver never fully eliminated even if stagnant, elitism reserves 1 slot for champion in species >= ElitismThreshold members, total offspring exactly equals PopulationSize after rounding adjustment in tests/NeatSharp.Tests/Evolution/Selection/ReproductionAllocatorTests.cs

### Implementation for User Story 4

- [x] T039 [P] [US4] Implement TournamentSelector: pick TournamentSize random candidates (with replacement), return highest fitness, constructor dependency IOptions<NeatSharpOptions> in src/NeatSharp/Evolution/Selection/TournamentSelector.cs
- [x] T040 [P] [US4] Implement RouletteWheelSelector: compute total fitness (shift all by |min|+epsilon if any <=0), roll random in [0,total), walk candidates accumulating fitness until exceeded, return current candidate in src/NeatSharp/Evolution/Selection/RouletteWheelSelector.cs
- [x] T041 [P] [US4] Implement StochasticUniversalSamplingSelector: compute total fitness (shift if needed), single random start in [0,spacing), evenly spaced pointer at totalFitness/1 for single selection, walk candidates to find pointer position in src/NeatSharp/Evolution/Selection/StochasticUniversalSamplingSelector.cs
- [x] T042 [US4] Implement ReproductionAllocator: identify stagnant species, protect top 2 by BestFitnessEver, set stagnant (non-protected) adjusted fitness to 0, compute proportional offspring = round(adjustedAvg/totalAdjusted * populationSize), reserve 1 for champion in species >= ElitismThreshold, adjust largest-remainder species to ensure total = PopulationSize, constructor dependency IOptions<NeatSharpOptions> in src/NeatSharp/Evolution/Selection/ReproductionAllocator.cs

**Checkpoint**: Parent selection and reproduction allocation produce correct, deterministic results with elitism and stagnation handling. Total offspring always equals PopulationSize.

---

## Phase 7: User Story 5 — Control Complexity Growth (Priority: P5)

**Goal**: Prevent unbounded network growth via optional complexity penalty that reduces effective fitness based on genome structural size.

**Independent Test**: Configure complexity penalty coefficient, verify adjusted fitness equals rawFitness - coefficient * complexityMeasure for various metrics (NodeCount, ConnectionCount, Both).

> **NOTE**: Hard caps (MaxNodes/MaxConnections) are already enforced in US1's AddConnectionMutation (T025) and AddNodeMutation (T026) per FR-008/FR-027. This phase adds the soft complexity penalty only.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T043 [US5] Write complexity penalty tests: verify fitness reduced by coefficient * nodeCount when Metric=NodeCount, coefficient * connectionCount when Metric=ConnectionCount, coefficient * (nodeCount+connectionCount) when Metric=Both, coefficient=0.0 returns original fitness unchanged (penalty disabled by default), negative adjusted fitness is allowed (not clamped) in tests/NeatSharp.Tests/Evolution/Selection/ComplexityPenaltyTests.cs

### Implementation for User Story 5

- [x] T044 [US5] Extend ReproductionAllocator with complexity penalty fitness adjustment: before computing species average fitness, reduce each genome's effective fitness by ComplexityPenaltyOptions.Coefficient * complexityMeasure (based on Metric enum: NodeCount, ConnectionCount, or Both=sum), coefficient=0.0 skips penalty; integrate into AllocateOffspring flow per FR-026 in src/NeatSharp/Evolution/Selection/ReproductionAllocator.cs

**Checkpoint**: Complexity penalty reduces effective fitness proportionally to genome size when enabled. Default coefficient=0.0 means no penalty (backwards-compatible).

---

## Phase 7b: Reproduction Orchestration (Cross-Cutting)

**Purpose**: Implement the per-offspring reproduction loop that applies crossover rate (FR-025a), interspecies crossover (FR-025b), survival threshold filtering (FR-024), and mutation to produce the next generation. This orchestrator is called by the evolution engine but is testable in isolation.

**Dependencies**: Phases 3-7 (all operators, speciation, selection, and complexity penalty must exist).

### Tests

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T049 [US4] Write ReproductionOrchestrator tests: verify survival threshold filters species members to top SurvivalThreshold fraction before parent selection, crossover vs clone decision respects CrossoverRate (75% crossover / 25% clone over many offspring with fixed seed), interspecies crossover triggers at InterspeciesCrossoverRate (second parent from different species), all offspring are mutated via CompositeMutationOperator, elite champions are NOT mutated, total offspring equals PopulationSize, determinism with fixed seed in tests/NeatSharp.Tests/Evolution/Selection/ReproductionOrchestratorTests.cs

### Implementation

- [x] T050 [US4] Implement ReproductionOrchestrator: for each species with allocated offspring, copy champion unchanged if species >= ElitismThreshold, for remaining slots filter members to top SurvivalThreshold fraction, roll crossover vs clone (random < CrossoverRate), if crossover roll interspecies (random < InterspeciesCrossoverRate) to pick second parent from different species else same species, select parents via IParentSelector, produce offspring via ICrossoverOperator or clone, apply CompositeMutationOperator to all non-elite offspring, constructor dependencies IOptions<NeatSharpOptions>/IParentSelector/ICrossoverOperator/CompositeMutationOperator/ReproductionAllocator in src/NeatSharp/Evolution/Selection/ReproductionOrchestrator.cs

**Checkpoint**: Full reproduction loop produces correct next-generation populations. Crossover rate, interspecies crossover, survival threshold, elitism, and mutation all integrated and deterministic.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Wire up DI registrations, validate end-to-end, ensure all tests pass.

- [x] T045 Update ServiceCollectionExtensions.AddNeatSharp() to register all new evolution services: each mutation operator as both its concrete type and as IMutationOperator (singleton) — WeightPerturbationMutation, WeightReplacementMutation, AddConnectionMutation, AddNodeMutation, ToggleEnableMutation — so CompositeMutationOperator can resolve concrete types and consumers can resolve IEnumerable<IMutationOperator>; NeatCrossover as ICrossoverOperator (singleton), CompatibilityDistance as ICompatibilityDistance (singleton), CompatibilitySpeciation as ISpeciationStrategy (singleton), TournamentSelector as IParentSelector (singleton, default), CompositeMutationOperator (singleton), ReproductionAllocator (singleton), ReproductionOrchestrator (singleton) in src/NeatSharp/Extensions/ServiceCollectionExtensions.cs
- [x] T046 Update DI registration tests to verify all new evolution services resolve correctly from the container, verify default IParentSelector is TournamentSelector, verify custom IParentSelector can be overridden in tests/NeatSharp.Tests/Extensions/ServiceCollectionExtensionsTests.cs
- [x] T047 Run full test suite across net8.0 and net9.0 targets and verify all tests pass with zero failures
- [x] T048 Validate quickstart.md code samples are consistent with implemented API signatures and DI registrations

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup (T001-T010) — BLOCKS all user stories
- **User Stories (Phases 3-7)**: All depend on Foundational phase (T011-T016) completion
  - US1-US4 can proceed in parallel (if staffed)
  - Or sequentially in priority order (US1 → US2 → US3 → US4 → US5)
- **US5 (Phase 7)**: Additionally depends on US4 (T042) since complexity penalty extends ReproductionAllocator
- **Reproduction Orchestration (Phase 7b)**: Depends on Phases 3-7 (all operators, speciation, selection, and complexity penalty)
- **Polish (Phase 8)**: Depends on all user stories (Phases 3-7) and Phase 7b being complete

### User Story Dependencies

- **US1 (Mutation)**: Phase 2 only — no dependency on other stories
- **US2 (Crossover)**: Phase 2 only — no dependency on other stories
- **US3 (Speciation)**: Phase 2 only — no dependency on other stories
- **US4 (Selection)**: Phase 2 only — no dependency on other stories (uses Species from Phase 2)
- **US5 (Complexity)**: Phase 2 + US4 T042 — penalty integrates into ReproductionAllocator
- **Reproduction Orchestration**: Phases 3-7 — uses all operators, speciation, selection, and complexity penalty

### Within Each User Story

- Tests MUST be written and FAIL before implementation (TDD Red-Green-Refactor)
- Individual operators/classes before composite/orchestrator
- Core implementation before integration

### Parallel Opportunities

- All Setup tasks T001-T007 can run in parallel
- All Foundational tasks T011-T016 can run in parallel
- All US1 test tasks T017-T022 can run in parallel
- All US1 individual operator implementations T023-T027 can run in parallel (T028 depends on all)
- US1 through US4 can run in parallel after Phase 2 completes
- All US3 test tasks T031-T032 can run in parallel
- All US4 test tasks T035-T038 can run in parallel
- All US4 selector implementations T039-T041 can run in parallel (T042 follows)

---

## Parallel Example: User Story 1

```bash
# Launch all mutation tests together (TDD: write first, verify they fail):
Task: "WeightPerturbationMutation tests in tests/.../WeightPerturbationMutationTests.cs"
Task: "WeightReplacementMutation tests in tests/.../WeightReplacementMutationTests.cs"
Task: "AddConnectionMutation tests in tests/.../AddConnectionMutationTests.cs"
Task: "AddNodeMutation tests in tests/.../AddNodeMutationTests.cs"
Task: "ToggleEnableMutation tests in tests/.../ToggleEnableMutationTests.cs"
Task: "CompositeMutationOperator tests in tests/.../CompositeMutationOperatorTests.cs"

# Then launch all individual operator implementations together:
Task: "WeightPerturbationMutation in src/.../WeightPerturbationMutation.cs"
Task: "WeightReplacementMutation in src/.../WeightReplacementMutation.cs"
Task: "AddConnectionMutation in src/.../AddConnectionMutation.cs"
Task: "AddNodeMutation in src/.../AddNodeMutation.cs"
Task: "ToggleEnableMutation in src/.../ToggleEnableMutation.cs"

# Then CompositeMutationOperator (depends on individual operators):
Task: "CompositeMutationOperator in src/.../CompositeMutationOperator.cs"
```

## Parallel Example: User Story 4

```bash
# Launch all selection tests together:
Task: "TournamentSelector tests in tests/.../TournamentSelectorTests.cs"
Task: "RouletteWheelSelector tests in tests/.../RouletteWheelSelectorTests.cs"
Task: "SUS selector tests in tests/.../StochasticUniversalSamplingSelectorTests.cs"
Task: "ReproductionAllocator tests in tests/.../ReproductionAllocatorTests.cs"

# Then launch all selector implementations together:
Task: "TournamentSelector in src/.../TournamentSelector.cs"
Task: "RouletteWheelSelector in src/.../RouletteWheelSelector.cs"
Task: "SUS selector in src/.../StochasticUniversalSamplingSelector.cs"

# Then ReproductionAllocator (uses Species, integrates allocation logic):
Task: "ReproductionAllocator in src/.../ReproductionAllocator.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (configuration types)
2. Complete Phase 2: Foundational (interfaces + Species)
3. Complete Phase 3: User Story 1 (all 5 mutation operators + composite)
4. **STOP and VALIDATE**: Run all mutation tests, verify determinism with fixed seeds
5. Mutations alone enable population variation — core evolutionary mechanic

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 (Mutation) → Test independently → Genetic variation works (MVP!)
3. US2 (Crossover) → Test independently → Gene recombination works
4. US3 (Speciation) → Test independently → Population grouping works
5. US4 (Selection) → Test independently → Evolutionary pressure works
6. US5 (Complexity) → Test independently → Growth control works
7. Polish → DI wiring + full integration validation

### Parallel Team Strategy

With multiple developers after Phase 2 completes:
- Developer A: US1 (Mutation) — 12 tasks
- Developer B: US2 (Crossover) + US3 (Speciation) — 6 tasks
- Developer C: US4 (Selection) — 8 tasks
- US5 (Complexity) — 2 tasks, assigned after US4 completes
