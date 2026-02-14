using NeatSharp.Exceptions;

namespace NeatSharp.Genetics;

/// <summary>
/// Executable feed-forward neural network implementing <see cref="IGenome"/>.
/// Pre-computes evaluation order at construction for allocation-free activation.
/// </summary>
/// <remarks>
/// NOT thread-safe — the shared activation buffer is mutated during each
/// <see cref="Activate"/> call. Each genome should have its own network instance.
/// </remarks>
internal sealed class FeedForwardNetwork : IGenome
{
    private readonly int _inputCount;
    private readonly int _outputCount;
    private readonly int[] _inputIndices;
    private readonly int[] _biasIndices;
    private readonly int[] _outputIndices;
    private readonly EvalNode[] _evalOrder;
    private readonly double[] _activations;

    /// <summary>
    /// Represents a node to be evaluated in topological order.
    /// </summary>
    /// <param name="BufferIndex">Index in the activation buffer.</param>
    /// <param name="IncomingConnections">Source buffer indices and weights.</param>
    /// <param name="ActivationFunction">The activation function to apply.</param>
    internal readonly record struct EvalNode(
        int BufferIndex,
        (int SourceIndex, double Weight)[] IncomingConnections,
        Func<double, double> ActivationFunction);

    internal FeedForwardNetwork(
        int inputCount,
        int outputCount,
        int[] inputIndices,
        int[] biasIndices,
        int[] outputIndices,
        EvalNode[] evalOrder,
        int nodeCount,
        int connectionCount)
    {
        _inputCount = inputCount;
        _outputCount = outputCount;
        _inputIndices = inputIndices;
        _biasIndices = biasIndices;
        _outputIndices = outputIndices;
        _evalOrder = evalOrder;
        _activations = new double[nodeCount];
        NodeCount = nodeCount;
        ConnectionCount = connectionCount;
    }

    /// <inheritdoc />
    public int NodeCount { get; }

    /// <inheritdoc />
    public int ConnectionCount { get; }

    /// <inheritdoc />
    public void Activate(ReadOnlySpan<double> inputs, Span<double> outputs)
    {
        if (inputs.Length != _inputCount)
        {
            throw new InputDimensionMismatchException(_inputCount, inputs.Length);
        }

        if (outputs.Length != _outputCount)
        {
            throw new ArgumentException(
                $"Expected {_outputCount} outputs, but received {outputs.Length}.",
                nameof(outputs));
        }

        // Clear activation buffer
        Array.Clear(_activations);

        // Set input node activations
        for (int i = 0; i < _inputIndices.Length; i++)
        {
            _activations[_inputIndices[i]] = inputs[i];
        }

        // Set bias node activations to 1.0
        for (int i = 0; i < _biasIndices.Length; i++)
        {
            _activations[_biasIndices[i]] = 1.0;
        }

        // Evaluate hidden and output nodes in topological order
        for (int i = 0; i < _evalOrder.Length; i++)
        {
            ref readonly var node = ref _evalOrder[i];
            double sum = 0.0;

            for (int j = 0; j < node.IncomingConnections.Length; j++)
            {
                var (sourceIndex, weight) = node.IncomingConnections[j];
                sum += _activations[sourceIndex] * weight;
            }

            _activations[node.BufferIndex] = node.ActivationFunction(sum);
        }

        // Copy output node activations to output span
        for (int i = 0; i < _outputIndices.Length; i++)
        {
            outputs[i] = _activations[_outputIndices[i]];
        }
    }
}
