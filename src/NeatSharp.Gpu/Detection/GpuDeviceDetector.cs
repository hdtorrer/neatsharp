using ILGPU;
using ILGPU.Runtime.Cuda;
using Microsoft.Extensions.Options;
using NeatSharp.Gpu.Configuration;

namespace NeatSharp.Gpu.Detection;

/// <summary>
/// Detects available CUDA GPU devices and determines compatibility with
/// NeatSharp GPU evaluation requirements.
/// </summary>
/// <remarks>
/// <para>
/// Creates an ILGPU <see cref="Context"/>, enumerates CUDA devices, and checks
/// the first device's compute capability against the configured minimum
/// (<see cref="GpuOptions.MinComputeCapability"/>). The detection result is
/// cached after the first call.
/// </para>
/// <para>
/// Returns null when no CUDA devices are found or when the CUDA runtime is
/// not available (e.g., missing drivers, CPU-only machine).
/// </para>
/// </remarks>
internal sealed class GpuDeviceDetector : IGpuDeviceDetector
{
    private readonly GpuOptions _options;
    private IGpuDeviceInfo? _cachedResult;
    private bool _detected;

    /// <summary>
    /// Initializes a new instance of <see cref="GpuDeviceDetector"/>.
    /// </summary>
    /// <param name="options">GPU configuration options containing minimum compute capability.</param>
    public GpuDeviceDetector(IOptions<GpuOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public IGpuDeviceInfo? Detect()
    {
        if (_detected)
        {
            return _cachedResult;
        }

        _detected = true;

        try
        {
            using var context = Context.CreateDefault();
            var cudaDevices = context.GetCudaDevices();

            if (cudaDevices.Count == 0)
            {
                _cachedResult = null;
                return null;
            }

            // Pick the first CUDA device
            var device = cudaDevices[0];
            string deviceName = device.Name;
            long memoryBytes = device.MemorySize;

            // Extract compute capability from CudaArchitecture struct.
            // CudaArchitecture exposes Major and Minor properties directly.
            // The Architecture property is nullable; if null, treat as no device.
            if (!device.Architecture.HasValue)
            {
                _cachedResult = null;
                return null;
            }

            var arch = device.Architecture.Value;
            var computeCapability = new Version(arch.Major, arch.Minor);

            // Check against minimum requirement
            int minCC = _options.MinComputeCapability;
            int minMajor = minCC / 10;
            int minMinor = minCC % 10;
            var minVersion = new Version(minMajor, minMinor);

            bool isCompatible = computeCapability >= minVersion;
            string? diagnostic = isCompatible
                ? null
                : $"GPU device '{deviceName}' has compute capability {computeCapability}, " +
                  $"but NeatSharp requires at least {minVersion}. " +
                  "Upgrade to a compatible NVIDIA GPU (Maxwell architecture or newer).";

            _cachedResult = new GpuDeviceInfo(
                deviceName, computeCapability, memoryBytes, isCompatible, diagnostic);
        }
        catch (Exception)
        {
            // CUDA runtime not available -- treat as no device
            _cachedResult = null;
        }

        return _cachedResult;
    }
}
