// CONTRACT — Design-time reference only. Not compiled.
// Actual implementation will be in src/NeatSharp.Gpu/Evaluation/IGpuFitnessFunction.cs

namespace NeatSharp.Gpu.Evaluation;

/// <summary>
/// Defines a GPU-compatible fitness evaluation protocol: test case inputs
/// and a CPU-side fitness computation function.
/// </summary>
/// <remarks>
/// <para>
/// The GPU evaluator handles forward propagation of all genomes across all
/// test cases in parallel. After downloading GPU outputs, it calls
/// <see cref="ComputeFitness"/> on the CPU for each genome to produce
/// the final fitness score.
/// </para>
/// <para>
/// <strong>Thread safety:</strong> <see cref="ComputeFitness"/> may be
/// called concurrently from multiple threads. Implementations must be
/// thread-safe or stateless.
/// </para>
/// </remarks>
public interface IGpuFitnessFunction
{
    /// <summary>
    /// Gets the number of test cases to evaluate each genome against.
    /// </summary>
    int CaseCount { get; }

    /// <summary>
    /// Gets the number of output values produced per genome per test case.
    /// </summary>
    /// <remarks>
    /// This must match the network's output node count. Used by the evaluator
    /// to correctly size output buffers and slice per-genome results.
    /// </remarks>
    int OutputCount { get; }

    /// <summary>
    /// Gets the flattened test case input data, row-major:
    /// <c>inputs[caseIndex * inputCount + inputIndex]</c>.
    /// </summary>
    /// <remarks>
    /// Length must equal <see cref="CaseCount"/> × the network's input count
    /// (from <see cref="Configuration.NeatSharpOptions.InputCount"/>).
    /// Data is uploaded to the GPU once per generation.
    /// </remarks>
    ReadOnlyMemory<float> InputCases { get; }

    /// <summary>
    /// Computes the fitness score for a single genome given its outputs
    /// across all test cases.
    /// </summary>
    /// <param name="outputs">
    /// Flattened output values, row-major:
    /// <c>outputs[caseIndex * outputCount + outputIndex]</c>.
    /// Length equals <see cref="CaseCount"/> × the network's output count.
    /// Values are fp32 from GPU computation.
    /// </param>
    /// <returns>
    /// The fitness score for this genome. Must be a finite, non-negative value.
    /// </returns>
    double ComputeFitness(ReadOnlySpan<float> outputs);
}
