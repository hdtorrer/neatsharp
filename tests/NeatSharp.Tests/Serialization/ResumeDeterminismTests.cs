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
using NeatSharp.Exceptions;
using NeatSharp.Genetics;
using NeatSharp.Serialization;
using Xunit;

namespace NeatSharp.Tests.Serialization;

public class ResumeDeterminismTests
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

    private static IEvaluationStrategy XorFitnessStrategy()
    {
        return EvaluationStrategy.FromFunction(XorFitness);
    }

    private static double XorFitness(IGenome genome)
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

    #endregion

    #region T038 — Deterministic Resume Tests

    [Fact]
    public async Task RunAsync_ResumeFromCheckpoint_ProducesIdenticalResultsToUninterruptedRun()
    {
        // Arrange: common configuration
        const int totalGenerations = 10;
        const int splitGeneration = 5; // checkpoint captured after gen 4 (Generation=5)

        var fullRunOptions = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = totalGenerations;
        });

        // === Run 1: Full uninterrupted run ===
        var fullEvolver = CreateEvolver(fullRunOptions);
        var fullResult = await fullEvolver.RunAsync(XorFitnessStrategy());

        // === Run 2: Split run (first half + resume) ===

        // First half: run with OnCheckpoint, capture checkpoint at splitGeneration
        var firstHalfOptions = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = totalGenerations;
        });
        var firstHalfEvolver = CreateEvolver(firstHalfOptions);

        TrainingCheckpoint? capturedCheckpoint = null;
        using var cts = new CancellationTokenSource();

        var runOptions = new EvolutionRunOptions
        {
            OnCheckpoint = (checkpoint, ct) =>
            {
                if (checkpoint.Generation == splitGeneration)
                {
                    capturedCheckpoint = checkpoint;
                    cts.Cancel(); // Stop the run after capturing the checkpoint
                }
                return Task.CompletedTask;
            }
        };

        var firstHalfResult = await firstHalfEvolver.RunAsync(XorFitnessStrategy(), runOptions, cts.Token);

        // Verify checkpoint was captured
        capturedCheckpoint.Should().NotBeNull(
            "the OnCheckpoint callback should have captured a checkpoint at generation {0}", splitGeneration);
        capturedCheckpoint!.Generation.Should().Be(splitGeneration);

        // Resume: new evolver instance, same config, resume from checkpoint
        var resumeOptions = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = totalGenerations;
        });
        var resumeEvolver = CreateEvolver(resumeOptions);

        var resumeRunOptions = new EvolutionRunOptions
        {
            ResumeFrom = capturedCheckpoint
        };

        var resumeResult = await resumeEvolver.RunAsync(XorFitnessStrategy(), resumeRunOptions);

        // Assert: both runs should produce identical results
        resumeResult.Champion.Fitness.Should().Be(fullResult.Champion.Fitness,
            "resumed run champion fitness should match the uninterrupted run");
        resumeResult.Champion.Generation.Should().Be(fullResult.Champion.Generation,
            "resumed run champion generation should match the uninterrupted run");
        resumeResult.History.TotalGenerations.Should().Be(fullResult.History.TotalGenerations,
            "resumed run should complete the same total number of generations");
        resumeResult.WasCancelled.Should().BeFalse();
        resumeResult.Seed.Should().Be(fullResult.Seed);

        // Verify champion genome identity (same nodes and connections)
        var fullChampionGenome = fullResult.Champion.Genome;
        var resumeChampionGenome = resumeResult.Champion.Genome;
        fullChampionGenome.NodeCount.Should().Be(resumeChampionGenome.NodeCount,
            "champion genomes should have the same number of nodes");
        fullChampionGenome.ConnectionCount.Should().Be(resumeChampionGenome.ConnectionCount,
            "champion genomes should have the same number of connections");
    }

    [Fact]
    public async Task RunAsync_CancellationDuringResumedRun_StopsAtGenerationBoundaryWithValidResult()
    {
        // Arrange: first create a checkpoint by running a few generations
        const int totalGenerations = 20;
        const int splitGeneration = 3;

        var options = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = totalGenerations;
        });

        // Run first few generations to capture a checkpoint
        var firstEvolver = CreateEvolver(options);
        TrainingCheckpoint? capturedCheckpoint = null;
        using var firstCts = new CancellationTokenSource();

        var firstRunOptions = new EvolutionRunOptions
        {
            OnCheckpoint = (checkpoint, ct) =>
            {
                if (checkpoint.Generation == splitGeneration)
                {
                    capturedCheckpoint = checkpoint;
                    firstCts.Cancel();
                }
                return Task.CompletedTask;
            }
        };

        await firstEvolver.RunAsync(XorFitnessStrategy(), firstRunOptions, firstCts.Token);
        capturedCheckpoint.Should().NotBeNull();

        // Resume and cancel during the resumed run
        var resumeOptions = DefaultOptions(o =>
        {
            o.Stopping.MaxGenerations = totalGenerations;
        });
        var resumeEvolver = CreateEvolver(resumeOptions);

        using var resumeCts = new CancellationTokenSource();
        int evalCount = 0;
        var cancellingStrategy = EvaluationStrategy.FromFunction(genome =>
        {
            int count = Interlocked.Increment(ref evalCount);
            // Cancel after evaluating ~2 generations worth of genomes (after resume)
            if (count > resumeOptions.PopulationSize * 2)
            {
                resumeCts.Cancel();
            }
            return XorFitness(genome);
        });

        var resumeRunOptions = new EvolutionRunOptions
        {
            ResumeFrom = capturedCheckpoint
        };

        var result = await resumeEvolver.RunAsync(cancellingStrategy, resumeRunOptions, resumeCts.Token);

        // Assert: cancelled during resumed run
        result.WasCancelled.Should().BeTrue(
            "the resumed run should have been cancelled");
        result.Champion.Should().NotBeNull(
            "a valid champion should be returned even when cancelled");
        result.Champion.Fitness.Should().BeGreaterThan(0,
            "champion should have a positive fitness score");
        result.History.TotalGenerations.Should().BeGreaterThan(splitGeneration,
            "at least some generations should have completed after resume before cancellation");
        result.History.TotalGenerations.Should().BeLessThan(totalGenerations,
            "the run should not have completed all generations before cancellation");
    }

    [Fact]
    public async Task RunAsync_ConfigHashMismatchOnResume_ThrowsCheckpointException()
    {
        // Arrange: create a checkpoint with one config
        var originalOptions = DefaultOptions(o =>
        {
            o.PopulationSize = 20;
            o.Stopping.MaxGenerations = 10;
        });

        var firstEvolver = CreateEvolver(originalOptions);
        TrainingCheckpoint? capturedCheckpoint = null;
        using var cts = new CancellationTokenSource();

        var firstRunOptions = new EvolutionRunOptions
        {
            OnCheckpoint = (checkpoint, ct) =>
            {
                capturedCheckpoint = checkpoint;
                cts.Cancel(); // Capture first checkpoint and stop
                return Task.CompletedTask;
            }
        };

        await firstEvolver.RunAsync(XorFitnessStrategy(), firstRunOptions, cts.Token);
        capturedCheckpoint.Should().NotBeNull();

        // Try to resume with a DIFFERENT config (different PopulationSize)
        var mismatchedOptions = DefaultOptions(o =>
        {
            o.PopulationSize = 50; // Different from original 20
            o.Stopping.MaxGenerations = 10;
        });
        var mismatchedEvolver = CreateEvolver(mismatchedOptions);

        var resumeRunOptions = new EvolutionRunOptions
        {
            ResumeFrom = capturedCheckpoint
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<CheckpointException>(
            () => mismatchedEvolver.RunAsync(XorFitnessStrategy(), resumeRunOptions));

        exception.Message.Should().Contain("hash mismatch",
            "the error message should indicate a configuration hash mismatch");
        exception.Message.Should().Contain("config",
            "the error message should reference configuration");
    }

    #endregion
}
