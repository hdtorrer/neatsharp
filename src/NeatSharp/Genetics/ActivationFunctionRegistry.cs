namespace NeatSharp.Genetics;

/// <summary>
/// Dictionary-backed registry mapping activation function names to their
/// implementations. Pre-populated with five built-in functions.
/// </summary>
/// <remarks>
/// Name lookups are case-insensitive (<see cref="StringComparer.OrdinalIgnoreCase"/>).
/// </remarks>
public sealed class ActivationFunctionRegistry : IActivationFunctionRegistry
{
    private readonly Dictionary<string, Func<double, double>> _functions = new(StringComparer.OrdinalIgnoreCase)
    {
        [ActivationFunctions.Sigmoid] = ActivationFunctions.SigmoidFunction,
        [ActivationFunctions.Tanh] = ActivationFunctions.TanhFunction,
        [ActivationFunctions.ReLU] = ActivationFunctions.ReLUFunction,
        [ActivationFunctions.Step] = ActivationFunctions.StepFunction,
        [ActivationFunctions.Identity] = ActivationFunctions.IdentityFunction,
    };

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
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(function);

        if (_functions.ContainsKey(name))
        {
            throw new ArgumentException(
                $"An activation function is already registered with name '{name}'.",
                nameof(name));
        }

        _functions[name] = function;
    }

    /// <inheritdoc />
    public bool Contains(string name) => _functions.ContainsKey(name);
}
