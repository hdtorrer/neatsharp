<!--
=== Sync Impact Report ===
Version change: 1.1.0 -> 1.2.0
Modified principles: None (I-V unchanged)
Added sections:
  - Core Principle VI: SOLID Design
  - Core Principle VII: Test-Driven Development
Modified sections:
  - Development Workflow > Definition of Done — added item 6
    (TDD cycle compliance) to align with new Principle VII.
Removed sections: None
Templates requiring updates:
  - .specify/templates/plan-template.md — Constitution Check now
    has two additional gates (SOLID, TDD). Template is generic;
    gates are filled at plan time. ✅ No update needed.
  - .specify/templates/spec-template.md — No constitution-specific
    references. ✅ Compatible.
  - .specify/templates/tasks-template.md — Already contains
    "Tests MUST be written and FAIL before implementation" in
    Phase 3+ and "Within Each User Story" sections. Consistent
    with new Principle VII. ✅ Compatible.
  - .specify/templates/commands/*.md — No command files exist.
    ✅ N/A.
  - README.md — Minimal stub; no constitution references.
    ✅ Compatible.
Follow-up TODOs: None.
=== End Sync Impact Report ===
-->

# NeatSharp Constitution

## Core Principles

### I. Correctness

The library MUST implement canonical NEAT behavior as described in
Kenneth Stanley's original work. This includes:

- Global innovation numbers for structural mutations.
- Speciation via genomic distance with historical markings.
- Proper crossover respecting gene alignment by innovation number.
- Protection of novel topologies through fitness sharing.

**Rationale**: Incorrect neuroevolution produces silently wrong results.
Users MUST be able to trust that the algorithm behaves as documented
in the NEAT literature.

### II. Reproducibility

Given the same machine, the same seed, and the same configuration,
a CPU run MUST produce identical results across invocations.

- Every stochastic operation MUST consume randomness from a
  single seeded PRNG in a deterministic order.
- GPU runs SHOULD produce results within an epsilon tolerance
  of the CPU baseline; bitwise equality is NOT required.
- Reproducibility regressions are treated as bugs.

**Rationale**: Reproducibility is essential for debugging, research
comparisons, and user trust. GPU floating-point non-determinism is
an accepted physical constraint, not a design choice.

### III. Performance

GPU acceleration is a first-class capability, not an afterthought.

- A CPU fallback MUST always exist and MUST be correct.
- GPU kernels MUST be benchmarked against the CPU path on a
  reference problem before merging.
- Performance-sensitive changes MUST include benchmark evidence.
- Numerical equivalence between CPU and GPU paths is validated
  with epsilon-based tolerances.

**Rationale**: The primary value proposition of this library is
making GPU-accelerated NEAT accessible to .NET developers.
Performance without correctness is worthless; correctness without
performance is already available elsewhere.

### IV. Developer Experience

The API MUST provide a simple "golden path" with sensible defaults.

- A new user MUST be able to run a complete NEAT evolution with
  fewer than 20 lines of configuration + fitness function.
- Advanced tuning knobs (speciation thresholds, mutation rates,
  selection strategies) MUST be available but MUST NOT be required.
- Error messages MUST be actionable and reference the
  misconfigured parameter by name.

**Rationale**: The target audience includes hobbyists and game
developers, not only researchers. Approachability drives adoption.

### V. Minimal Dependencies & Cross-Platform

The library MUST minimize external dependencies and MUST build
and run on both Windows and Linux.

- The only required runtime dependency beyond the .NET SDK is the
  CUDA toolkit (when GPU features are used).
- CPU-only usage MUST NOT require CUDA to be installed.
- Native interop layers MUST be isolated behind platform-specific
  build targets; user code MUST NOT reference native symbols
  directly.

**Rationale**: Dependency sprawl increases maintenance burden and
breaks downstream consumers. Cross-platform support doubles the
addressable user base at modest cost.

### VI. SOLID Design

All production code MUST adhere to the SOLID principles:

- **Single Responsibility**: Every class MUST have one reason to
  change. A class that handles both genome mutation logic and
  serialization MUST be split.
- **Open/Closed**: Types MUST be open for extension and closed
  for modification. Fitness evaluators, selection strategies, and
  activation functions MUST be swappable via interfaces or
  delegates without modifying existing code.
- **Liskov Substitution**: Any implementation of an interface or
  base class MUST be substitutable for its parent without altering
  correctness. Narrowing preconditions or widening postconditions
  in a subtype is forbidden.
- **Interface Segregation**: Interfaces MUST NOT force consumers
  to depend on methods they do not use. A single `IEvolutionEngine`
  that bundles evaluation, speciation, and serialization MUST be
  decomposed into focused interfaces.
- **Dependency Inversion**: High-level modules (evolution loop,
  speciation) MUST NOT depend on low-level modules (CUDA kernels,
  file I/O) directly. Both MUST depend on abstractions. Concrete
  implementations MUST be injected, not constructed internally.

**Rationale**: SOLID prevents the codebase from calcifying as it
grows. For a library with multiple execution backends (CPU, GPU)
and user-extensible components (fitness functions, activation
functions), violation of these principles produces rigid code that
resists extension and defeats the library's design goals.

### VII. Test-Driven Development

All new production code MUST be developed using the Red-Green-Refactor
cycle:

1. **Red**: Write a failing test that defines the expected behavior
   before writing any production code. The test MUST compile and
   MUST fail with a meaningful assertion message.
2. **Green**: Write the minimum production code necessary to make
   the failing test pass. No speculative code beyond what the test
   demands.
3. **Refactor**: Clean up both test and production code while all
   tests remain green. Refactoring MUST NOT change observable
   behavior.

Additional mandates:

- Tests MUST be committed in the same PR as the production code
  they cover; PRs that add untested production logic MUST be
  rejected.
- Bug fixes MUST begin with a regression test that reproduces
  the bug before the fix is applied.
- Test coverage MUST NOT be used as a sole quality metric;
  the focus is on testing meaningful behavior and edge cases,
  not hitting a coverage number.
- Characterization tests (tests written after the fact to capture
  existing behavior) are permitted only for pre-existing untested
  code; new code MUST follow TDD.

**Rationale**: TDD produces code that is testable by construction,
catches regressions immediately, and forces interface design to
precede implementation. For a correctness-critical library like
NeatSharp, untested code is untrustworthy code.

## Scope & Trade-offs

### Vision

Deliver an ergonomic NEAT baseline for .NET users with clear GPU
acceleration value. Primary users are .NET developers and
simulation, game, and optimization hobbyists. Research-style
reproducibility is supported where feasible.

### v1 Success Criteria

1. Clean, minimal public API with documentation and examples.
2. Reproducible seeded runs (CPU deterministic, GPU best-effort).
3. Measurable GPU speedup demonstrated on a reference benchmark.

### v1 Trade-offs Accepted

- Fewer features to keep quality high.
- Slightly less configurability to keep the API approachable.
- GPU numerical equivalence is epsilon-based; exact bitwise match
  is not required.

### Explicitly Out of Scope (until after v1.0)

- Distributed or multi-node training.
- Advanced NEAT variants (HyperNEAT, CoDeepNEAT, ES-HyperNEAT).
- Visual UI tooling.

### Release & Compatibility Policy

- **Pre-v1.0**: Breaking changes are allowed with notice.
- **Post-v1.0**: Breaking changes occur only in major versions.
- Serialization is versioned; breaking schema changes MUST include
  migration guidance.

## Development Workflow

### Definition of Done (every PR)

A PR is mergeable when ALL of the following are satisfied:

1. The project builds on both Windows and Linux.
2. All automated tests pass; new logic includes tests.
3. Public API changes include documentation updates.
4. Performance-sensitive changes include benchmark evidence.
5. The PR description contains a "Spec impact" section
   referencing the relevant spec item ID.
6. New production code follows the TDD Red-Green-Refactor cycle
   (Principle VII); reviewers MUST verify test-first commit
   ordering or author attestation.

### LLM-First Workflow

LLMs MAY author PRs and tests. Humans approve all merges.

Mandatory human review is required for:

- CUDA or native-interop-facing changes.
- Packaging and release changes.
- Security-sensitive changes.
- Public API surface changes.

**Prompt storage policy**: Store summaries only. Raw prompt dumps
MUST NOT be committed unless a prompt is itself a user-facing asset.

### Observability Expectations

**During a run**, the system MUST expose:

- Current generation number.
- Best and average fitness.
- Species count and sizes.
- Time breakdown (evaluation, reproduction, speciation).
- Complexity statistics (average nodes/connections).

**After a run**, the system MUST produce:

- A summary report (text).
- Full metrics history (structured, serializable).
- Artifact references (champion genome, checkpoint files).

## Coding Practices

### Naming & Style

- All C# code MUST follow the
  [.NET Runtime Coding Style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md)
  unless this constitution explicitly overrides a rule.
- Public types MUST use PascalCase. Private fields MUST use
  `_camelCase` with a leading underscore.
- One top-level type per file. The file name MUST match the type
  name exactly (e.g., `Genome.cs` for `class Genome`).
- Namespaces MUST mirror the folder structure rooted at the
  project directory (e.g., `NeatSharp.Genetics` for
  `src/NeatSharp/Genetics/`).
- `var` MUST be used when the type is obvious from the right-hand
  side of the assignment. Explicit types MUST be used when the
  type is not obvious.
- LINQ expressions MUST prefer method syntax over query syntax
  for consistency.
- Magic numbers MUST be extracted into named constants or
  configuration parameters. The only permitted bare literals are
  `0`, `1`, `-1`, `true`, `false`, and `null`.

### Nullability

- Nullable reference types MUST be enabled project-wide
  (`<Nullable>enable</Nullable>`).
- The project MUST compile with zero nullable warnings
  (`TreatWarningsAsErrors` for nullable categories).
- Public API parameters MUST NOT accept null unless the parameter
  is explicitly typed as nullable with documented semantics for
  the null case.
- Internal code MUST use null-forgiving (`!`) only when a
  preceding guard or invariant guarantees non-null; each use MUST
  include a comment citing the invariant.

### Error Handling

- Public API methods MUST throw `ArgumentException`,
  `ArgumentNullException`, or `ArgumentOutOfRangeException` for
  invalid inputs. Generic `Exception` MUST NOT be thrown.
- Library-specific error conditions MUST use a custom exception
  hierarchy rooted at `NeatSharpException`.
- Exceptions MUST NOT be used for control flow. Expected
  failure paths (e.g., GPU unavailable) MUST use result types
  or explicit status checks.
- Every `catch` block MUST either handle the exception
  meaningfully or re-throw; empty catches are forbidden.
- CUDA kernel launch failures MUST be surfaced as
  `NeatSharpException` subclasses with the native error code
  included in the message.

### Resource Management

- Every type that holds unmanaged resources (CUDA device memory,
  native handles, pinned buffers) MUST implement `IDisposable`
  following the full Dispose pattern (destructor + `Dispose(bool)`
  + `GC.SuppressFinalize`).
- `using` statements or `using` declarations MUST be used at
  every call site that creates a disposable object.
- Finalizers MUST only release unmanaged resources and MUST NOT
  throw exceptions.
- GPU memory allocations MUST be pooled or reused across
  generations where feasible; allocation-per-generation patterns
  are prohibited in hot paths.

### CUDA & Native Interop

- All P/Invoke and CUDA interop declarations MUST be isolated
  in a dedicated `NeatSharp.Native` namespace; no other namespace
  may contain `DllImport`, `LibraryImport`, or raw pointer
  operations.
- Every native call MUST check its return code immediately.
  Unchecked native returns are forbidden.
- Managed-to-native data transfers MUST use pinned memory or
  `Span<T>`-based marshalling; `Marshal.AllocHGlobal` is
  prohibited unless no span-based alternative exists (with
  justification in a code comment).
- CUDA kernel wrappers MUST accept `ReadOnlySpan<T>` or
  `Span<T>` for input/output buffers and MUST NOT expose
  `IntPtr` in their public signatures.
- Platform-specific native binaries MUST be loaded via
  `NativeLibrary.Load` with explicit fallback paths for
  Windows and Linux.

### Testing Conventions

- Unit tests MUST use xUnit as the test framework.
- Test class names MUST follow the pattern
  `{TypeUnderTest}Tests` (e.g., `GenomeTests`).
- Test method names MUST follow the pattern
  `{Method}_{Scenario}_{ExpectedResult}`
  (e.g., `Mutate_AddNode_IncreasesNodeCount`).
- Each test MUST assert a single logical concept. Multiple
  related assertions on the same object are permitted; asserting
  on unrelated outcomes is forbidden.
- Tests involving randomness MUST use a fixed seed and MUST
  assert deterministic outcomes.
- GPU tests MUST be gated behind a `[Trait("Category", "GPU")]`
  attribute so CI can skip them when no GPU is available.
- Test projects MUST NOT reference `NeatSharp.Native` directly;
  GPU behavior MUST be tested through the public API.

## Governance

### License

MIT.

### Contribution

- All contributions via pull requests with maintainer review.
- RFCs are required for major or architectural changes.

### Security

Security vulnerabilities are reported via GitHub Security
Advisories. Maintainers MUST acknowledge reports within 7 days.

### Amendments

- This constitution supersedes conflicting project documentation.
- Amendments require a PR with rationale, maintainer approval, and
  a version bump following the versioning rules below.
- MAJOR version: Principle removal or backward-incompatible
  governance redefinition.
- MINOR version: New principle or materially expanded guidance.
- PATCH version: Clarifications, wording, and non-semantic fixes.

**Version**: 1.2.0 | **Ratified**: 2026-02-12 | **Last Amended**: 2026-02-12
