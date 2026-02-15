using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evaluation;
using NeatSharp.Evolution.Selection;
using NeatSharp.Evolution.Speciation;
using NeatSharp.Genetics;
using NeatSharp.Reporting;

namespace NeatSharp.Evolution;

/// <summary>
/// Default implementation of <see cref="INeatEvolver"/>. Orchestrates the complete
/// NEAT evolution loop: population initialization, evaluation, speciation, selection,
/// and reproduction across generations until a stopping criterion is met.
/// </summary>
internal sealed class NeatEvolver : INeatEvolver
{
    private readonly NeatSharpOptions _options;
    private readonly IPopulationFactory _populationFactory;
    private readonly INetworkBuilder _networkBuilder;
    private readonly ISpeciationStrategy _speciationStrategy;
    private readonly ReproductionOrchestrator _reproductionOrchestrator;
    private readonly IInnovationTracker _tracker;
    private readonly ILogger<NeatEvolver> _logger;

    public NeatEvolver(
        IOptions<NeatSharpOptions> options,
        IPopulationFactory populationFactory,
        INetworkBuilder networkBuilder,
        ISpeciationStrategy speciationStrategy,
        ReproductionOrchestrator reproductionOrchestrator,
        IInnovationTracker tracker,
        ILogger<NeatEvolver> logger)
    {
        _options = options.Value;
        _populationFactory = populationFactory;
        _networkBuilder = networkBuilder;
        _speciationStrategy = speciationStrategy;
        _reproductionOrchestrator = reproductionOrchestrator;
        _tracker = tracker;
        _logger = logger;
    }

    public async Task<EvolutionResult> RunAsync(
        IEvaluationStrategy evaluator,
        CancellationToken cancellationToken = default)
    {
        // 1. Resolve seed
        int seed = _options.Seed ?? Random.Shared.Next();
        var random = new Random(seed);

        // 2. Create initial population
        var population = _populationFactory.CreateInitialPopulation(
            _options.PopulationSize,
            _options.InputCount,
            _options.OutputCount,
            random,
            _tracker);

        // 3. Call tracker.NextGeneration() after init
        _tracker.NextGeneration();

        // Metrics state
        bool enableMetrics = _options.EnableMetrics;
        List<GenerationStatistics>? generationHistory = enableMetrics ? [] : null;

        // Error handling config
        var evalErrorMode = _options.Evaluation.ErrorMode;
        double evalErrorFitness = _options.Evaluation.ErrorFitnessValue;

        // State
        var currentPopulation = new List<Genome>(population);
        var species = new List<Species>();
        Genome? championGenome = null;
        double championFitness = double.NegativeInfinity;
        int championGeneration = 0;
        int generation = 0;
        int completedGenerations = 0;
        bool wasCancelled = false;
        var fitness = new double[currentPopulation.Count];
        var scored = new bool[currentPopulation.Count];

        // Generation loop
        while (true)
        {
            // Cancel check at generation boundary
            if (cancellationToken.IsCancellationRequested)
            {
                wasCancelled = true;
                break;
            }

            // === Evaluation phase ===
            Stopwatch? evalSw = enableMetrics ? Stopwatch.StartNew() : null;

            await EvaluatePopulationAsync(
                evaluator, currentPopulation, fitness, scored,
                evalErrorMode, evalErrorFitness);

            evalSw?.Stop();

            // Track champion
            double previousBest = championFitness;
            UpdateChampion(currentPopulation, fitness, generation,
                ref championGenome, ref championFitness, ref championGeneration);

            if (championFitness > previousBest)
            {
                double prev = previousBest == double.NegativeInfinity ? 0.0 : previousBest;
                TrainingLog.NewBestFitness(_logger, championGeneration, championFitness, prev);
            }

            // === Speciation phase ===
            Stopwatch? specSw = enableMetrics ? Stopwatch.StartNew() : null;

            SpeciateAndLog(currentPopulation, fitness, species, generation);

            specSw?.Stop();

            // Log generation completed
            double bestFitness = GetMaxFitness(fitness);
            double avgFitness = GetAverageFitness(fitness);
            TrainingLog.GenerationCompleted(_logger, generation, bestFitness, avgFitness, species.Count);

            // Mark generation as completed
            completedGenerations = generation + 1;

            // Pre-compute metrics data (before stopping check and reproduction)
            List<int>? metricsSpeciesSizes = null;
            double metricsAvgNodes = 0, metricsAvgConns = 0;
            if (enableMetrics)
            {
                metricsSpeciesSizes = new List<int>(species.Count);
                foreach (var s in species)
                    metricsSpeciesSizes.Add(s.Members.Count);

                ComputeComplexityStats(currentPopulation, out metricsAvgNodes, out metricsAvgConns);
            }

            // Check stopping criteria
            if (ShouldStop(completedGenerations, championFitness, species))
            {
                if (enableMetrics)
                {
                    generationHistory!.Add(new GenerationStatistics(
                        generation, bestFitness, avgFitness, species.Count, metricsSpeciesSizes!,
                        new ComplexityStatistics(metricsAvgNodes, metricsAvgConns),
                        new TimingBreakdown(evalSw!.Elapsed, TimeSpan.Zero, specSw!.Elapsed)));
                }
                break;
            }

            // === Reproduction phase ===
            Stopwatch? reproSw = enableMetrics ? Stopwatch.StartNew() : null;

            var offspring = _reproductionOrchestrator.Reproduce(species, random, _tracker);
            currentPopulation = EnforceComplexityLimits(offspring, species);

            reproSw?.Stop();

            // Guard: if reproduction produced no offspring, the population is extinct
            if (currentPopulation.Count == 0)
            {
                TrainingLog.PopulationExtinct(_logger, generation);
                break;
            }

            // Collect generation metrics
            if (enableMetrics)
            {
                generationHistory!.Add(new GenerationStatistics(
                    generation, bestFitness, avgFitness, species.Count, metricsSpeciesSizes!,
                    new ComplexityStatistics(metricsAvgNodes, metricsAvgConns),
                    new TimingBreakdown(evalSw!.Elapsed, reproSw!.Elapsed, specSw!.Elapsed)));
            }

            // Resize fitness/scored arrays if needed
            if (fitness.Length != currentPopulation.Count)
            {
                fitness = new double[currentPopulation.Count];
                scored = new bool[currentPopulation.Count];
            }

            // Advance generation
            _tracker.NextGeneration();
            generation++;
        }

        // Build result
        var result = BuildResult(
            championGenome, championFitness, championGeneration,
            currentPopulation, species, completedGenerations, seed, wasCancelled,
            generationHistory);

        TrainingLog.RunCompleted(
            _logger, completedGenerations, result.Champion.Fitness,
            result.Champion.Generation, wasCancelled);

        return result;
    }

