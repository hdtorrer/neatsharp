using System.Text.Json;

namespace NeatSharp.Serialization.Migration;

/// <summary>
/// Composite migrator that delegates to version-specific migrators.
/// For v1.0.0 there are no prior versions, so the registry is empty.
/// </summary>
public sealed class SchemaVersionMigrator : ISchemaVersionMigrator
{
    private readonly Dictionary<string, ISchemaVersionMigrator> _migrators = new();

    /// <inheritdoc />
    public bool CanMigrate(string fromVersion)
    {
        return _migrators.ContainsKey(fromVersion);
    }

    /// <inheritdoc />
    public JsonDocument Migrate(JsonDocument doc, string fromVersion)
    {
        if (!_migrators.TryGetValue(fromVersion, out var migrator))
        {
            throw new InvalidOperationException(
                $"No migrator registered for schema version '{fromVersion}'.");
        }

        return migrator.Migrate(doc, fromVersion);
    }
}
