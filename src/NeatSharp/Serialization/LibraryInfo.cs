namespace NeatSharp.Serialization;

/// <summary>
/// Provides assembly-level metadata for the NEATSharp library.
/// </summary>
internal static class LibraryInfo
{
    /// <summary>
    /// Gets the library version string from the assembly metadata.
    /// </summary>
    public static string Version { get; } =
        typeof(LibraryInfo).Assembly.GetName().Version?.ToString() ?? "0.0.0";
}
