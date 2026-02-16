using ILGPU;
using ILGPU.Runtime;

namespace NeatSharp.Gpu.Kernels;

/// <summary>
/// Bundles per-genome offset and count arrays for the forward propagation kernel.
/// </summary>
/// <remarks>
/// Used to reduce kernel parameter count below ILGPU's limit by packing
/// related <see cref="ArrayView1D{T, TStride}"/> fields into a single struct parameter.
/// </remarks>
internal struct GenomeIndexData
{
    /// <summary>Start index per genome in node arrays.</summary>
    public ArrayView1D<int, Stride1D.Dense> GenomeNodeOffsets;

    /// <summary>Node count per genome.</summary>
    public ArrayView1D<int, Stride1D.Dense> GenomeNodeCounts;

    /// <summary>Start index per genome in eval order arrays.</summary>
    public ArrayView1D<int, Stride1D.Dense> GenomeEvalOrderOffsets;

    /// <summary>Eval order length per genome.</summary>
    public ArrayView1D<int, Stride1D.Dense> GenomeEvalOrderCounts;

    /// <summary>Start index per genome in input arrays.</summary>
    public ArrayView1D<int, Stride1D.Dense> GenomeInputOffsets;

    /// <summary>Input count per genome.</summary>
    public ArrayView1D<int, Stride1D.Dense> GenomeInputCounts;

    /// <summary>Start index per genome in output arrays.</summary>
    public ArrayView1D<int, Stride1D.Dense> GenomeOutputOffsets;

    /// <summary>Output count per genome.</summary>
    public ArrayView1D<int, Stride1D.Dense> GenomeOutputCounts;

    /// <summary>Start index per genome in bias arrays.</summary>
    public ArrayView1D<int, Stride1D.Dense> GenomeBiasOffsets;

    /// <summary>Bias count per genome.</summary>
    public ArrayView1D<int, Stride1D.Dense> GenomeBiasCounts;
}

/// <summary>
/// Bundles flattened topology arrays for the forward propagation kernel.
/// </summary>
/// <remarks>
/// Used to reduce kernel parameter count below ILGPU's limit by packing
/// related <see cref="ArrayView1D{T, TStride}"/> fields into a single struct parameter.
/// </remarks>
internal struct TopologyData
{
    /// <summary>Activation type per node.</summary>
    public ArrayView1D<int, Stride1D.Dense> NodeActivationTypes;

    /// <summary>Globally-adjusted node buffer index in eval order.</summary>
    public ArrayView1D<int, Stride1D.Dense> EvalNodeIndices;

    /// <summary>Start index into incoming arrays per eval node.</summary>
    public ArrayView1D<int, Stride1D.Dense> EvalNodeConnOffsets;

    /// <summary>Incoming connection count per eval node.</summary>
    public ArrayView1D<int, Stride1D.Dense> EvalNodeConnCounts;

    /// <summary>Globally-adjusted source node buffer indices.</summary>
    public ArrayView1D<int, Stride1D.Dense> IncomingSourceIndices;

    /// <summary>Connection weights (fp32).</summary>
    public ArrayView1D<float, Stride1D.Dense> IncomingWeights;

    /// <summary>Globally-adjusted buffer indices for all input nodes.</summary>
    public ArrayView1D<int, Stride1D.Dense> InputNodeIndices;

    /// <summary>Globally-adjusted buffer indices for all output nodes.</summary>
    public ArrayView1D<int, Stride1D.Dense> OutputNodeIndices;

    /// <summary>Globally-adjusted buffer indices for all bias nodes.</summary>
    public ArrayView1D<int, Stride1D.Dense> BiasNodeIndices;
}

