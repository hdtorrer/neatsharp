namespace NeatSharp.Serialization;

/// <summary>
/// Provides schema version constants and compatibility checks for serialized artifacts.
/// </summary>
public static class SchemaVersion
{
    /// <summary>
    /// The current schema version used when writing new artifacts.
    /// </summary>
    public const string Current = "1.0.0";

    /// <summary>
    /// The minimum schema version that can be loaded (with or without migration).
    /// </summary>
    public const string MinimumSupported = "1.0.0";

    /// <summary>
    /// Parses a semantic version string into its (Major, Minor, Patch) components.
    /// </summary>
    /// <param name="version">A version string in "Major.Minor.Patch" format (e.g., "1.0.0").</param>
    /// <returns>A tuple of (Major, Minor, Patch) integers.</returns>
    /// <exception cref="ArgumentException">Thrown when the version string is null, empty, or not valid semver.</exception>
    public static (int Major, int Minor, int Patch) Parse(string version)
    {
        if (string.IsNullOrEmpty(version))
        {
            throw new ArgumentException("Version string cannot be null or empty.", nameof(version));
        }

        var parts = version.Split('.');
        if (parts.Length != 3)
        {
            throw new ArgumentException($"Version string '{version}' is not valid semver (expected Major.Minor.Patch).", nameof(version));
        }

        if (!int.TryParse(parts[0], out int major) ||
            !int.TryParse(parts[1], out int minor) ||
            !int.TryParse(parts[2], out int patch))
        {
            throw new ArgumentException($"Version string '{version}' contains non-numeric components.", nameof(version));
        }

        return (major, minor, patch);
    }

    /// <summary>
    /// Determines whether the specified version is compatible with the current library.
    /// A version is compatible if it is between <see cref="MinimumSupported"/> and <see cref="Current"/> inclusive.
    /// </summary>
    /// <param name="version">The version string to check.</param>
    /// <returns><c>true</c> if the version can be loaded; <c>false</c> otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when the version string is invalid.</exception>
    public static bool IsCompatible(string version)
    {
        var parsed = Parse(version);
        var min = Parse(MinimumSupported);
        var cur = Parse(Current);

        return CompareVersions(parsed, min) >= 0 && CompareVersions(parsed, cur) <= 0;
    }

    /// <summary>
    /// Determines whether the specified version requires migration to the current version.
    /// A version needs migration if it is compatible but older than <see cref="Current"/>.
    /// </summary>
    /// <param name="version">The version string to check.</param>
    /// <returns><c>true</c> if migration is needed; <c>false</c> if the version is current or incompatible.</returns>
    /// <exception cref="ArgumentException">Thrown when the version string is invalid.</exception>
    public static bool NeedsMigration(string version)
    {
        var parsed = Parse(version);
        var min = Parse(MinimumSupported);
        var cur = Parse(Current);

        return CompareVersions(parsed, min) >= 0 && CompareVersions(parsed, cur) < 0;
    }

    private static int CompareVersions(
        (int Major, int Minor, int Patch) a,
        (int Major, int Minor, int Patch) b)
    {
        int majorCmp = a.Major.CompareTo(b.Major);
        if (majorCmp != 0)
        {
            return majorCmp;
        }

        int minorCmp = a.Minor.CompareTo(b.Minor);
        if (minorCmp != 0)
        {
            return minorCmp;
        }

        return a.Patch.CompareTo(b.Patch);
    }
}
