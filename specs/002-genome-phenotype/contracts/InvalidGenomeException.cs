// Contract definition — not compilable source code.
// Defines the exception for genome structural integrity violations.

namespace NeatSharp.Exceptions;

/// <summary>
/// Thrown when a genome cannot be constructed due to structural integrity
/// violations (e.g., duplicate node IDs, connections referencing non-existent
/// nodes, missing required node types).
/// </summary>
public sealed class InvalidGenomeException : NeatSharpException
{
    /// <summary>
    /// Initializes a new instance with the specified error message.
    /// </summary>
    /// <param name="message">A description of the structural violation.</param>
    public InvalidGenomeException(string message);

    /// <summary>
    /// Initializes a new instance with the specified error message
    /// and inner exception.
    /// </summary>
    /// <param name="message">A description of the structural violation.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public InvalidGenomeException(string message, Exception innerException);
}
