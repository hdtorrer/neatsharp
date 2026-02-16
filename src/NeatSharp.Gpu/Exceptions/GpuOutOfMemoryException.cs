namespace NeatSharp.Gpu.Exceptions;

/// <summary>
/// Exception thrown when a GPU evaluation fails due to insufficient device memory.
/// </summary>
/// <remarks>
/// The exception message includes the population size, estimated memory requirement,
/// and available GPU memory to help users diagnose and resolve the issue
/// (e.g., by reducing population size or using <see cref="Configuration.GpuOptions.MaxPopulationSize"/>).
/// </remarks>
public class GpuOutOfMemoryException : GpuEvaluationException
{
    /// <summary>
    /// Initializes a new instance of <see cref="GpuOutOfMemoryException"/> with the specified error message.
    /// </summary>
    /// <param name="message">
    /// An actionable description including population size, estimated memory, and available GPU memory.
    /// </param>
    public GpuOutOfMemoryException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="GpuOutOfMemoryException"/> with the specified error message
    /// and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">
    /// An actionable description including population size, estimated memory, and available GPU memory.
    /// </param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public GpuOutOfMemoryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a <see cref="GpuOutOfMemoryException"/> with a standardized message
    /// including population size, estimated memory, and available GPU memory.
    /// </summary>
    /// <param name="populationSize">The number of genomes in the population.</param>
    /// <param name="estimatedMemoryBytes">The estimated GPU memory required in bytes.</param>
    /// <param name="availableMemoryBytes">The available GPU memory in bytes.</param>
    /// <returns>A new <see cref="GpuOutOfMemoryException"/> with a descriptive message.</returns>
    public static GpuOutOfMemoryException Create(
        int populationSize,
        long estimatedMemoryBytes,
        long availableMemoryBytes)
    {
        return new GpuOutOfMemoryException(
            $"GPU out of memory: population size {populationSize} requires approximately " +
            $"{estimatedMemoryBytes / (1024.0 * 1024.0):F1} MB, but only " +
            $"{availableMemoryBytes / (1024.0 * 1024.0):F1} MB is available on the GPU. " +
            $"Reduce the population size or set GpuOptions.MaxPopulationSize to limit GPU buffer allocation.");
    }

    /// <summary>
    /// Creates a <see cref="GpuOutOfMemoryException"/> with a standardized message
    /// including population size, estimated memory, available GPU memory, and the original exception.
    /// </summary>
    /// <param name="populationSize">The number of genomes in the population.</param>
    /// <param name="estimatedMemoryBytes">The estimated GPU memory required in bytes.</param>
    /// <param name="availableMemoryBytes">The available GPU memory in bytes.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    /// <returns>A new <see cref="GpuOutOfMemoryException"/> with a descriptive message.</returns>
    public static GpuOutOfMemoryException Create(
        int populationSize,
        long estimatedMemoryBytes,
        long availableMemoryBytes,
        Exception innerException)
    {
        return new GpuOutOfMemoryException(
            $"GPU out of memory: population size {populationSize} requires approximately " +
            $"{estimatedMemoryBytes / (1024.0 * 1024.0):F1} MB, but only " +
            $"{availableMemoryBytes / (1024.0 * 1024.0):F1} MB is available on the GPU. " +
            $"Reduce the population size or set GpuOptions.MaxPopulationSize to limit GPU buffer allocation.",
            innerException);
    }
}
