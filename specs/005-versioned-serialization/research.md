# Research: Versioned Serialization

**Feature**: 005-versioned-serialization
**Date**: 2026-02-15
**Status**: Complete

## Research Item 1: RNG State Capture and Restore

### Context

The `NeatEvolver.RunAsync` creates a `Random` instance via `new Random(seed)` (line 51 of `NeatEvolver.cs`). For deterministic resume, we must capture and restore the exact internal state at a generation boundary.

### Findings

In .NET 8 and .NET 9, `new Random(int seed)` creates a `Net5CompatSeedImpl` internally — the legacy Knuth subtractive random number generator preserved for backward compatibility with seeded constructors. This is distinct from the `XoshiroImpl` used by the parameterless `new Random()`.

**Internal state of `Net5CompatSeedImpl`**:
- `int[] _seedArray` — 56-element array (the core state)
- `int _inext` — index pointer 1
- `int _inextp` — index pointer 2

These three fields fully determine the PRNG's future output. No other state exists.

**Access path**: `Random._impl` (field of type `Random.ImplBase`) → cast to `Net5CompatSeedImpl` → access `_seedArray`, `_inext`, `_inextp`.

**Verification**: The `Net5CompatSeedImpl` type name and field layout have been stable from .NET 6 through .NET 9. It exists specifically for backward compatibility and is unlikely to change.

### Decision

Use reflection to extract and restore the three state fields. Implement in a `RngStateHelper` static class with:
- `RngState Capture(Random random)` — extracts state via reflection
- `void Restore(Random random, RngState state)` — overwrites state via reflection

### Alternatives Considered

| Alternative | Rejected Because |
|------------|-----------------|
| Custom PRNG extending `Random` | Requires changing `Random` creation throughout codebase; higher implementation effort |
| Replay from seed (count calls) | Impractical for large runs; O(n) restoration time |
| Third-party PRNG (Redzen) | Adds external dependency (violates Principle V) |
| Xoshiro256** reimplementation | Seeded `Random` uses `Net5CompatSeedImpl`, not Xoshiro; mismatch |

### Risk Mitigation

Unit tests will validate:
1. Capture → restore produces identical next-N random values
2. Works on both .NET 8 and .NET 9 target frameworks
3. Deterministic resume produces results identical to uninterrupted run

If a future .NET version changes the internal implementation, reflection will throw (field not found) and tests will fail immediately — no silent corruption.

---

## Research Item 2: Configuration Hash Algorithm

### Context

FR-014 requires a deterministic configuration hash from the full `NeatSharpOptions` state. Used for verifying checkpoint-config compatibility on resume (FR-006a).

### Findings

`NeatSharpOptions` is a plain mutable class with nested option classes (all plain data). All properties are primitive types (`int`, `double`, `bool`, `int?`, `double?`) or enums (`WeightDistributionType`, `ComplexityPenaltyMetric`, `EvaluationErrorMode`). System.Text.Json can serialize the full object graph deterministically.

**Determinism concern**: JSON property ordering must be stable. System.Text.Json serializes properties in declaration order by default (reflection-based) and this order is deterministic for a given type.

**Hash algorithm**: SHA-256 (available in-box via `System.Security.Cryptography.SHA256`). Output as 64-character lowercase hex string.

### Decision

1. Serialize `NeatSharpOptions` to JSON using `JsonSerializer.Serialize()` with explicit `JsonSerializerOptions` (camelCase naming, sorted enum string conversion).
2. Compute SHA-256 of the resulting UTF-8 bytes.
3. Output as lowercase hex string.

The `JsonSerializerOptions` instance must be a singleton to ensure consistent serialization.

### Alternatives Considered

| Alternative | Rejected Because |
|------------|-----------------|
| GetHashCode() | Not deterministic across processes/runs |
| MD5 | Deprecated; SHA-256 is standard |
| xxHash / FNV | Requires additional implementation or dependency |
| Binary serialization | More complex, less debuggable |

---

## Research Item 3: System.Text.Json Serialization Patterns

### Context

FR-019 mandates System.Text.Json. Domain types have varying construction patterns: `Genome` validates in its constructor, `NodeGene`/`ConnectionGene` are records, `Species` is a mutable class, `NeatSharpOptions` is a plain POCO.

### Findings

**Records** (`NodeGene`, `ConnectionGene`, `Champion`, `RunHistory`, etc.): System.Text.Json supports records with constructor parameters natively in .NET 8+. However, these records use `IReadOnlyList<T>` properties which require concrete type handling.

**Genome class**: Constructor takes `IReadOnlyList<NodeGene>` and `IReadOnlyList<ConnectionGene>` and performs validation. Directly deserializing into this constructor would require:
- A `[JsonConstructor]` attribute, OR
- Custom converter

The validation in the constructor is actually desirable during deserialization — it catches corrupt data.

**Species class**: Mutable with internal setters. Not suitable for direct STJ serialization.

