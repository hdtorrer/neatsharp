namespace NeatSharp.Genetics;

/// <summary>
/// Registry mapping activation function names to their implementations.
/// Pre-populated with built-in functions (sigmoid, tanh, ReLU, step, identity).
/// Supports consumer-registered custom activation functions.
/// </summary>
/// <remarks>
/// Name lookups are case-insensitive to prevent "Sigmoid" vs "sigmoid" errors.
/// </remarks>
public interface IActivationFunctionRegistry
{
    /// <summary>
    /// Retrieves the activation function registered under the specified name.
    /// </summary>
    /// <param name="name">The activation function name (case-insensitive).</param>
    /// <returns>The activation function delegate.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when no function is registered under <paramref name="name"/>.
    /// </exception>
    public Func<double, double> Get(string name);

    /// <summary>
    /// Registers a custom activation function.
    /// </summary>
    /// <param name="name">
    /// The name to register under (case-insensitive). Must not already be registered.
    /// </param>
    /// <param name="function">The activation function implementation.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/> or <paramref name="function"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is already registered.
    /// </exception>
    public void Register(string name, Func<double, double> function);

    /// <summary>
    /// Checks whether an activation function is registered under the specified name.
    /// </summary>
    /// <param name="name">The activation function name (case-insensitive).</param>
    /// <returns><c>true</c> if registered; otherwise <c>false</c>.</returns>
    public bool Contains(string name);
}
