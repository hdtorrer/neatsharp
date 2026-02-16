using NeatSharp.Exceptions;

namespace NeatSharp.Gpu.Exceptions;

/// <summary>
/// Exception thrown when GPU-based evaluation encounters an error.
/// </summary>
/// <remarks>
/// This is the base GPU exception type. More specific exceptions
/// (<see cref="GpuOutOfMemoryException"/>, <see cref="GpuDeviceException"/>)
/// derive from this class for targeted error handling.
/// </remarks>
public class GpuEvaluationException : NeatSharpException
{
    /// <summary>
    /// Initializes a new instance of <see cref="GpuEvaluationException"/> with the specified error message.
    /// </summary>
    /// <param name="message">An actionable description of the GPU evaluation error.</param>
    public GpuEvaluationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="GpuEvaluationException"/> with the specified error message
    /// and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">An actionable description of the GPU evaluation error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public GpuEvaluationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
