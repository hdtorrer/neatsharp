using ILGPU.Algorithms;

namespace NeatSharp.Gpu.Kernels;

/// <summary>
/// Provides GPU-compatible activation function implementations using ILGPU XMath
/// for fp32 math operations. All methods are static and compatible with ILGPU
/// kernel inlining.
/// </summary>
/// <remarks>
/// Activation function types correspond to <see cref="Evaluation.GpuActivationFunction"/> enum values:
/// <list type="bullet">
/// <item>0 = Sigmoid (steepened with 4.9)</item>
/// <item>1 = Tanh</item>
/// <item>2 = ReLU</item>
/// <item>3 = Step</item>
/// <item>4 = Identity</item>
/// </list>
/// </remarks>
internal static class ActivationKernels
{
    /// <summary>
    /// Applies the activation function identified by <paramref name="activationType"/>
    /// to the input value <paramref name="x"/>.
    /// </summary>
    /// <param name="x">The pre-activation value (weighted sum).</param>
    /// <param name="activationType">
    /// Integer activation type corresponding to <see cref="Evaluation.GpuActivationFunction"/>.
    /// </param>
    /// <returns>The activated output value.</returns>
    /// <remarks>
    /// This method must remain compatible with ILGPU kernel inlining:
    /// no reference types, exceptions, delegates, or dynamic allocation.
    /// Unknown activation types return the input unchanged (identity fallback).
    /// </remarks>
    public static float ApplyActivation(float x, int activationType)
    {
        // 0 = Sigmoid (steepened with 4.9)
        if (activationType == 0)
        {
            return 1.0f / (1.0f + XMath.Exp(-4.9f * x));
        }

        // 1 = Tanh
        if (activationType == 1)
        {
            return XMath.Tanh(x);
        }

        // 2 = ReLU
        if (activationType == 2)
        {
            return XMath.Max(0.0f, x);
        }

        // 3 = Step
        if (activationType == 3)
        {
            return x > 0.0f ? 1.0f : 0.0f;
        }

        // 4 = Identity (and unknown types)
        return x;
    }
}
