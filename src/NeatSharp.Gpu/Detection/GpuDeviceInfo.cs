namespace NeatSharp.Gpu.Detection;

/// <summary>
/// Immutable record describing a detected GPU device and its compatibility status.
/// </summary>
/// <param name="DeviceName">The human-readable device name (e.g., "NVIDIA GeForce RTX 4090").</param>
/// <param name="ComputeCapability">The CUDA compute capability as a version (e.g., 8.9).</param>
/// <param name="MemoryBytes">The total device memory in bytes.</param>
/// <param name="IsCompatible">Whether this device meets the minimum requirements for NeatSharp GPU evaluation.</param>
/// <param name="DiagnosticMessage">
/// An actionable diagnostic message when the device is not compatible, or null when compatible.
/// </param>
public record GpuDeviceInfo(
    string DeviceName,
    Version ComputeCapability,
    long MemoryBytes,
    bool IsCompatible,
    string? DiagnosticMessage) : IGpuDeviceInfo;