**NeatSharpOptions**: Plain POCO with public get/set properties. STJ handles this natively.

### Decision

Use a **DTO layer** (`NeatSharp.Serialization.Dto` namespace) for all types that don't serialize cleanly:

| Domain Type | DTO | Rationale |
|------------|-----|-----------|
| `Genome` | `GenomeDto` | Avoid validation on deserialization path; reconstruct via constructor post-validation |
| `NodeGene` | `NodeGeneDto` | Record with string default; DTO ensures explicit serialization |
| `ConnectionGene` | `ConnectionGeneDto` | Clean JSON property names |
| `Species` | `SpeciesCheckpointDto` | Captures species metadata, not runtime-mutable state |
| `NeatSharpOptions` | Direct serialization | Plain POCO, no issues |
| `RunHistory` | Direct serialization | Record with `IReadOnlyList` — STJ handles natively |
| `GenerationStatistics` | Direct serialization | Record — STJ handles natively |

The DTO types have public get/set properties and parameterless constructors for clean STJ deserialization. Mapping between domain and DTO types is explicit in the serializer.

### STJ Configuration

```csharp
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true,  // Human-readable checkpoints
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};
```

---

## Research Item 4: Library Version Detection at Runtime

### Context

FR-013 requires the library version in artifact metadata.

### Findings

Standard .NET approach: `typeof(SomeType).Assembly.GetName().Version` returns the `AssemblyVersion`. For informational version (includes pre-release tags), use `Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()`.

The `.csproj` currently doesn't set an explicit version, so it defaults to `1.0.0.0`. For proper version tracking, a `<Version>` property should be set in the `.csproj`, but this is a release concern outside this spec's scope.

### Decision

Use `typeof(SchemaVersion).Assembly.GetName().Version?.ToString() ?? "0.0.0"` for the library version. This will reflect whatever version is set in the `.csproj` at build time.

---

## Research Item 5: Schema Version Migration Design

### Context

FR-009 requires migration infrastructure for loading artifacts from prior schema versions. The initial release only has v1.0.0 — no actual migration exists yet.

### Findings

Common patterns:
1. **Linear chain**: v1 → v2 → v3 (each migrator handles one hop). Simple but slow for many hops.
2. **Direct migration**: Dedicated migrator per (from, to) pair. Fast but combinatorial.
3. **Strategy/registry pattern**: Register migrators by source version. Load determines which to invoke.

The spec limits scope to "at least one prior schema version" (no multi-hop). This means the linear chain pattern is sufficient.

### Decision

```
ISchemaVersionMigrator
├── Migrate(JsonDocument doc, string fromVersion) → JsonDocument
└── CanMigrate(string fromVersion) → bool

SchemaVersionMigrator (composite)
├── Holds Dictionary<string, ISchemaVersionMigrator> by fromVersion
├── On load: check schema version → if current, use directly; if prior, find migrator
└── If no migrator found → throw SchemaVersionException
```

For v1.0.0: The migrator registry is empty (no prior versions). A placeholder test validates that the current version loads without migration. When v1.1.0 or v2.0.0 is introduced, a `V1_0_0_To_V1_1_0_Migrator` is registered.

---

## Research Item 6: Structural Validation on Load (FR-023)

### Context

FR-023 requires full structural validation: connection-node reference integrity, species-genome assignment integrity, counter consistency, champion existence.

### Findings

The `Genome` constructor already validates:
- Unique node IDs
- Connection references to existing nodes
- At least one input and one output node

For checkpoint-level validation, additional checks are needed:
1. **Connection-node integrity**: Already handled by `Genome` constructor during deserialization.
2. **Species assignments**: Each genome referenced in a species' members list must exist in the population. The species representative must exist in the population.
3. **Counter consistency**: `NextInnovationNumber` must be > max innovation number in any connection. `NextNodeId` must be > max node ID in any genome.
4. **Champion existence**: The champion genome must exist in the population.

### Decision

Implement a `CheckpointValidator` that performs all four validation categories after deserialization but before returning the checkpoint to the consumer. Validation failures are collected into a `CheckpointCorruptionException` with all failed checks listed.

---

## Research Item 7: Atomic Write / Corruption Prevention

### Context

The spec edge case mentions interrupted writes: "The system must not leave a partially-written artifact that could be mistaken for a valid checkpoint."

### Findings

Since the serializer writes to a consumer-provided `Stream`, the library cannot control the storage backend's atomicity. For file streams, the consumer would need to implement temp-file-then-rename.

### Decision

Document in the quickstart guide that consumers should use a temporary-then-rename pattern for file-based checkpoints. The library's responsibility is to write complete, valid JSON to the stream — if the write completes without exception, the stream contains a valid artifact. If the write throws (e.g., disk full), the consumer's stream is in an indeterminate state, and the consumer is responsible for cleanup.

No built-in atomic write is needed since the library operates at the stream level, not the filesystem level. This follows FR-018 (stream abstraction).
