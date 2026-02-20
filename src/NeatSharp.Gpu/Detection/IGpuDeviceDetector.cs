namespace NeatSharp.Gpu.Detection;

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
    public IGpuDeviceInfo? Detect();
}
