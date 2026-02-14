// Contract definition — reference for the existing INeatEvolver interface.
// This interface is ALREADY DEFINED in src/NeatSharp/Evolution/INeatEvolver.cs.
// No changes to the interface are required. Only the implementation changes
// (NeatEvolverStub → NeatEvolver).

// Existing contract (unchanged):
//
// public interface INeatEvolver
// {
//     Task<EvolutionResult> RunAsync(
//         IEvaluationStrategy evaluator,
//         CancellationToken cancellationToken = default);
// }
//
// Existing convenience extensions (unchanged):
//
// public static class NeatEvolverExtensions
// {
//     RunAsync(Func<IGenome, double>)
//     RunAsync(Func<IGenome, CancellationToken, Task<double>>)
//     RunAsync(IEnvironmentEvaluator)
//     RunAsync(IBatchEvaluator)
// }
