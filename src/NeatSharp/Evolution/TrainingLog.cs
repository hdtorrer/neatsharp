using Microsoft.Extensions.Logging;

namespace NeatSharp.Evolution;

/// <summary>
/// Source-generated structured logging methods for training events.
/// </summary>
internal static partial class TrainingLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information,
        Message = "Generation {Generation} completed: best={BestFitness:F4}, avg={AverageFitness:F4}, species={SpeciesCount}")]
    public static partial void GenerationCompleted(
        ILogger logger, int generation, double bestFitness, double averageFitness, int speciesCount);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information,
        Message = "New best fitness {Fitness:F4} found at generation {Generation} (previous: {PreviousBest:F4})")]
    public static partial void NewBestFitness(
        ILogger logger, int generation, double fitness, double previousBest);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Warning,
        Message = "Species {SpeciesId} went extinct at generation {Generation}")]
    public static partial void SpeciesExtinct(
        ILogger logger, int speciesId, int generation);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning,
        Message = "Stagnation detected in species {SpeciesId}: {GenerationsSinceImprovement} generations without improvement")]
    public static partial void StagnationDetected(
        ILogger logger, int speciesId, int generationsSinceImprovement);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information,
        Message = "Run completed: {TotalGenerations} generations, champion fitness={ChampionFitness:F4} (gen {ChampionGeneration}), cancelled={WasCancelled}")]
    public static partial void RunCompleted(
        ILogger logger, int totalGenerations, double championFitness, int championGeneration, bool wasCancelled);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning,
        Message = "Evaluation failed for genome at index {GenomeIndex}: {ExceptionMessage}")]
    public static partial void EvaluationFailed(
        ILogger logger, int genomeIndex, string exceptionMessage);
}
