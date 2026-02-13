# Research: Core Package, Public API & Reproducibility Baseline

**Feature**: 001-core-api-baseline
**Date**: 2026-02-12

## R-001: .NET Multi-Targeting Strategy

**Decision**: Multi-target `net8.0;net9.0` using `<TargetFrameworks>` (plural) in the library .csproj. Shared build settings in `Directory.Build.props`.

**Rationale**: FR-017 requires .NET 8 (LTS) + .NET 9 (Current). Multi-targeting produces a single NuGet package with both TFM assemblies. NuGet automatically selects the best target for consumers.

**Alternatives considered**:
- Target `netstandard2.0` — rejected: loses access to modern APIs (Span-based marshalling, LoggerMessage source generators, nullable annotations), and both target TFMs are already .NET Core.
- Target only `net9.0` — rejected: violates FR-017; .NET 8 LTS users would be excluded.

**Key details**:
- `LangVersion` set to `13` (explicit, not `latest`) for reproducible builds across machines.
- `Directory.Build.props` centralizes: `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`.
- Test project also multi-targets `net8.0;net9.0` to catch TFM-specific regressions.

---

## R-002: Dependency Injection Registration Pattern

**Decision**: Expose `AddNeatSharp(this IServiceCollection, Action<NeatSharpOptions>?)` extension method in `NeatSharp.Extensions.ServiceCollectionExtensions`. Use the Options pattern (`IOptions<NeatSharpOptions>`) for configuration.

**Rationale**: This is the standard .NET ecosystem pattern for library DI registration. Constitution mandates DI, `AddNeatSharp(...)` extension, and `Microsoft.Extensions.DependencyInjection.Abstractions`.

**Alternatives considered**:
- Builder pattern (e.g., `services.AddNeatSharp().WithPopulationSize(150)`) — rejected: adds unnecessary complexity; the Options pattern with `Action<T>` is idiomatic and well-understood.
- No DI, static factory only — rejected: violates constitution DI Practices section; prevents testability via constructor injection.

**Key details**:
- `NeatSharpOptions` uses `[Range]` data annotations + `ValidateDataAnnotations()` for validation at startup.
- `ValidateOnStart()` ensures misconfigurations fail fast when the host starts, not when `RunAsync` is called.
- Additional custom validation via `IValidateOptions<NeatSharpOptions>` for cross-field rules (e.g., "at least one stopping criterion required").
- Service lifetimes: configuration as singleton, evolution engine as scoped (one run = one scope).

---

## R-003: Evaluation Strategy Abstraction

**Decision**: Single internal abstraction `IEvaluationStrategy` with static factory methods on `EvaluationStrategy` to create instances from user-facing patterns (sync function, async function, environment, batch).

**Rationale**: FR-002 requires "a single entry point" accepting "an evaluation strategy." A unified abstraction keeps `INeatEvolver` to a single method (ISP), while factory methods provide good DX for all three evaluation patterns (FR-003, FR-004, FR-005).

**Alternatives considered**:
- Multiple overloads on `INeatEvolver.RunAsync(...)` — rejected: violates ISP; the interface would grow with each new evaluation pattern.
- Separate runner interfaces per evaluation type — rejected: over-engineered; one method + adapters is simpler.
- Accept `Delegate` and pattern-match at runtime — rejected: loses type safety; error messages would be poor.

**Key details**:
- `EvaluationStrategy.FromFunction(Func<IGenome, double>)` — simplest path, sync.
- `EvaluationStrategy.FromFunction(Func<IGenome, CancellationToken, Task<double>>)` — async fitness.
- `EvaluationStrategy.FromEnvironment(IEnvironmentEvaluator)` — episode-based (FR-004).
- `EvaluationStrategy.FromBatch(IBatchEvaluator)` — bulk scoring (FR-005).
- Extension methods on `INeatEvolver` provide convenience overloads wrapping the factory.

---

## R-004: CancellationToken Handling

**Decision**: `INeatEvolver.RunAsync` accepts `CancellationToken`. On cancellation, returns `EvolutionResult` with `WasCancelled = true` and the best genome found so far. Does NOT throw `OperationCanceledException`.

**Rationale**: FR-018 explicitly requires graceful cancellation with partial results and a cancellation flag.

**Alternatives considered**:
- Throw `OperationCanceledException` (standard .NET pattern) — rejected: FR-018 explicitly says "rather than throwing."
- Return `null` on cancellation — rejected: forces null checks on every call site; a result with a flag is cleaner.

**Key details**:
- Check `cancellationToken.IsCancellationRequested` at the start of each generation loop (not `ThrowIfCancellationRequested`).
- Pass token through to `IEvaluationStrategy.EvaluateAsync(...)` so user code can also respond to cancellation.
- Entry point is `async Task<EvolutionResult>` to support async evaluation callbacks (environments, batch evaluators).

---

## R-005: Sync vs Async Entry Point

**Decision**: The entry point is `Task<EvolutionResult> RunAsync(...)` (async). The core loop is CPU-bound but evaluation callbacks may be async.

