using NeatSharp.Genetics;

namespace NeatSharp.Gpu.Evaluation;

/// <summary>
/// Represents a single node in the GPU evaluation order, containing
/// the buffer index, incoming source indices, incoming weights, and activation type.
/// </summary>
/// <param name="BufferIndex">Index in the flat activation buffer for this node.</param>
/// <param name="IncomingSources">Buffer indices of source nodes for incoming connections.</param>
/// <param name="IncomingWeights">Weights corresponding to each incoming connection (fp32 for GPU).</param>
/// <param name="ActivationType">Integer activation function type matching <see cref="GpuActivationFunction"/>.</param>
internal readonly record struct GpuEvalNode(
    int BufferIndex,
    int[] IncomingSources,
    float[] IncomingWeights,
    int ActivationType);

/// <summary>
/// GPU-aware feed-forward network implementing <see cref="IGenome"/>.
/// Wraps a CPU phenotype for fallback activation and carries flat GPU topology
/// arrays for efficient GPU kernel execution.
/// </summary>
/// <remarks>
/// <para>
/// The CPU phenotype (<see cref="IGenome"/>) handles all <see cref="IGenome.Activate"/>
/// calls — GPU evaluation is performed externally by <see cref="GpuBatchEvaluator"/>
/// using the flat topology arrays.
/// </para>
/// <para>
/// Created by <see cref="GpuNetworkBuilder"/> which performs topology extraction
/// and activation function mapping during construction.
/// </para>
/// </remarks>
internal sealed class GpuFeedForwardNetwork : IGenome
{
    private readonly IGenome _cpuNetwork;

    /// <summary>
    /// Initializes a new instance of <see cref="GpuFeedForwardNetwork"/>.
    /// </summary>
    /// <param name="cpuNetwork">The CPU phenotype for fallback activation.</param>
    /// <param name="inputIndices">Buffer indices for input nodes, in genome declaration order.</param>
    /// <param name="biasIndices">Buffer indices for bias nodes.</param>
    /// <param name="outputIndices">Buffer indices for output nodes, in genome declaration order.</param>
    /// <param name="nodeActivationTypes">Activation type for each node in buffer-index order.</param>
    /// <param name="evalOrder">Evaluation order for hidden and output nodes.</param>
    internal GpuFeedForwardNetwork(
        IGenome cpuNetwork,
        int[] inputIndices,
        int[] biasIndices,
        int[] outputIndices,
        int[] nodeActivationTypes,
        GpuEvalNode[] evalOrder)
    {
        ArgumentNullException.ThrowIfNull(cpuNetwork);
        ArgumentNullException.ThrowIfNull(inputIndices);
        ArgumentNullException.ThrowIfNull(biasIndices);
        ArgumentNullException.ThrowIfNull(outputIndices);
        ArgumentNullException.ThrowIfNull(nodeActivationTypes);
        ArgumentNullException.ThrowIfNull(evalOrder);

        _cpuNetwork = cpuNetwork;
        InputIndices = inputIndices;
        BiasIndices = biasIndices;
        OutputIndices = outputIndices;
        NodeActivationTypes = nodeActivationTypes;
        EvalOrder = evalOrder;
    }

    /// <summary>
    /// Gets the buffer indices for input nodes, in genome declaration order.
    /// </summary>
    internal int[] InputIndices { get; }

    /// <summary>
    /// Gets the buffer indices for bias nodes.
    /// </summary>
    internal int[] BiasIndices { get; }

    /// <summary>
    /// Gets the buffer indices for output nodes, in genome declaration order.
    /// </summary>
    internal int[] OutputIndices { get; }

    /// <summary>
    /// Gets the activation type for each node in buffer-index order.
    /// Values correspond to <see cref="GpuActivationFunction"/> enum.
    /// </summary>
    internal int[] NodeActivationTypes { get; }

    /// <summary>
    /// Gets the evaluation order for hidden and output nodes.
    /// </summary>
    internal GpuEvalNode[] EvalOrder { get; }

    /// <summary>
    /// Gets the total number of nodes from the CPU phenotype.
    /// </summary>
    public int NodeCount => _cpuNetwork.NodeCount;

    /// <summary>
    /// Gets the total number of connections from the CPU phenotype.
    /// </summary>
    public int ConnectionCount => _cpuNetwork.ConnectionCount;

    /// <summary>
    /// Delegates activation to the CPU phenotype.
    /// GPU evaluation is performed externally by <see cref="GpuBatchEvaluator"/>.
    /// </summary>
    public void Activate(ReadOnlySpan<double> inputs, Span<double> outputs)
    {
        _cpuNetwork.Activate(inputs, outputs);
    }
}