    /// <summary>
    /// Builds phenotypes from genomes, evaluates them via the evaluation strategy,
    /// and handles per-genome and population-level evaluation failures.
    /// </summary>
    private async Task EvaluatePopulationAsync(
        IEvaluationStrategy evaluator,
        List<Genome> population,
        double[] fitness,
        bool[] scored,
        EvaluationErrorMode errorMode,
        double errorFitness)
    {
        // Build phenotypes — genomes that can't be built (e.g., cycles) get a
        // zero-output placeholder and will receive 0 fitness.
        var phenotypes = new IGenome[population.Count];
        Array.Fill(fitness, 0.0);
        for (int i = 0; i < population.Count; i++)
        {
            try
            {
                phenotypes[i] = _networkBuilder.Build(population[i]);
            }
            catch (Exception)
            {
                phenotypes[i] = ZeroOutputGenome.Instance;
                // fitness[i] already 0.0
            }
        }

        // Track which genomes have been scored by the evaluation callback.
        // This allows the general Exception handler to preserve partial results
        // from batch evaluators (per the IBatchEvaluator error contract).
        if (scored.Length < population.Count)
            scored = new bool[population.Count];
        else
            Array.Fill(scored, false);

        // Evaluate — cancellation token is NOT passed to the evaluation strategy.
        // The current generation always completes fully; cancellation is only
        // checked at generation boundaries.
        try
        {
            await evaluator.EvaluatePopulationAsync(
                phenotypes,
                (index, score) =>
                {
                    fitness[index] = score;
                    scored[index] = true;
                },
                CancellationToken.None);
        }
        catch (EvaluationException evalEx)
        {
            // Per-genome failures collected by sequential adapters
            if (errorMode == EvaluationErrorMode.StopRun)
            {
                throw;
            }

            // AssignFitness mode: assign configured default and log each failure
            foreach (var (index, error) in evalEx.Errors)
            {
                fitness[index] = errorFitness;
                TrainingLog.EvaluationFailed(_logger, index, error.Message);
            }
        }
        catch (Exception ex)
        {
            // Population-level failure (e.g., from BatchAdapter).
            if (errorMode == EvaluationErrorMode.StopRun)
            {
                throw;
            }

            // AssignFitness mode: preserve fitness for genomes that were
            // already scored via the callback; assign default to the rest.
            for (int i = 0; i < fitness.Length; i++)
            {
                if (!scored[i])
                    fitness[i] = errorFitness;
            }
            TrainingLog.EvaluationFailed(_logger, -1, ex.Message);
        }
    }

