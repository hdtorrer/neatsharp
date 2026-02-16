namespace NeatSharp.Gpu.Evaluation;

/// <summary>
/// Enumerates the activation function types supported by the GPU evaluation kernels.
/// Integer values are used as indices in kernel dispatch to avoid string comparisons
/// on the GPU.
/// </summary>
/// <remarks>
/// Maps 1-to-1 with the string constants in <see cref="NeatSharp.Genetics.ActivationFunctions"/>.
/// The mapping is performed by <c>GpuNetworkBuilder</c> during network construction.
/// </remarks>
internal enum GpuActivationFunction
{
    /// <summary>Steepened sigmoid: 1 / (1 + e^(-4.9x)).</summary>
    Sigmoid = 0,

    /// <summary>Hyperbolic tangent.</summary>
    Tanh = 1,

    /// <summary>Rectified Linear Unit: max(0, x).</summary>
    ReLU = 2,

    /// <summary>Step function: 1 if x > 0, else 0.</summary>
    Step = 3,

    /// <summary>Identity function: x (passthrough).</summary>
    Identity = 4
}
