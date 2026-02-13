// Contract definition — not compilable source code.
// This file defines the public API contract for IGenome.
// The implementation will be created during task execution.

namespace NeatSharp.Genetics;

/// <summary>
/// Represents a genome (neural network topology) that can be activated
/// to produce outputs from inputs via feed-forward propagation.
/// </summary>
/// <remarks>
/// This is the primary abstraction users interact with in fitness evaluation.
/// The genome encapsulates the NEAT network structure (nodes, connections,
/// weights) and provides activation without exposing internal topology details.
/// </remarks>
public interface IGenome
{
    /// <summary>
    /// Gets the number of nodes in this genome's network.
    /// </summary>
    int NodeCount { get; }

    /// <summary>
    /// Gets the number of connections in this genome's network.
    /// </summary>
    int ConnectionCount { get; }

    /// <summary>
    /// Activates the neural network with the given inputs and writes
    /// the resulting outputs.
    /// </summary>
    /// <param name="inputs">
    /// Input values to feed into the network's input nodes.
    /// Length must match the network's input count.
    /// </param>
    /// <param name="outputs">
    /// Buffer to receive the network's output values.
    /// Length must match the network's output count.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="inputs"/> length does not match input
    /// node count, or <paramref name="outputs"/> length does not match
    /// output node count.
    /// </exception>
    void Activate(ReadOnlySpan<double> inputs, Span<double> outputs);
}