    /// <summary>
    /// Assigns genomes to species, detects extinct species, and logs stagnation warnings.
    /// </summary>
    private void SpeciateAndLog(
        List<Genome> population,
        double[] fitness,
        List<Species> species,
        int generation)
    {
        var populationWithFitness = new List<(Genome Genome, double Fitness)>(population.Count);
        for (int i = 0; i < population.Count; i++)
        {
            populationWithFitness.Add((population[i], fitness[i]));
        }

        // Track species before speciation for extinction detection
        var previousSpeciesIds = new HashSet<int>(species.Count);
        foreach (var s in species)
        {
            previousSpeciesIds.Add(s.Id);
        }

        _speciationStrategy.Speciate(populationWithFitness, species);

        // Detect extinct species via HashSet lookup — O(prev + current)
        var currentSpeciesIds = new HashSet<int>(species.Count);
        foreach (var s in species)
            currentSpeciesIds.Add(s.Id);

        foreach (int prevId in previousSpeciesIds)
        {
            if (!currentSpeciesIds.Contains(prevId))
                TrainingLog.SpeciesExtinct(_logger, prevId, generation);
        }

        // Log stagnation warnings
        foreach (var s in species)
        {
            if (s.GenerationsSinceImprovement > _options.Selection.StagnationThreshold)
            {
                TrainingLog.StagnationDetected(_logger, s.Id, s.GenerationsSinceImprovement);
            }
        }
    }

    /// <summary>
    /// Checks whether any stopping criterion has been met.
    /// </summary>
    private bool ShouldStop(int completedGenerations, double championFitness, List<Species> species)
    {
        // MaxGenerations
        if (_options.Stopping.MaxGenerations.HasValue
            && completedGenerations >= _options.Stopping.MaxGenerations.Value)
        {
            return true;
        }

        // FitnessTarget
        if (_options.Stopping.FitnessTarget.HasValue
            && championFitness >= _options.Stopping.FitnessTarget.Value)
        {
            return true;
        }

        // Run-level stagnation: all species simultaneously stagnant
        if (_options.Stopping.StagnationThreshold.HasValue && species.Count > 0)
        {
            bool allStagnant = true;
            foreach (var s in species)
            {
                if (s.GenerationsSinceImprovement <= _options.Stopping.StagnationThreshold.Value)
                {
                    allStagnant = false;
                    break;
                }
            }
            if (allStagnant)
            {
                return true;
            }
        }

        return false;
    }

    private static void UpdateChampion(
        List<Genome> population,
        double[] fitness,
        int generation,
        ref Genome? championGenome,
        ref double championFitness,
        ref int championGeneration)
    {
        for (int i = 0; i < population.Count; i++)
        {
            if (fitness[i] > championFitness)
            {
                championGenome = population[i];
                championFitness = fitness[i];
                championGeneration = generation;
            }
        }
    }

