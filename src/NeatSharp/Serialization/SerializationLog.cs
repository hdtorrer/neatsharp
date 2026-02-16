using Microsoft.Extensions.Logging;

namespace NeatSharp.Serialization;

/// <summary>
/// Source-generated structured logging methods for serialization events.
/// </summary>
internal static partial class SerializationLog
{
    [LoggerMessage(EventId = 2001, Level = LogLevel.Warning,
        Message = "Configuration hash mismatch detected in loaded checkpoint. Stored hash: '{StoredHash}', computed hash: '{ComputedHash}'. The checkpoint may have been saved with a different library version.")]
    public static partial void ConfigHashMismatch(
        ILogger logger, string storedHash, string computedHash);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Error,
        Message = "Incompatible schema version '{Version}' in artifact. Expected '{ExpectedVersion}'.")]
    public static partial void IncompatibleSchemaVersion(
        ILogger logger, string version, string expectedVersion);

    [LoggerMessage(EventId = 2003, Level = LogLevel.Error,
        Message = "Checkpoint validation failed with {Count} error(s).")]
    public static partial void CheckpointValidationFailed(
        ILogger logger, int count);
}
