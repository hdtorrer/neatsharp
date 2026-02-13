namespace NeatSharp.Exceptions;

/// <summary>
/// Base exception for all NeatSharp library-specific error conditions.
/// </summary>
/// <remarks>
/// Used to wrap user fitness function exceptions and other library-specific errors.
/// Provides actionable error descriptions per FR-014.
/// </remarks>
public class NeatSharpException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="NeatSharpException"/> with the specified error message.
    /// </summary>
    /// <param name="message">An actionable description of the error.</param>
    public NeatSharpException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="NeatSharpException"/> with the specified error message
    /// and a reference to the inner exception that caused this exception.
    /// </summary>
    /// <param name="message">An actionable description of the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public NeatSharpException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
