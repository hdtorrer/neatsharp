using System.Text.Json;

namespace NeatSharp.Serialization.Migration;

/// <summary>
/// Migrates serialized artifacts from older schema versions to the current version.
/// </summary>
public interface ISchemaVersionMigrator
{
    /// <summary>
    /// Determines whether this migrator can handle migration from the specified version.
    /// </summary>
    /// <param name="fromVersion">The source schema version to migrate from.</param>
    /// <returns><c>true</c> if migration from the specified version is supported; <c>false</c> otherwise.</returns>
    bool CanMigrate(string fromVersion);

    /// <summary>
    /// Migrates a JSON document from the specified version to the current schema version.
    /// </summary>
    /// <param name="doc">The JSON document to migrate.</param>
    /// <param name="fromVersion">The source schema version of the document.</param>
    /// <returns>A new JSON document conforming to the current schema version.</returns>
    JsonDocument Migrate(JsonDocument doc, string fromVersion);
}
