using NeatSharp.Configuration;
using NeatSharp.Reporting;

namespace NeatSharp.Serialization.Dto;

/// <summary>
/// Top-level data transfer object for diagnostics bundle serialization.
/// Contains the complete training state, configuration, environment metadata,
/// and run history in a single JSON document for bug reports and troubleshooting.
/// </summary>
public class DiagnosticsBundleDto
{
    /// <summary>
    /// Gets or sets the schema version of the diagnostics bundle format.
    /// </summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the artifact metadata.
    /// </summary>
    public ArtifactMetadataDto Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the full checkpoint DTO containing all training state.
    /// </summary>
    public CheckpointDto Checkpoint { get; set; } = new();

    /// <summary>
    /// Gets or sets the configuration. Serialized directly as a plain POCO.
    /// </summary>
    public NeatSharpOptions Configuration { get; set; } = new();

    /// <summary>
    /// Gets or sets the environment information captured at bundle creation time.
    /// </summary>
    public EnvironmentInfoDto Environment { get; set; } = new();

    /// <summary>
    /// Gets or sets the run history. Serialized directly as a record.
    /// </summary>
    public RunHistory History { get; set; } = new([], 0);

    /// <summary>
    /// Maps a <see cref="TrainingCheckpoint"/> domain object to a <see cref="DiagnosticsBundleDto"/>.
    /// </summary>
    /// <param name="checkpoint">The training checkpoint to map.</param>
    /// <returns>A new DTO representing the diagnostics bundle.</returns>
    public static DiagnosticsBundleDto ToDto(TrainingCheckpoint checkpoint)
    {
        var currentEnvironment = EnvironmentInfo.CreateCurrent();
        var libraryVersion = LibraryInfo.Version;

        var metadata = new ArtifactMetadata(
            SchemaVersion: Serialization.SchemaVersion.Current,
            LibraryVersion: libraryVersion,
            Seed: checkpoint.Seed,
            ConfigurationHash: checkpoint.ConfigurationHash,
            CreatedAtUtc: DateTime.UtcNow.ToString("O"),
            Environment: currentEnvironment);

        return new DiagnosticsBundleDto
        {
            SchemaVersion = Serialization.SchemaVersion.Current,
            Metadata = ArtifactMetadataDto.ToDto(metadata),
            Checkpoint = CheckpointDto.ToDto(checkpoint),
            Configuration = checkpoint.Configuration,
            Environment = EnvironmentInfoDto.ToDto(currentEnvironment),
            History = checkpoint.History
        };
    }
}
