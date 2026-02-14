namespace NeatSharp.Exceptions;

/// <summary>
/// Thrown when a genome's connection topology contains a cycle,
/// making feed-forward phenotype construction impossible.
/// </summary>
public sealed class CycleDetectedException : NeatSharpException
{
    /// <summary>
    /// Initializes a new instance with the specified error message.
    /// </summary>
    /// <param name="message">A description of the cycle detected.</param>
    public CycleDetectedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified error message
    /// and inner exception.
    /// </summary>
    /// <param name="message">A description of the cycle detected.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public CycleDetectedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
