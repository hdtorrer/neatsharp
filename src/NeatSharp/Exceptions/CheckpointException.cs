namespace NeatSharp.Exceptions;

/// <summary>
/// Base exception for all checkpoint-related error conditions,
/// including corruption, version mismatch, and serialization failures.
/// </summary>
public class CheckpointException : NeatSharpException
{
    /// <summary>
    /// Initializes a new instance of <see cref="CheckpointException"/> with the specified error message.
    /// </summary>
    /// <param name="message">An actionable description of the checkpoint error.</param>
    public CheckpointException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="CheckpointException"/> with the specified error message
    /// and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">An actionable description of the checkpoint error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CheckpointException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
