using System.Collections.Concurrent;

namespace NeatSharp.Genetics;

/// <summary>
/// Thread-safe registry mapping activation function names to their
/// implementations. Pre-populated with five built-in functions.
/// </summary>
/// <remarks>
/// Name lookups are case-insensitive (<see cref="StringComparer.OrdinalIgnoreCase"/>).
/// Uses <see cref="ConcurrentDictionary{TKey, TValue}"/> for safe concurrent access.
/// </remarks>
public sealed class ActivationFunctionRegistry : IActivationFunctionRegistry
{
    private readonly ConcurrentDictionary<string, Func<double, double>> _functions = new(
        new Dictionary<string, Func<double, double>>(StringComparer.OrdinalIgnoreCase)
        {
            [ActivationFunctions.Sigmoid] = ActivationFunctions.SigmoidFunction,
            [ActivationFunctions.Tanh] = ActivationFunctions.TanhFunction,
            [ActivationFunctions.ReLU] = ActivationFunctions.ReLUFunction,
            [ActivationFunctions.Step] = ActivationFunctions.StepFunction,
            [ActivationFunctions.Identity] = ActivationFunctions.IdentityFunction,
        },
        StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Func<double, double> Get(string name)
    {
        if (_functions.TryGetValue(name, out var function))
        {
            return function;
        }

        var available = string.Join(", ", _functions.Keys);
        throw new ArgumentException(
            $"No activation function registered with name '{name}'. Available functions: {available}.",
            nameof(name));
    }

    /// <inheritdoc />
    public void Register(string name, Func<double, double> function)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(function);

        if (!_functions.TryAdd(name, function))
        {
            throw new ArgumentException(
                $"An activation function is already registered with name '{name}'.",
                nameof(name));
        }
    }

    /// <inheritdoc />
    public bool Contains(string name) => _functions.ContainsKey(name);
}
