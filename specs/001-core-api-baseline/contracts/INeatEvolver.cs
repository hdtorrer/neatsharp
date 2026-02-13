// Contract definition — not compilable source code.
// This file defines the core evolution entry point.

namespace NeatSharp.Evolution;

/// <summary>
/// The single entry point for running a NEAT evolution.
/// Resolved from the DI container after calling
/// <c>services.AddNeatSharp(...)</c>.
/// </summary>
/// <remarks>
/// <para>
/// The evolver is configured via <see cref="NeatSharpOptions"/>
/// (registered through the Options pattern at DI setup time).
/// Each call to <see cref="RunAsync"/> executes a complete
/// evolution run: population initialization, evaluation,
/// speciation, selection, and reproduction over multiple
/// generations until a stopping criterion is met or cancellation
/// is requested.
/// </para>
/// <para>
/// On cancellation, the method returns the best genome found so
/// far with <see cref="EvolutionResult.WasCancelled"/> set to
/// <c>true</c>. It does NOT throw
/// <see cref="OperationCanceledException"/>.
/// </para>
/// </remarks>
public interface INeatEvolver
{
    /// <summary>
    /// Runs a NEAT evolution and returns the result.
    /// </summary>
    /// <param name="evaluator">
    /// The evaluation strategy used to assign fitness scores to genomes.
    /// Create via <see cref="EvaluationStrategy"/> factory methods.
    /// </param>
    /// <param name="cancellationToken">
    /// Optional token to request graceful cancellation.
    /// When cancelled, returns the best result found so far.
    /// </param>
    /// <returns>
    /// The evolution result containing the champion, run history,
    /// final population snapshot, and the seed used.
    /// </returns>
    /// <exception cref="NeatSharpException">
    /// Thrown when the user's fitness function throws during evaluation.
    /// The original exception is available via
    /// <see cref="Exception.InnerException"/>.
    /// </exception>
    Task<EvolutionResult> RunAsync(
        IEvaluationStrategy evaluator,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Convenience extension methods for <see cref="INeatEvolver"/>
/// that wrap common evaluation patterns.
/// </summary>
public static class NeatEvolverExtensions
{
    /// <summary>
    /// Runs evolution using a simple synchronous fitness function.
    /// </summary>
    /// <param name="evolver">The evolver instance.</param>
    /// <param name="fitnessFunction">
    /// A function that takes a genome and returns its fitness score.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The evolution result.</returns>
    public static Task<EvolutionResult> RunAsync(
        this INeatEvolver evolver,
        Func<IGenome, double> fitnessFunction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs evolution using an asynchronous fitness function.
    /// </summary>
    public static Task<EvolutionResult> RunAsync(
        this INeatEvolver evolver,
        Func<IGenome, CancellationToken, Task<double>> fitnessFunction,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs evolution using an environment evaluator.
    /// </summary>
    public static Task<EvolutionResult> RunAsync(
        this INeatEvolver evolver,
        IEnvironmentEvaluator evaluator,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs evolution using a batch evaluator.
    /// </summary>
    public static Task<EvolutionResult> RunAsync(
        this INeatEvolver evolver,
        IBatchEvaluator evaluator,
        CancellationToken cancellationToken = default);
}