    private List<Genome> EnforceComplexityLimits(
        IReadOnlyList<(Genome Offspring, int SourceSpeciesId)> offspring,
        List<Species> species)
    {
        int? maxNodes = _options.Complexity.MaxNodes;
        int? maxConnections = _options.Complexity.MaxConnections;

        if (maxNodes is null && maxConnections is null)
        {
            var result = new List<Genome>(offspring.Count);
            foreach (var (genome, _) in offspring)
                result.Add(genome);
            return result;
        }

        // Build species lookup for same-species replacement
        var speciesById = new Dictionary<int, Species>(species.Count);
        foreach (var s in species)
        {
            speciesById[s.Id] = s;
        }

        var population = new List<Genome>(offspring.Count);
        foreach (var (genome, sourceSpeciesId) in offspring)
        {
            bool overLimit = false;
            if (maxNodes.HasValue && genome.Nodes.Count > maxNodes.Value)
                overLimit = true;
            if (maxConnections.HasValue && genome.Connections.Count > maxConnections.Value)
                overLimit = true;

            if (overLimit
                && speciesById.TryGetValue(sourceSpeciesId, out var parentSpecies)
                && parentSpecies.Members.Count > 0)
            {
                // Replace with the species champion (highest fitness member)
                var champion = parentSpecies.Members[0];
                for (int i = 1; i < parentSpecies.Members.Count; i++)
                {
                    if (parentSpecies.Members[i].Fitness > champion.Fitness)
                        champion = parentSpecies.Members[i];
                }
                population.Add(champion.Genome);
            }
            else
            {
                population.Add(genome);
            }
        }

        return population;
    }

    private EvolutionResult BuildResult(
        Genome? championGenome,
        double championFitness,
        int championGeneration,
        List<Genome> population,
        List<Species> species,
        int totalGenerations,
        int seed,
        bool wasCancelled,
        IReadOnlyList<GenerationStatistics>? generationHistory)
    {
        // Build champion
        IGenome championPhenotype;
        double resultFitness;
        int resultGeneration;

        if (championGenome is not null)
        {
            championPhenotype = _networkBuilder.Build(championGenome);
            resultFitness = championFitness;
            resultGeneration = championGeneration;
        }
        else
        {
            // No evaluation completed — use first genome from population
            championPhenotype = _networkBuilder.Build(population[0]);
            resultFitness = 0.0;
            resultGeneration = 0;
        }

        var champion = new Champion(championPhenotype, resultFitness, resultGeneration);

        // Build population snapshot
        var speciesSnapshots = new List<SpeciesSnapshot>(species.Count);
        int totalCount = 0;
        foreach (var s in species)
        {
            var members = new List<GenomeInfo>(s.Members.Count);
            foreach (var (genome, fitness) in s.Members)
            {
                members.Add(new GenomeInfo(fitness, genome.Nodes.Count, genome.Connections.Count));
            }
            speciesSnapshots.Add(new SpeciesSnapshot(s.Id, members));
            totalCount += s.Members.Count;
        }

        var populationSnapshot = new PopulationSnapshot(speciesSnapshots, totalCount);

        var history = new RunHistory(
            generationHistory ?? Array.Empty<GenerationStatistics>(),
            totalGenerations);

        return new EvolutionResult(champion, populationSnapshot, history, seed, wasCancelled);
    }

    private static void ComputeComplexityStats(
        List<Genome> population,
        out double avgNodes,
        out double avgConns)
    {
        if (population.Count == 0)
        {
            avgNodes = 0;
            avgConns = 0;
            return;
        }

        double totalNodes = 0;
        double totalConns = 0;
        foreach (var genome in population)
        {
            totalNodes += genome.Nodes.Count;
            totalConns += genome.Connections.Count;
        }
        avgNodes = totalNodes / population.Count;
        avgConns = totalConns / population.Count;
    }

    private static double GetMaxFitness(double[] fitness)
    {
        double max = double.NegativeInfinity;
        foreach (double f in fitness)
        {
            if (f > max) max = f;
        }
        return max;
    }

    private static double GetAverageFitness(double[] fitness)
    {
        if (fitness.Length == 0) return 0.0;
        double sum = 0.0;
        foreach (double f in fitness)
        {
            sum += f;
        }
        return sum / fitness.Length;
    }

    /// <summary>
    /// Placeholder phenotype for genomes that can't be built (e.g., cyclic topologies
    /// in feed-forward mode). Always outputs zeros so they naturally receive low fitness.
    /// </summary>
    private sealed class ZeroOutputGenome : IGenome
    {
        public static readonly ZeroOutputGenome Instance = new();
        public int NodeCount => 0;
        public int ConnectionCount => 0;
        public void Activate(ReadOnlySpan<double> inputs, Span<double> outputs) => outputs.Clear();
    }
}