/// <summary>
/// ILGPU kernel that performs forward propagation for an entire population of
/// NEAT networks across all test cases. Each thread evaluates one genome.
/// </summary>
/// <remarks>
/// <para>
/// The kernel is designed for 1 thread per genome execution, where each thread
/// sequentially processes all test cases for its assigned genome. This ensures
/// deterministic accumulation order within each genome evaluation.
/// </para>
/// <para>
/// <strong>Determinism guarantees (BestEffortDeterministic mode):</strong>
/// <list type="bullet">
/// <item><strong>Same machine, same driver, same population:</strong> Identical GPU results
/// across runs. The 1-thread-per-genome design with sequential accumulation in fixed
/// topological order eliminates thread-scheduling non-determinism.</item>
/// <item><strong>Cross-machine:</strong> Results may vary at the ULP (Unit in the Last Place)
/// level due to different GPU hardware fp32 implementations, driver-level FMA (Fused
/// Multiply-Add) instruction behavior, and compiler optimizations that differ between
/// GPU architectures (e.g., Ampere vs Hopper).</item>
/// <item><strong>GPU vs CPU:</strong> Results are NOT bitwise identical due to fp32 vs fp64
/// precision differences. The documented tolerance is 1e-4 per output value.</item>
/// </list>
/// </para>
/// <para>
/// For each test case the kernel:
/// (1) clears the activation buffer for this genome's nodes,
/// (2) loads test case inputs into input nodes,
/// (3) sets bias nodes to 1.0f,
/// (4) iterates eval order nodes sequentially accumulating weighted inputs
///     and applying activation via <see cref="ActivationKernels.ApplyActivation"/>,
/// (5) copies output node values to the test output buffer.
/// </para>
/// <para>
/// ILGPU kernel constraints: no reference types, no exceptions, no delegates,
/// no dynamic allocation. All data access is through <see cref="ArrayView1D{T, TStride}"/>
/// value types.
/// </para>
/// </remarks>
internal static class ForwardPropagationKernel
{
    /// <summary>
    /// Evaluates all test cases for a single genome (1 thread per genome).
    /// </summary>
    /// <param name="genomeIndex">The index of the genome to evaluate (thread index).</param>
    /// <param name="genomeData">Per-genome offset and count arrays.</param>
    /// <param name="topology">Flattened topology arrays.</param>
    /// <param name="testInputs">
    /// Flattened test case inputs, row-major: testInputs[caseIndex * inputCount + inputIndex].
    /// Length: caseCount * inputCount.
    /// </param>
    /// <param name="caseCount">Number of test cases.</param>
    /// <param name="inputCount">Number of inputs per test case.</param>
    /// <param name="outputCount">Number of outputs per test case.</param>
    /// <param name="testOutputs">
    /// Output buffer for all genomes and test cases, row-major:
    /// testOutputs[genomeIndex * caseCount * outputCount + caseIndex * outputCount + outputIndex].
    /// Length: populationSize * caseCount * outputCount.
    /// </param>
    /// <param name="activationBuffer">
    /// Shared workspace for node activation values. Each genome uses a region
    /// starting at activationBuffer[genomeNodeOffsets[genomeIndex]].
    /// Length: totalNodes (sum of all genomes' node counts).
    /// </param>
    public static void Execute(
        Index1D genomeIndex,
        GenomeIndexData genomeData,
        TopologyData topology,
        ArrayView1D<float, Stride1D.Dense> testInputs,
        int caseCount,
        int inputCount,
        int outputCount,
        ArrayView1D<float, Stride1D.Dense> testOutputs,
        ArrayView1D<float, Stride1D.Dense> activationBuffer)
    {
        int nodeOffset = genomeData.GenomeNodeOffsets[genomeIndex];
        int nodeCount = genomeData.GenomeNodeCounts[genomeIndex];
        int evalOrderOffset = genomeData.GenomeEvalOrderOffsets[genomeIndex];
        int evalOrderCount = genomeData.GenomeEvalOrderCounts[genomeIndex];
        int inputNodeOffset = genomeData.GenomeInputOffsets[genomeIndex];
        int inputNodeCount = genomeData.GenomeInputCounts[genomeIndex];
        int outputNodeOffset = genomeData.GenomeOutputOffsets[genomeIndex];
        int outputNodeCount = genomeData.GenomeOutputCounts[genomeIndex];
        int biasNodeOffset = genomeData.GenomeBiasOffsets[genomeIndex];
        int biasNodeCount = genomeData.GenomeBiasCounts[genomeIndex];

        for (int c = 0; c < caseCount; c++)
        {
            // Step 1: Clear activation buffer for this genome's nodes
            for (int n = 0; n < nodeCount; n++)
            {
                activationBuffer[nodeOffset + n] = 0.0f;
            }

            // Step 2: Load test case inputs into input nodes
            for (int i = 0; i < inputNodeCount; i++)
            {
                int nodeIdx = topology.InputNodeIndices[inputNodeOffset + i];
                if (i < inputCount)
                {
                    activationBuffer[nodeIdx] = testInputs[c * inputCount + i];
                }
            }

            // Step 3: Set bias nodes to 1.0f
            for (int b = 0; b < biasNodeCount; b++)
            {
                int nodeIdx = topology.BiasNodeIndices[biasNodeOffset + b];
                activationBuffer[nodeIdx] = 1.0f;
            }

            // Step 4: Iterate eval order nodes — accumulate weighted inputs, apply activation
            for (int e = 0; e < evalOrderCount; e++)
            {
                int globalEvalIdx = evalOrderOffset + e;
                int nodeIdx = topology.EvalNodeIndices[globalEvalIdx];
                int connOffset = topology.EvalNodeConnOffsets[globalEvalIdx];
                int connCount = topology.EvalNodeConnCounts[globalEvalIdx];
                int activationType = topology.NodeActivationTypes[nodeIdx];

                // Accumulate weighted inputs (sequential for determinism)
                float sum = 0.0f;
                for (int ci = 0; ci < connCount; ci++)
                {
                    int sourceIdx = topology.IncomingSourceIndices[connOffset + ci];
                    float weight = topology.IncomingWeights[connOffset + ci];
                    sum += activationBuffer[sourceIdx] * weight;
                }

                // Apply activation function
                activationBuffer[nodeIdx] = ActivationKernels.ApplyActivation(sum, activationType);
            }

            // Step 5: Copy output node values to test output buffer
            for (int o = 0; o < outputNodeCount; o++)
            {
                int nodeIdx = topology.OutputNodeIndices[outputNodeOffset + o];
                int outputIdx = genomeIndex * caseCount * outputCount + c * outputCount + o;
                testOutputs[outputIdx] = activationBuffer[nodeIdx];
            }
        }
    }
}
