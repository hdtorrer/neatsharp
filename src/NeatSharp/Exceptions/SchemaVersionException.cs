namespace NeatSharp.Exceptions;

/// <summary>
/// Thrown when a checkpoint or artifact has an incompatible schema version.
/// Provides the artifact's version, expected version, and whether migration is available.
/// </summary>
public class SchemaVersionException : CheckpointException
{
    /// <summary>
    /// Gets the schema version found in the artifact.
    /// </summary>
    public string ArtifactVersion { get; }

    /// <summary>
    /// Gets the expected (current) schema version.
    /// </summary>
    public string ExpectedVersion { get; }

    /// <summary>
    /// Gets a value indicating whether a migration path is available
    /// from the artifact version to the current version.
    /// </summary>
    public bool IsMigrationAvailable { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="SchemaVersionException"/> with
    /// version details and migration availability.
    /// </summary>
    /// <param name="artifactVersion">The schema version found in the artifact.</param>
    /// <param name="expectedVersion">The expected (current) schema version.</param>
    /// <param name="isMigrationAvailable">Whether a migration path exists.</param>
    public SchemaVersionException(string artifactVersion, string expectedVersion, bool isMigrationAvailable)
        : base(FormatMessage(artifactVersion, expectedVersion, isMigrationAvailable))
    {
        ArtifactVersion = artifactVersion;
        ExpectedVersion = expectedVersion;
        IsMigrationAvailable = isMigrationAvailable;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="SchemaVersionException"/> with
    /// version details, migration availability, and an inner exception.
    /// </summary>
    /// <param name="artifactVersion">The schema version found in the artifact.</param>
    /// <param name="expectedVersion">The expected (current) schema version.</param>
    /// <param name="isMigrationAvailable">Whether a migration path exists.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public SchemaVersionException(string artifactVersion, string expectedVersion, bool isMigrationAvailable, Exception innerException)
        : base(FormatMessage(artifactVersion, expectedVersion, isMigrationAvailable), innerException)
    {
        ArtifactVersion = artifactVersion;
        ExpectedVersion = expectedVersion;
        IsMigrationAvailable = isMigrationAvailable;
    }

    private static string FormatMessage(string artifactVersion, string expectedVersion, bool isMigrationAvailable)
    {
        var baseMessage = $"Schema version mismatch: artifact version is '{artifactVersion}', but this library expects version '{expectedVersion}'.";

        if (isMigrationAvailable)
        {
            return $"{baseMessage} A migration path is available and will be attempted automatically.";
        }

        // Determine if the artifact is newer or older for more actionable guidance
        try
        {
            var artifactParts = artifactVersion.Split('.');
            var expectedParts = expectedVersion.Split('.');

            if (artifactParts.Length == 3 && expectedParts.Length == 3 &&
                int.TryParse(artifactParts[0], out var artifactMajor) &&
                int.TryParse(expectedParts[0], out var expectedMajor))
            {
                if (artifactMajor > expectedMajor ||
                    (artifactMajor == expectedMajor &&
                     int.TryParse(artifactParts[1], out var artifactMinor) &&
                     int.TryParse(expectedParts[1], out var expectedMinor) &&
                     artifactMinor > expectedMinor))
                {
                    return $"{baseMessage} The artifact was created with a newer version of NEATSharp. Update the library to load this artifact.";
                }
            }
        }
        catch
        {
            // Fall through to default message if version parsing fails
        }

        return $"{baseMessage} No migration path is available. Re-export the artifact with the current library version.";
    }
}
