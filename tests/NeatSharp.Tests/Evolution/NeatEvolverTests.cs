using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeatSharp.Configuration;
using NeatSharp.Evaluation;
using NeatSharp.Evolution;
using NeatSharp.Evolution.Crossover;
using NeatSharp.Evolution.Mutation;
using NeatSharp.Evolution.Selection;
using NeatSharp.Evolution.Speciation;
using NeatSharp.Genetics;
using NeatSharp.Reporting;
using Xunit;

namespace NeatSharp.Tests.Evolution;

public class NeatEvolverTests
{
    #region Helper Methods

    private static NeatSharpOptions DefaultOptions(Action<NeatSharpOptions>? configure = null)
    {
        var options = new NeatSharpOptions
        {
            InputCount = 2,
            OutputCount = 1,
            PopulationSize = 20,
            Seed = 42,
            EnableMetrics = false,
        };
        options.Stopping.MaxGenerations = 10;
        configure?.Invoke(options);
        return options;
    }

    private static NeatEvolver CreateEvolver(
        NeatSharpOptions options,
        IInnovationTracker? tracker = null,
        ILogger<NeatEvolver>? logger = null)
    {
        var opts = Options.Create(options);
        int startNodeId = options.InputCount + 1 + options.OutputCount;
        var actualTracker = tracker ?? new InnovationTracker(startNodeId: startNodeId);
        return new NeatEvolver(
            opts,
            new PopulationFactory(opts),
            new FeedForwardNetworkBuilder(new ActivationFunctionRegistry()),
            new CompatibilitySpeciation(opts, new CompatibilityDistance(opts)),
            CreateOrchestrator(opts),
            actualTracker,
            logger ?? NullLogger<NeatEvolver>.Instance);
    }

    private static ReproductionOrchestrator CreateOrchestrator(IOptions<NeatSharpOptions> opts)
    {
        var wp = new WeightPerturbationMutation(opts);
        var wr = new WeightReplacementMutation(opts);
        var ac = new AddConnectionMutation(opts);
        var an = new AddNodeMutation(opts);
        var te = new ToggleEnableMutation();
        var composite = new CompositeMutationOperator(opts, wp, wr, ac, an, te);
        var parentSelector = new TournamentSelector(opts);
        var allocator = new ReproductionAllocator(opts);
        return new ReproductionOrchestrator(opts, parentSelector, new NeatCrossover(opts), composite, allocator);
    }

    private static IEvaluationStrategy ConstantFitnessStrategy(double fitness)
    {
        return EvaluationStrategy.FromFunction(_ => fitness);
    }

    private static IEvaluationStrategy IncrementingFitnessStrategy()
    {
        int callCount = 0;
        return EvaluationStrategy.FromFunction(_ =>
        {
            // Each call returns a slightly higher base fitness
            return 1.0 + Interlocked.Increment(ref callCount) * 0.001;
        });
    }

    #endregion

    #region T006a — Core Loop Unit Tests

    [Fact]
    public async Task RunAsync_ExecutesEvaluateSpeciateReproduceInCorrectOrder()
    {
        // Arrange: Run for a few generations and verify the result shows
        // speciated population and evolving champion (proves full loop ran)
        var options = DefaultOptions(o => o.Stopping.MaxGenerations = 5);
        var evolver = CreateEvolver(options);

        // Act
        var result = await evolver.RunAsync(IncrementingFitnessStrategy());

        // Assert: result should show completed generations with species
        result.History.TotalGenerations.Should().Be(5);
        result.Population.Species.Should().NotBeEmpty();
        result.Champion.Fitness.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunAsync_StopsAtMaxGenerations()
    {
        // Arrange
        var options = DefaultOptions(o => o.Stopping.MaxGenerations = 3);
        var evolver = CreateEvolver(options);

        // Act
        var result = await evolver.RunAsync(ConstantFitnessStrategy(1.0));

        // Assert
        result.History.TotalGenerations.Should().Be(3);
        result.WasCancelled.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_StopsWhenFitnessTargetMet()
    {
        // Arrange
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 100;
            o.Stopping.FitnessTarget = 1.5;
        });
        var evolver = CreateEvolver(options);

        // Act: fitness increases each generation so it should stop before 100
        var result = await evolver.RunAsync(IncrementingFitnessStrategy());

        // Assert
        result.History.TotalGenerations.Should().BeLessThan(100);
        result.Champion.Fitness.Should().BeGreaterThanOrEqualTo(1.5);
        result.WasCancelled.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_StopsWhenAllSpeciesStagnant()
    {
        // Arrange: set a low stagnation threshold so it triggers quickly
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 200;
            o.Stopping.StagnationThreshold = 2; // Stop when all species stagnant for 2+ gens
        });
        var evolver = CreateEvolver(options);

