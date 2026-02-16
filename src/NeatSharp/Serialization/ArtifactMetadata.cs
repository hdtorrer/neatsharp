namespace NeatSharp.Serialization;

/// <summary>
/// Metadata attached to every serialized artifact (checkpoint, champion export, diagnostics bundle).
/// </summary>
/// <param name="SchemaVersion">The schema version of the artifact format.</param>
/// <param name="LibraryVersion">The NEATSharp library version that created the artifact.</param>
/// <param name="Seed">The random seed used for the evolution run.</param>
/// <param name="ConfigurationHash">SHA-256 hash of the serialized configuration.</param>
/// <param name="CreatedAtUtc">ISO 8601 UTC timestamp of artifact creation.</param>
/// <param name="Environment">Runtime environment details at the time of creation.</param>
public record ArtifactMetadata(
    string SchemaVersion,
    string LibraryVersion,
    int Seed,
    string ConfigurationHash,
    string CreatedAtUtc,
    EnvironmentInfo Environment);
