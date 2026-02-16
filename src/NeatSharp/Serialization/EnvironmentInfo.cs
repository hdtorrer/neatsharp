using System.Runtime.InteropServices;

namespace NeatSharp.Serialization;

/// <summary>
/// Captures runtime environment information for diagnostic purposes.
/// </summary>
/// <param name="OsDescription">Operating system description (e.g., "Microsoft Windows 10.0.22631").</param>
/// <param name="RuntimeVersion">Runtime version string (e.g., ".NET 8.0.0").</param>
/// <param name="Architecture">Process architecture (e.g., "X64", "Arm64").</param>
public record EnvironmentInfo(
    string OsDescription,
    string RuntimeVersion,
    string Architecture)
{
    /// <summary>
    /// Creates an <see cref="EnvironmentInfo"/> instance reflecting the current runtime environment.
    /// </summary>
    /// <returns>A new <see cref="EnvironmentInfo"/> with current OS, runtime, and architecture details.</returns>
    public static EnvironmentInfo CreateCurrent() => new(
        OsDescription: RuntimeInformation.OSDescription,
        RuntimeVersion: RuntimeInformation.FrameworkDescription,
        Architecture: RuntimeInformation.ProcessArchitecture.ToString());
}
