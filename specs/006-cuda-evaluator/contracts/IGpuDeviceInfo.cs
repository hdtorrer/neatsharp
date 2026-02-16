// CONTRACT — Design-time reference only. Not compiled.
// Actual implementation will be in src/NeatSharp.Gpu/Detection/

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

/// <summary>
/// Detects available GPU devices and determines compatibility.
/// </summary>
public interface IGpuDeviceDetector
{
    /// <summary>
    /// Detects the best available GPU device, or returns null if no compatible
    /// device is found.
    /// </summary>
    /// <returns>
    /// Device info for the best compatible GPU, or null if no GPU is available.
    /// Result is cached after first call.
    /// </returns>
    /// <remarks>
    /// When a GPU is present but incompatible, the returned
    /// <see cref="IGpuDeviceInfo"/> has <see cref="IGpuDeviceInfo.IsCompatible"/>
    /// set to false with a diagnostic message explaining the incompatibility.
    /// When no GPU hardware is detected at all, returns null.
    /// </remarks>
    IGpuDeviceInfo? Detect();
}
