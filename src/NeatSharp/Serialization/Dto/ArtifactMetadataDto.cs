namespace NeatSharp.Serialization.Dto;

/// <summary>
/// Data transfer object for <see cref="ArtifactMetadata"/> serialization.
/// </summary>
public class ArtifactMetadataDto
{
    /// <summary>
    /// Gets or sets the schema version.
    /// </summary>
    public string SchemaVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the library version.
    /// </summary>
    public string LibraryVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the random seed.
    /// </summary>
    public int Seed { get; set; }

    /// <summary>
    /// Gets or sets the configuration hash.
    /// </summary>
    public string ConfigurationHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ISO 8601 UTC creation timestamp.
    /// </summary>
    public string CreatedAtUtc { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the environment information.
    /// </summary>
    public EnvironmentInfoDto Environment { get; set; } = new();

    /// <summary>
    /// Maps an <see cref="ArtifactMetadata"/> domain object to a <see cref="ArtifactMetadataDto"/>.
    /// </summary>
    /// <param name="metadata">The metadata to map.</param>
    /// <returns>A new DTO representing the metadata.</returns>
    public static ArtifactMetadataDto ToDto(ArtifactMetadata metadata) => new()
    {
        SchemaVersion = metadata.SchemaVersion,
        LibraryVersion = metadata.LibraryVersion,
        Seed = metadata.Seed,
        ConfigurationHash = metadata.ConfigurationHash,
        CreatedAtUtc = metadata.CreatedAtUtc,
        Environment = EnvironmentInfoDto.ToDto(metadata.Environment)
    };

    /// <summary>
    /// Maps this DTO back to an <see cref="ArtifactMetadata"/> domain object.
    /// </summary>
    /// <returns>A new <see cref="ArtifactMetadata"/> instance.</returns>
    public ArtifactMetadata ToDomain() => new(
        SchemaVersion,
        LibraryVersion,
        Seed,
        ConfigurationHash,
        CreatedAtUtc,
        Environment.ToDomain());
}

/// <summary>
/// Data transfer object for <see cref="Serialization.EnvironmentInfo"/> serialization.
/// </summary>
public class EnvironmentInfoDto
{
    /// <summary>
    /// Gets or sets the OS description.
    /// </summary>
    public string OsDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime version.
    /// </summary>
    public string RuntimeVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the process architecture.
    /// </summary>
    public string Architecture { get; set; } = string.Empty;

    /// <summary>
    /// Maps an <see cref="EnvironmentInfo"/> domain object to a <see cref="EnvironmentInfoDto"/>.
    /// </summary>
    /// <param name="info">The environment info to map.</param>
    /// <returns>A new DTO representing the environment info.</returns>
    public static EnvironmentInfoDto ToDto(EnvironmentInfo info) => new()
    {
        OsDescription = info.OsDescription,
        RuntimeVersion = info.RuntimeVersion,
        Architecture = info.Architecture
    };

    /// <summary>
    /// Maps this DTO back to an <see cref="EnvironmentInfo"/> domain object.
    /// </summary>
    /// <returns>A new <see cref="EnvironmentInfo"/> instance.</returns>
    public EnvironmentInfo ToDomain() => new(
        OsDescription,
        RuntimeVersion,
        Architecture);
}
