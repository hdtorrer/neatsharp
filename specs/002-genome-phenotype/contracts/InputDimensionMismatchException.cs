// Contract definition — not compilable source code.
// Defines the exception for input dimension validation during phenotype activation.

namespace NeatSharp.Exceptions;

/// <summary>
/// Thrown when the number of inputs provided to a phenotype does not
/// match the expected number of input nodes.
/// </summary>
public sealed class InputDimensionMismatchException : ArgumentException
{
    /// <summary>
    /// Gets the expected number of inputs (network's input node count).
    /// </summary>
    public int Expected { get; }

    /// <summary>
    /// Gets the actual number of inputs provided by the caller.
    /// </summary>
    public int Actual { get; }

    /// <summary>
    /// Initializes a new instance with the expected and actual input counts.
    /// Generates a descriptive message automatically.
    /// </summary>
    /// <param name="expected">The number of input nodes in the network.</param>
    /// <param name="actual">The number of inputs provided by the caller.</param>
    public InputDimensionMismatchException(int expected, int actual);

    /// <summary>
    /// Initializes a new instance with a custom message.
    /// </summary>
    /// <param name="message">A description of the dimension mismatch.</param>
    public InputDimensionMismatchException(string message);

    /// <summary>
    /// Initializes a new instance with a custom message and inner exception.
    /// </summary>
    /// <param name="message">A description of the dimension mismatch.</param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public InputDimensionMismatchException(string message, Exception innerException);
}
