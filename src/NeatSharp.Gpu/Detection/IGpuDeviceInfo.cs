namespace NeatSharp.Gpu.Detection;

/// <summary>
/// Describes a detected GPU device and its compatibility with NeatSharp
/// GPU evaluation requirements.
/// </summary>
public interface IGpuDeviceInfo
{
    /// <summary>
    /// Gets the human-readable device name (e.g., "NVIDIA GeForce RTX 4090").
    /// </summary>
    string DeviceName { get; }

    /// <summary>
    /// Gets the CUDA compute capability as a version (e.g., 8.9).
    /// </summary>
    Version ComputeCapability { get; }

    /// <summary>
    /// Gets the total device memory in bytes.
    /// </summary>
    long MemoryBytes { get; }

    /// <summary>
    /// Gets whether this device meets the minimum requirements for
    /// NeatSharp GPU evaluation (compute capability >= configured minimum).
    /// </summary>
    bool IsCompatible { get; }

    /// <summary>
    /// Gets an actionable diagnostic message when the device is not compatible,
    /// including the minimum required compute capability and installation guidance.
    /// Null when <see cref="IsCompatible"/> is true.
    /// </summary>
    string? DiagnosticMessage { get; }
}