**Rationale**: User fitness functions might involve async operations (e.g., async environments, network calls in evaluation). Making the entry point async supports this without blocking threads. CPU-bound callers can still use it naturally.

**Alternatives considered**:
- Sync-only `EvolutionResult Run(...)` — rejected: forces `.GetAwaiter().GetResult()` for async evaluators, risking deadlocks.
- Both sync and async overloads — rejected: sync-over-async or async-over-sync wrappers are antipatterns; pick one.
- Sync entry + `Task.Run` wrapping — rejected: caller should decide threading, not the library (Stephen Cleary's principle).

---

## R-006: Logging and Metrics Strategy

**Decision**: Use `Microsoft.Extensions.Logging.Abstractions` with `ILogger<T>` injection. Use `[LoggerMessage]` source generator for zero-allocation logging when disabled (FR-013).

**Rationale**: FR-011 requires structured log events when enabled. FR-013 requires no overhead when disabled. The `[LoggerMessage]` source generator achieves both: structured logs at the call site, and the generated code short-circuits on `IsEnabled` check before allocating.

**Alternatives considered**:
- Custom event system — rejected: reinvents what M.E.Logging already provides; adds learning curve.
- `IObservable<T>` for metrics — rejected: adds dependency (System.Reactive) or custom implementation; M.E.Logging handles structured events. Metrics can use the same logging infrastructure or a dedicated metrics interface for later evolution.

**Key details**:
- Logging is controlled via standard log-level configuration (host responsibility), not a library toggle.
- Metrics (FR-012) are collected into `RunHistory.Generations` (in-memory) and optionally emitted via `ILogger` at `Debug` level.
- FR-013 zero-overhead: `[LoggerMessage]` generates `if (_logger.IsEnabled(level))` guard before any string interpolation.
- `NeatSharpOptions.EnableMetrics` controls whether `GenerationStatistics` are collected and stored in `RunHistory`. When `false`, the history is empty, and the per-generation stats allocation is skipped entirely.

---

## R-007: Configuration Validation Strategy

**Decision**: Two-layer validation: (1) Data annotation attributes (`[Range]`) for simple field constraints, validated via `ValidateDataAnnotations()` at DI startup. (2) Custom `IValidateOptions<NeatSharpOptions>` for cross-field rules (e.g., "at least one stopping criterion").

**Rationale**: FR-014 requires "clear, actionable errors for invalid inputs." Data annotations handle the common cases; `IValidateOptions<T>` handles the complex ones. Both run at startup (fail-fast) and produce named-parameter error messages.

**Alternatives considered**:
- Validate only at `RunAsync` time — rejected: delays error discovery; violates fail-fast principle.
- FluentValidation library — rejected: adds a third-party dependency; the built-in Options validation is sufficient.

**Key details**:
- `PopulationSize` must be > 0 (`[Range(1, int.MaxValue)]`).
- At least one of `StoppingCriteria.MaxGenerations`, `FitnessTarget`, or `StagnationThreshold` must be set.
- `Seed` is nullable; `null` means auto-generate (FR-010).
- Error messages reference the parameter name: e.g., `"NeatSharpOptions.PopulationSize must be greater than 0"`.

---

## R-008: Test Framework Setup

**Decision**: xUnit 2.9.3 with FluentAssertions 7.0.0. Test project multi-targets `net8.0;net9.0`. Test naming: `{Method}_{Scenario}_{ExpectedResult}` per constitution.

**Rationale**: Constitution mandates xUnit and the `{Method}_{Scenario}_{ExpectedResult}` naming pattern. FluentAssertions provides superior error messages and collection assertions without adding runtime dependency to the library itself (test-only).

**Alternatives considered**:
- NUnit — rejected: constitution mandates xUnit.
- xUnit built-in `Assert` only — rejected: FluentAssertions produces significantly better failure diagnostics (especially for collections and complex objects), which speeds up TDD red-green cycles.

**Key details**:
- `[Trait("Category", "GPU")]` for future GPU tests; CPU tests have no category trait.
- `dotnet test --filter "Category!=GPU"` to skip GPU tests in CI when no GPU available.
- `xunit.runner.json` with `methodDisplayOptions: replaceUnderscoreWithSpace` for readable test output.
- Tests involving randomness use a fixed seed and assert deterministic outcomes (constitution).

---

## R-009: NuGet Package Versions

**Decision**: Pin `Microsoft.Extensions.*` packages at `8.0.2` for maximum compatibility.

**Rationale**: Version 8.0.x is the lowest common denominator that supports both net8.0 and net9.0. Using 8.0.2 avoids forcing consumers on .NET 8 to pull in newer transitive dependencies.

**Alternatives considered**:
- Use 9.0.x or 10.0.x — rejected: would force .NET 8 consumers to load newer assembly versions; no features in newer versions are needed for this feature.
- Use Central Package Management (`Directory.Packages.props`) — deferred: reasonable for larger solutions but premature for two projects. Can be added later without breaking changes.