        // Act: constant fitness means species never improve → stagnation builds up
        var result = await evolver.RunAsync(ConstantFitnessStrategy(1.0));

        // Assert: should stop due to stagnation before MaxGenerations
        result.History.TotalGenerations.Should().BeLessThan(200);
        result.WasCancelled.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_ChampionTracksHighestFitnessAcrossAllGenerations()
    {
        // Arrange
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 5;
            o.PopulationSize = 10;
        });
        var evolver = CreateEvolver(options);

        // Act: fitness is always positive, champion should be tracked
        var result = await evolver.RunAsync(IncrementingFitnessStrategy());

        // Assert: champion should have the highest fitness seen across all gens
        result.Champion.Fitness.Should().BeGreaterThan(0);
        result.Champion.Generation.Should().BeGreaterThanOrEqualTo(0);
        result.Champion.Generation.Should().BeLessThan(5);
        result.Champion.Genome.Should().NotBeNull();
    }

    [Fact]
    public async Task RunAsync_ChampionGenerationReflectsWhenDiscovered()
    {
        // Arrange: Use a fitness function that gives higher fitness in later generations
        int evalCount = 0;
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 5;
            o.PopulationSize = 10;
        });
        var evolver = CreateEvolver(options);

        var strategy = EvaluationStrategy.FromFunction(_ =>
        {
            int count = Interlocked.Increment(ref evalCount);
            // Genomes in later generations (higher eval counts) get higher fitness
            return count * 0.01;
        });

        // Act
        var result = await evolver.RunAsync(strategy);

        // Assert: champion should be from a later generation (not gen 0)
        result.Champion.Generation.Should().BeGreaterThan(0,
            "champion should be discovered in a later generation where fitness is higher");
    }

    #endregion

    #region T006b — Cancellation and Determinism Unit Tests

    [Fact]
    public async Task RunAsync_Cancellation_ReturnsPartialResultWithWasCancelledTrue()
    {
        // Arrange
        var options = DefaultOptions(o => o.Stopping.MaxGenerations = 100);
        var evolver = CreateEvolver(options);
        using var cts = new CancellationTokenSource();

        int evalCount = 0;
        var strategy = EvaluationStrategy.FromFunction(genome =>
        {
            int count = Interlocked.Increment(ref evalCount);
            if (count > 50) // Cancel after evaluating ~50 genomes (a few generations)
            {
                cts.Cancel();
            }
            return 1.0;
        });

        // Act
        var result = await evolver.RunAsync(strategy, cts.Token);

        // Assert
        result.WasCancelled.Should().BeTrue();
        result.Champion.Should().NotBeNull();
        result.History.TotalGenerations.Should().BeLessThan(100);
    }

    [Fact]
    public async Task RunAsync_CancellationBeforeAnyGeneration_ReturnsValidResult()
    {
        // Arrange: cancel immediately
        var options = DefaultOptions(o => o.Stopping.MaxGenerations = 100);
        var evolver = CreateEvolver(options);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel before RunAsync starts

        // Act
        var result = await evolver.RunAsync(ConstantFitnessStrategy(1.0), cts.Token);

        // Assert
        result.WasCancelled.Should().BeTrue();
        result.Champion.Should().NotBeNull();
        result.Champion.Fitness.Should().BeGreaterThanOrEqualTo(0);
        result.History.TotalGenerations.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_SameSeed_ProducesIdenticalResults()
    {
        // Arrange
        var options1 = DefaultOptions(o =>
        {
            o.Seed = 12345;
            o.Stopping.MaxGenerations = 10;
            o.PopulationSize = 20;
        });
        var options2 = DefaultOptions(o =>
        {
            o.Seed = 12345;
            o.Stopping.MaxGenerations = 10;
            o.PopulationSize = 20;
        });

        var evolver1 = CreateEvolver(options1);
        var evolver2 = CreateEvolver(options2);

        static double XorFitness(IGenome genome)
        {
            double fitness = 0;
            Span<double> output = stackalloc double[1];
            double[][] inputs = [[0, 0], [0, 1], [1, 0], [1, 1]];
            double[] expected = [0, 1, 1, 0];
            for (int i = 0; i < 4; i++)
            {
                genome.Activate(inputs[i], output);
                fitness += 1.0 - Math.Abs(expected[i] - output[0]);
            }
            return fitness;
        }

        var strategy1 = EvaluationStrategy.FromFunction(XorFitness);
        var strategy2 = EvaluationStrategy.FromFunction(XorFitness);

        // Act
        var result1 = await evolver1.RunAsync(strategy1);
        var result2 = await evolver2.RunAsync(strategy2);

        // Assert
        result1.Seed.Should().Be(result2.Seed);
        result1.Champion.Fitness.Should().Be(result2.Champion.Fitness);
        result1.Champion.Generation.Should().Be(result2.Champion.Generation);
        result1.History.TotalGenerations.Should().Be(result2.History.TotalGenerations);
    }

    [Fact]
    public async Task RunAsync_InnovationTrackerNextGenerationCalledBetweenGenerations()
    {
        // Arrange: Use a counting tracker wrapper
        var options = DefaultOptions(o => o.Stopping.MaxGenerations = 3);
        int startNodeId = options.InputCount + 1 + options.OutputCount;
        var innerTracker = new InnovationTracker(startNodeId: startNodeId);
        var countingTracker = new NextGenerationCountingTracker(innerTracker);
        var evolver = CreateEvolver(options, countingTracker);

        // Act
        await evolver.RunAsync(ConstantFitnessStrategy(1.0));

        // Assert: NextGeneration should be called:
        // 1 time after population init + 2 times between generations (for 3 gen run)
        // = 1 (init) + (maxGen - 1) (between gens) = 1 + 2 = 3
        countingTracker.NextGenerationCallCount.Should().BeGreaterThanOrEqualTo(3);
    }

    private sealed class NextGenerationCountingTracker : IInnovationTracker
    {
        private readonly IInnovationTracker _inner;
        public int NextGenerationCallCount { get; private set; }

        public NextGenerationCountingTracker(IInnovationTracker inner) => _inner = inner;

        public int GetConnectionInnovation(int sourceNodeId, int targetNodeId)
            => _inner.GetConnectionInnovation(sourceNodeId, targetNodeId);

        public NodeSplitResult GetNodeSplitInnovation(int connectionInnovation)
            => _inner.GetNodeSplitInnovation(connectionInnovation);

        public void NextGeneration()
        {
            NextGenerationCallCount++;
            _inner.NextGeneration();
        }
    }

    #endregion

    #region T006c — Error Handling and Edge Case Unit Tests

    [Fact]
    public async Task RunAsync_EvaluationFailure_AssignsZeroFitnessAndContinues()
    {
        // Arrange
        int evalCount = 0;
        var options = DefaultOptions(o => o.Stopping.MaxGenerations = 3);
        var evolver = CreateEvolver(options);

        var strategy = EvaluationStrategy.FromFunction(genome =>
        {
            int count = Interlocked.Increment(ref evalCount);
            if (count == 1) throw new InvalidOperationException("Simulated failure");
            return 1.0;
        });

        // Act
        var result = await evolver.RunAsync(strategy);

        // Assert: run should complete despite the evaluation failure
        result.History.TotalGenerations.Should().Be(3);
        result.Champion.Should().NotBeNull();
    }

    [Fact]
    public async Task RunAsync_ComplexityLimits_ReplacesOverLimitGenomes()
    {
        // Arrange: set very tight complexity limits
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 10;
            o.PopulationSize = 20;
            o.Complexity.MaxNodes = 4;  // initial genome has 4 nodes (2in+1bias+1out)
            o.Complexity.MaxConnections = 3; // initial has 3 connections
        });
        var evolver = CreateEvolver(options);

        // Act: run should complete — any mutated genome exceeding limits gets replaced
        var result = await evolver.RunAsync(IncrementingFitnessStrategy());

        // Assert: run completes successfully
        result.History.TotalGenerations.Should().Be(10);
        result.Champion.Should().NotBeNull();
    }

    [Fact]
    public async Task RunAsync_PreservesFittestSpeciesWhenStagnationEliminatesAll()
    {
        // Arrange: Configure per-species stagnation penalty but NO run-level stagnation stopping
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 30;
            o.Stopping.StagnationThreshold = null; // No run-level stagnation stopping
            o.Selection.StagnationThreshold = 3;   // Per-species penalty kicks in after 3 gens
            o.PopulationSize = 20;
        });
        var evolver = CreateEvolver(options);

        // Act: constant fitness → all species become stagnant, but allocator preserves top species
        var result = await evolver.RunAsync(ConstantFitnessStrategy(1.0));

        // Assert: run completes at MaxGenerations without crash
        result.History.TotalGenerations.Should().Be(30);
        result.Champion.Should().NotBeNull();
    }

    [Fact]
    public async Task RunAsync_ZeroFitnessPopulation_ProceedsWithoutCrash()
    {
        // Arrange
        var options = DefaultOptions(o => o.Stopping.MaxGenerations = 3);
        var evolver = CreateEvolver(options);

        // Act
        var result = await evolver.RunAsync(ConstantFitnessStrategy(0.0));

        // Assert
        result.History.TotalGenerations.Should().Be(3);
        result.Champion.Fitness.Should().Be(0.0);
    }

    [Fact]
    public async Task RunAsync_SingleSpeciesPopulation_ContinuesNormally()
    {
        // Arrange: small population likely stays in one species
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 5;
            o.PopulationSize = 5;
            o.Speciation.CompatibilityThreshold = 100.0; // High threshold → all in one species
        });
        var evolver = CreateEvolver(options);

        // Act
        var result = await evolver.RunAsync(ConstantFitnessStrategy(1.0));

        // Assert
        result.History.TotalGenerations.Should().Be(5);
        result.Population.Species.Should().HaveCount(1);
    }

    [Fact]
    public async Task RunAsync_MaxGenerationsOne_ReturnsValidResult()
    {
        // Arrange
        var options = DefaultOptions(o => o.Stopping.MaxGenerations = 1);
        var evolver = CreateEvolver(options);

        // Act
        var result = await evolver.RunAsync(ConstantFitnessStrategy(2.0));

        // Assert
        result.History.TotalGenerations.Should().Be(1);
        result.Champion.Fitness.Should().Be(2.0);
        result.Champion.Generation.Should().Be(0);
        result.WasCancelled.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_Result_ContainsValidPopulationSnapshot()
    {
        // Arrange
        var options = DefaultOptions(o => o.Stopping.MaxGenerations = 3);
        var evolver = CreateEvolver(options);

        // Act
        var result = await evolver.RunAsync(ConstantFitnessStrategy(1.0));

        // Assert
        result.Population.Should().NotBeNull();
        result.Population.Species.Should().NotBeEmpty();
        result.Population.TotalCount.Should().BeGreaterThan(0);

        foreach (var species in result.Population.Species)
        {
            species.Members.Should().NotBeEmpty();
            foreach (var member in species.Members)
            {
                member.NodeCount.Should().BeGreaterThan(0);
                member.ConnectionCount.Should().BeGreaterThan(0);
            }
        }
    }

    [Fact]
    public async Task RunAsync_Result_ContainsSeedUsed()
    {
        // Arrange
        var options = DefaultOptions(o =>
        {
            o.Seed = 99;
            o.Stopping.MaxGenerations = 1;
        });
        var evolver = CreateEvolver(options);

        // Act
        var result = await evolver.RunAsync(ConstantFitnessStrategy(1.0));

        // Assert
        result.Seed.Should().Be(99);
    }

    [Fact]
    public async Task RunAsync_NullSeed_AutoGeneratesAndRecordsSeed()
    {
        // Arrange
        var options = DefaultOptions(o =>
        {
            o.Seed = null;
            o.Stopping.MaxGenerations = 1;
        });
        var evolver = CreateEvolver(options);

        // Act
        var result = await evolver.RunAsync(ConstantFitnessStrategy(1.0));

        // Assert: seed should be recorded (non-negative int)
        result.Seed.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RunAsync_EvaluationThrowsForAllGenomes_ContinuesWithZeroFitness()
    {
        // Arrange: evaluation always throws
        var options = DefaultOptions(o => o.Stopping.MaxGenerations = 2);
        var evolver = CreateEvolver(options);

        var strategy = EvaluationStrategy.FromFunction(
            (IGenome _) => throw new InvalidOperationException("Always fails"));

        // Act
        var result = await evolver.RunAsync(strategy);

        // Assert: run completes with zero fitness champion
        result.History.TotalGenerations.Should().Be(2);
        result.Champion.Fitness.Should().Be(0.0);
    }

    #endregion

    #region T010 — Metrics, Zero-Overhead, and Structured Logging Tests

    [Fact]
    public async Task RunAsync_MetricsEnabled_RecordsGenerationStatisticsEachGeneration()
    {
        // Arrange
        var options = DefaultOptions(o =>
        {
            o.EnableMetrics = true;
            o.Stopping.MaxGenerations = 5;
        });
        var evolver = CreateEvolver(options);

        // Act
        var result = await evolver.RunAsync(IncrementingFitnessStrategy());

        // Assert
        result.History.Generations.Should().HaveCount(5);
        for (int i = 0; i < 5; i++)
        {
            var gen = result.History.Generations[i];
            gen.Generation.Should().Be(i);
            gen.BestFitness.Should().BeGreaterThanOrEqualTo(0);
            gen.AverageFitness.Should().BeGreaterThanOrEqualTo(0);
            gen.SpeciesCount.Should().BeGreaterThan(0);
            gen.SpeciesSizes.Should().NotBeEmpty();
            gen.SpeciesSizes.Should().HaveCount(gen.SpeciesCount);
            gen.SpeciesSizes.Should().AllSatisfy(size => size.Should().BeGreaterThan(0));
            gen.Complexity.AverageNodes.Should().BeGreaterThan(0);
            gen.Complexity.AverageConnections.Should().BeGreaterThan(0);
            gen.Timing.Evaluation.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
            gen.Timing.Speciation.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
            gen.Timing.Reproduction.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
        }
    }

    [Fact]
    public async Task RunAsync_MetricsDisabled_GenerationsListIsEmpty()
    {
        // Arrange
        var options = DefaultOptions(o =>
        {
            o.EnableMetrics = false;
            o.Stopping.MaxGenerations = 3;
        });
        var evolver = CreateEvolver(options);

        // Act
        var result = await evolver.RunAsync(ConstantFitnessStrategy(1.0));

        // Assert
        result.History.Generations.Should().BeEmpty();
        result.History.TotalGenerations.Should().Be(3);
    }

    [Fact]
    public async Task RunAsync_TotalGenerationsCorrect_RegardlessOfMetricsSetting()
    {
        // Arrange
        var optionsEnabled = DefaultOptions(o =>
        {
            o.EnableMetrics = true;
            o.Stopping.MaxGenerations = 5;
            o.Seed = 42;
        });
        var optionsDisabled = DefaultOptions(o =>
        {
            o.EnableMetrics = false;
            o.Stopping.MaxGenerations = 5;
            o.Seed = 42;
        });
        var evolver1 = CreateEvolver(optionsEnabled);
        var evolver2 = CreateEvolver(optionsDisabled);

        // Act
        var result1 = await evolver1.RunAsync(ConstantFitnessStrategy(1.0));
        var result2 = await evolver2.RunAsync(ConstantFitnessStrategy(1.0));

        // Assert
        result1.History.TotalGenerations.Should().Be(5);
        result2.History.TotalGenerations.Should().Be(5);
    }

    [Fact]
    public async Task RunAsync_StructuredLogEvents_CoreEventsEmitted()
    {
        // Arrange
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 3;
        });
        var logger = new CapturingLogger();
        var evolver = CreateEvolver(options, logger: logger);

        // Act
        await evolver.RunAsync(IncrementingFitnessStrategy());

        // Assert
        // GenerationCompleted (1001) emitted once per generation
        logger.LogEntries.Count(e => e.EventId.Id == 1001).Should().Be(3);

        // NewBestFitness (1002) emitted at least once (gen 0 always sets first champion)
        logger.LogEntries.Count(e => e.EventId.Id == 1002).Should().BeGreaterThanOrEqualTo(1);

        // RunCompleted (1005) emitted exactly once
        logger.LogEntries.Count(e => e.EventId.Id == 1005).Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_StructuredLogEvents_EvaluationFailedEmitted()
    {
        // Arrange
        var options = DefaultOptions(o => o.Stopping.MaxGenerations = 2);
        var logger = new CapturingLogger();
        var evolver = CreateEvolver(options, logger: logger);

        var strategy = EvaluationStrategy.FromFunction(
            (IGenome _) => throw new InvalidOperationException("Test failure"));

        // Act
        await evolver.RunAsync(strategy);

        // Assert: EvaluationFailed (1006) should be logged
        logger.LogEntries.Should().Contain(e => e.EventId.Id == 1006);
    }

    [Fact]
    public async Task RunAsync_StructuredLogEvents_StagnationDetectedEmitted()
    {
        // Arrange: configure low stagnation threshold with constant fitness
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 20;
            o.Selection.StagnationThreshold = 3;
            o.Stopping.StagnationThreshold = null; // Don't stop on run-level stagnation
        });
        var logger = new CapturingLogger();
        var evolver = CreateEvolver(options, logger: logger);

        // Act: constant fitness → species never improve → stagnation builds
        await evolver.RunAsync(ConstantFitnessStrategy(1.0));

        // Assert: StagnationDetected (1004) should be logged
        logger.LogEntries.Should().Contain(e => e.EventId.Id == 1004);
    }

    [Fact]
    public async Task RunAsync_HumanReadableSummary_ContainsExpectedFields()
    {
        // Arrange
        var options = DefaultOptions(o =>
        {
            o.Seed = 42;
            o.Stopping.MaxGenerations = 3;
        });
        var evolver = CreateEvolver(options);
        var reporter = new RunReporter();

        // Act
        var result = await evolver.RunAsync(IncrementingFitnessStrategy());
        var summary = reporter.GenerateSummary(result);

        // Assert
        summary.Should().Contain("Champion Fitness:");
        summary.Should().Contain("Champion Generation:");
        summary.Should().Contain("Total Generations:");
        summary.Should().Contain("Species Count:");
        summary.Should().Contain("Seed: 42");
    }

    private sealed class CapturingLogger : ILogger<NeatEvolver>
    {
        public List<(LogLevel Level, EventId EventId, string Message)> LogEntries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogEntries.Add((logLevel, eventId, formatter(state, exception)));
        }
    }

    #endregion

    #region T013 — Environment Evaluator Integration Tests

    [Fact]
    public async Task RunAsync_EnvironmentEvaluator_EvaluatesGenomesThroughEnvironment()
    {
        // Arrange: Create a mock environment evaluator that runs a fixed number of steps,
        // scores genome outputs, and returns cumulative reward
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 5;
            o.PopulationSize = 20;
        });
        var evolver = CreateEvolver(options);

        var environment = new StepBasedEnvironmentEvaluator(steps: 10);
        var strategy = EvaluationStrategy.FromEnvironment(environment);

        // Act
        var result = await evolver.RunAsync(strategy);

        // Assert: every genome should have been evaluated through the environment
        environment.EvaluationCount.Should().BeGreaterThanOrEqualTo(options.PopulationSize,
            "at least the initial population should be evaluated through the environment");
        result.History.TotalGenerations.Should().Be(5);
        result.Champion.Fitness.Should().BeGreaterThan(0,
            "champion fitness should reflect environment performance");
    }

    [Fact]
    public async Task RunAsync_EnvironmentEvaluator_ChampionFitnessReflectsEnvironmentPerformance()
    {
        // Arrange: environment that gives cumulative reward based on genome outputs
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 10;
            o.PopulationSize = 20;
        });
        var evolver = CreateEvolver(options);

        var environment = new StepBasedEnvironmentEvaluator(steps: 5);
        var strategy = EvaluationStrategy.FromEnvironment(environment);

        // Act
        var result = await evolver.RunAsync(strategy);

        // Assert: champion fitness should be in the valid range for the environment
        // Each step gives reward in [0, 1], so max fitness = steps = 5
        result.Champion.Fitness.Should().BeGreaterThan(0,
            "champion should have positive fitness from environment evaluation");
        result.Champion.Fitness.Should().BeLessThanOrEqualTo(5.0,
            "fitness should not exceed maximum possible cumulative reward");
        result.Champion.Genome.Should().NotBeNull();
        result.Champion.Generation.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RunAsync_EnvironmentEvaluator_CompletesWithValidResult()
    {
        // Arrange
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 3;
            o.PopulationSize = 10;
        });
        var evolver = CreateEvolver(options);

        var environment = new StepBasedEnvironmentEvaluator(steps: 8);
        var strategy = EvaluationStrategy.FromEnvironment(environment);

        // Act
        var result = await evolver.RunAsync(strategy);

        // Assert: full EvolutionResult structure should be valid
        result.Should().NotBeNull();
        result.WasCancelled.Should().BeFalse();
        result.History.TotalGenerations.Should().Be(3);
        result.Champion.Should().NotBeNull();
        result.Champion.Genome.Should().NotBeNull();
        result.Population.Should().NotBeNull();
        result.Population.Species.Should().NotBeEmpty();
        result.Seed.Should().Be(42);
    }

    /// <summary>
    /// Mock environment evaluator that runs a fixed number of steps.
    /// Each step feeds the current step index (normalized) as input to the genome,
    /// reads the output, and accumulates reward based on the output value.
    /// </summary>
    private sealed class StepBasedEnvironmentEvaluator(int steps) : IEnvironmentEvaluator
    {
        private int _evaluationCount;
        public int EvaluationCount => _evaluationCount;

        public Task<double> EvaluateAsync(IGenome genome, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _evaluationCount);

            double totalReward = 0;
            Span<double> outputs = stackalloc double[1];

            for (int step = 0; step < steps; step++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Feed normalized step index as input
                double input1 = (double)step / steps;
                double input2 = 1.0 - input1;
                genome.Activate([input1, input2], outputs);

                // Reward is the clamped output value [0, 1]
                totalReward += Math.Clamp(outputs[0], 0.0, 1.0);
            }

            return Task.FromResult(totalReward);
        }
    }

    #endregion

    #region T014 — Batch Evaluator Integration Tests

    [Fact]
    public async Task RunAsync_BatchEvaluator_ReceivesFullPopulationInSingleCall()
    {
        // Arrange: Create a mock batch evaluator that tracks how many genomes it receives per call
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 3;
            o.PopulationSize = 20;
        });
        var evolver = CreateEvolver(options);

        var batchEvaluator = new TrackingBatchEvaluator();
        var strategy = EvaluationStrategy.FromBatch(batchEvaluator);

        // Act
        var result = await evolver.RunAsync(strategy);

        // Assert: each call should receive the full population
        batchEvaluator.CallCount.Should().BeGreaterThanOrEqualTo(3,
            "batch evaluator should be called once per generation");
        batchEvaluator.GenomesPerCall.Should().AllSatisfy(count =>
            count.Should().Be(options.PopulationSize,
                "each batch call should receive the full population"));
    }

    [Fact]
    public async Task RunAsync_BatchEvaluator_AllGenomesGetAssignedFitnessScores()
    {
        // Arrange: batch evaluator assigns unique fitness based on genome index
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 5;
            o.PopulationSize = 15;
        });
        var evolver = CreateEvolver(options);

        var batchEvaluator = new TrackingBatchEvaluator();
        var strategy = EvaluationStrategy.FromBatch(batchEvaluator);

        // Act
        var result = await evolver.RunAsync(strategy);

        // Assert: all genomes should have been scored (setFitness called for every index)
        batchEvaluator.FitnessAssignments.Should().AllSatisfy(assignments =>
            assignments.Should().HaveCount(options.PopulationSize,
                "every genome should receive a fitness score"));
        result.Champion.Fitness.Should().BeGreaterThan(0,
            "champion should have a positive fitness from batch evaluation");
    }

    [Fact]
    public async Task RunAsync_BatchEvaluator_TrainingLoopProceedsWithBatchAssignedScores()
    {
        // Arrange: batch evaluator assigns increasing scores by index to drive evolution
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 10;
            o.PopulationSize = 20;
        });
        var evolver = CreateEvolver(options);

        var batchEvaluator = new TrackingBatchEvaluator();
        var strategy = EvaluationStrategy.FromBatch(batchEvaluator);

        // Act
        var result = await evolver.RunAsync(strategy);

        // Assert: speciation and reproduction occurred (multiple generations with species)
        result.History.TotalGenerations.Should().Be(10);
        result.Population.Species.Should().NotBeEmpty(
            "speciation should have assigned genomes to species using batch-assigned scores");
        result.Champion.Fitness.Should().BeGreaterThan(0,
            "champion fitness should reflect batch-assigned scores");
        result.Champion.Generation.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task RunAsync_BatchEvaluator_CompletesWithValidEvolutionResult()
    {
        // Arrange
        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = 3;
            o.PopulationSize = 10;
        });
        var evolver = CreateEvolver(options);

        var batchEvaluator = new TrackingBatchEvaluator();
        var strategy = EvaluationStrategy.FromBatch(batchEvaluator);

        // Act
        var result = await evolver.RunAsync(strategy);

        // Assert: full EvolutionResult structure should be valid
        result.Should().NotBeNull();
        result.WasCancelled.Should().BeFalse();
        result.History.TotalGenerations.Should().Be(3);
        result.Champion.Should().NotBeNull();
        result.Champion.Genome.Should().NotBeNull();
        result.Population.Should().NotBeNull();
        result.Population.Species.Should().NotBeEmpty();
        result.Population.TotalCount.Should().BeGreaterThan(0);
        result.Seed.Should().Be(42);
    }

    /// <summary>
    /// Mock batch evaluator that receives the full population in a single call
    /// and assigns fitness scores based on genome index. Tracks call counts
    /// and per-call genome counts for assertions.
    /// </summary>
    private sealed class TrackingBatchEvaluator : IBatchEvaluator
    {
        private int _callCount;
        private readonly List<int> _genomesPerCall = [];
        private readonly List<List<int>> _fitnessAssignments = [];

        public int CallCount => _callCount;
        public IReadOnlyList<int> GenomesPerCall => _genomesPerCall;
        public IReadOnlyList<IReadOnlyList<int>> FitnessAssignments => _fitnessAssignments;

        public Task EvaluateAsync(
            IReadOnlyList<IGenome> genomes,
            Action<int, double> setFitness,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);

            var assignedIndices = new List<int>();
            lock (_genomesPerCall)
            {
                _genomesPerCall.Add(genomes.Count);
            }

            for (int i = 0; i < genomes.Count; i++)
            {
                // Assign fitness based on node count * (index + 1) to create variation
                double fitness = (i + 1) * 0.1 + genomes[i].NodeCount * 0.01;
                setFitness(i, fitness);
                assignedIndices.Add(i);
            }

            lock (_fitnessAssignments)
            {
                _fitnessAssignments.Add(assignedIndices);
            }

            return Task.CompletedTask;
        }
    }

    #endregion
}
