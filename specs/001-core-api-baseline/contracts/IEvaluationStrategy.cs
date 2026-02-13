// Contract definition — not compilable source code.
// This file defines the internal evaluation abstraction used by the evolution engine.
// Users create instances via EvaluationStrategy static factory methods.

namespace NeatSharp.Evaluation;

/// <summary>
/// Internal abstraction for evaluating a population of genomes.
/// The evolution engine calls this once per generation to assign
/// fitness scores to all genomes.
/// </summary>
/// <remarks>
/// Users do not implement this interface directly. Instead, they use
/// the <see cref="EvaluationStrategy"/> factory methods:
/// <list type="bullet">
///   <item><see cref="EvaluationStrategy.FromFunction(Func{IGenome, double})"/> — simple sync fitness</item>
///   <item><see cref="EvaluationStrategy.FromFunction(Func{IGenome, CancellationToken, Task{double}})"/> — async fitness</item>
///   <item><see cref="EvaluationStrategy.FromEnvironment(IEnvironmentEvaluator)"/> — episode-based</item>
///   <item><see cref="EvaluationStrategy.FromBatch(IBatchEvaluator)"/> — bulk scoring</item>
/// </list>
/// </remarks>
public interface IEvaluationStrategy
{
    /// <summary>
    /// Evaluates all genomes in a population and reports their fitness scores.
    /// </summary>
    /// <param name="genomes">The genomes to evaluate.</param>
    /// <param name="setFitness">
    /// Callback to report a fitness score for a genome at the given index.
    /// Must be called exactly once for each index in <paramref name="genomes"/>.
    /// </param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A task that completes when all genomes have been evaluated.</returns>
    Task EvaluatePopulationAsync(
        IReadOnlyList<IGenome> genomes,
        Action<int, double> setFitness,
        CancellationToken cancellationToken);
}

/// <summary>
/// Factory methods for creating <see cref="IEvaluationStrategy"/> instances
/// from user-facing evaluation patterns.
/// </summary>
public static class EvaluationStrategy
{
    /// <summary>
    /// Creates a strategy from a synchronous fitness function.
    /// Each genome is evaluated individually by calling the function.
    /// </summary>
    /// <param name="fitnessFunction">
    /// A function that takes a genome and returns its fitness score.
    /// </param>
    /// <returns>An evaluation strategy that applies the function to each genome.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fitnessFunction"/> is null.
    /// </exception>
    public static IEvaluationStrategy FromFunction(Func<IGenome, double> fitnessFunction);

    /// <summary>
    /// Creates a strategy from an asynchronous fitness function.
    /// Each genome is evaluated individually by awaiting the function.
    /// </summary>
    /// <param name="fitnessFunction">
    /// An async function that takes a genome and cancellation token,
    /// and returns its fitness score.
    /// </param>
    /// <returns>An evaluation strategy that applies the function to each genome.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fitnessFunction"/> is null.
    /// </exception>
    public static IEvaluationStrategy FromFunction(
        Func<IGenome, CancellationToken, Task<double>> fitnessFunction);

    /// <summary>
    /// Creates a strategy from an environment evaluator.
    /// Each genome is run through the environment's episode loop.
    /// </summary>
    /// <param name="evaluator">
    /// An environment that evaluates genomes through multi-step episodes.
    /// </param>
    /// <returns>An evaluation strategy that runs each genome through the environment.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="evaluator"/> is null.
    /// </exception>
    public static IEvaluationStrategy FromEnvironment(IEnvironmentEvaluator evaluator);

    /// <summary>
    /// Creates a strategy from a batch evaluator.
    /// All genomes are passed to the evaluator in a single call.
    /// </summary>
    /// <param name="evaluator">
    /// A batch evaluator that scores multiple genomes at once.
    /// </param>
    /// <returns>An evaluation strategy that delegates to the batch evaluator.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="evaluator"/> is null.
    /// </exception>
    public static IEvaluationStrategy FromBatch(IBatchEvaluator evaluator);
}
