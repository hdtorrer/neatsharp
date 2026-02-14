namespace NeatSharp.Genetics;

/// <summary>
/// Represents a single neuron (node gene) in a NEAT genome.
/// Immutable after construction.
/// </summary>
/// <param name="Id">Unique identifier for this node within a genome.</param>
/// <param name="Type">The role of this node (Input, Hidden, Output, Bias).</param>
/// <param name="ActivationFunction">
/// Name of the activation function applied to this node's weighted input sum.
/// Must be a key registered in <see cref="IActivationFunctionRegistry"/>.
/// Defaults to "sigmoid".
/// </param>
public sealed record NodeGene(
    int Id,
    NodeType Type,
    string ActivationFunction = "sigmoid");
