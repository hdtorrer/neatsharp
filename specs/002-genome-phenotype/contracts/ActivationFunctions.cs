// Contract definition — not compilable source code.
// Defines the built-in activation function constants and implementations.

namespace NeatSharp.Genetics;

/// <summary>
/// Provides named constants and implementations for built-in activation functions.
/// Use the constants to reference activation functions by name (e.g., in
/// <see cref="NodeGene"/> construction). Implementations are pre-registered
/// in <see cref="IActivationFunctionRegistry"/>.
/// </summary>
public static class ActivationFunctions
{
    /// <summary>Sigmoid activation: 1 / (1 + e^(-x)).</summary>
    public const string Sigmoid = "sigmoid";

    /// <summary>Hyperbolic tangent activation.</summary>
    public const string Tanh = "tanh";

    /// <summary>Rectified Linear Unit: max(0, x).</summary>
    public const string ReLU = "relu";

    /// <summary>Step function: 1 if x &gt; 0, else 0.</summary>
    public const string Step = "step";

    /// <summary>Identity function: x (passthrough).</summary>
    public const string Identity = "identity";

    /// <summary>Computes the sigmoid function: 1 / (1 + e^(-x)).</summary>
    public static double SigmoidFunction(double x);

    /// <summary>Computes the hyperbolic tangent function.</summary>
    public static double TanhFunction(double x);

    /// <summary>Computes the Rectified Linear Unit: max(0, x).</summary>
    public static double ReLUFunction(double x);

    /// <summary>Computes the step function: 1 if x &gt; 0, else 0.</summary>
    public static double StepFunction(double x);

    /// <summary>Returns x unchanged (identity/passthrough).</summary>
    public static double IdentityFunction(double x);
}
