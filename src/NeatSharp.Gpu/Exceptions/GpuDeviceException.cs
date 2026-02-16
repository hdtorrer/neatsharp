namespace NeatSharp.Gpu.Exceptions;

/// <summary>
/// Exception thrown when a GPU device does not meet the minimum requirements
/// for NeatSharp GPU evaluation.
/// </summary>
/// <remarks>
/// The exception message includes the device name, its compute capability,
/// the minimum required compute capability, and installation guidance to
/// help users resolve hardware incompatibility.
/// </remarks>
public class GpuDeviceException : GpuEvaluationException
{
    /// <summary>
    /// Initializes a new instance of <see cref="GpuDeviceException"/> with the specified error message.
    /// </summary>
    /// <param name="message">
    /// An actionable description including device name, compute capability,
    /// minimum required CC, and installation guidance.
    /// </param>
    public GpuDeviceException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="GpuDeviceException"/> with the specified error message
    /// and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">
    /// An actionable description including device name, compute capability,
    /// minimum required CC, and installation guidance.
    /// </param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public GpuDeviceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a <see cref="GpuDeviceException"/> with a standardized message
    /// including device name, compute capability, minimum required CC, and installation guidance.
    /// </summary>
    /// <param name="deviceName">The human-readable name of the GPU device.</param>
    /// <param name="computeCapability">The device's CUDA compute capability.</param>
    /// <param name="minRequiredComputeCapability">The minimum compute capability required by NeatSharp.</param>
    /// <returns>A new <see cref="GpuDeviceException"/> with a descriptive message.</returns>
    public static GpuDeviceException Create(
        string deviceName,
        Version computeCapability,
        Version minRequiredComputeCapability)
    {
        return new GpuDeviceException(
            $"GPU device '{deviceName}' has compute capability {computeCapability}, " +
            $"but NeatSharp requires at least {minRequiredComputeCapability}. " +
            $"Please upgrade to a compatible NVIDIA GPU (Maxwell architecture or newer) " +
            $"and ensure the latest CUDA drivers are installed from https://developer.nvidia.com/cuda-downloads.");
    }
}
