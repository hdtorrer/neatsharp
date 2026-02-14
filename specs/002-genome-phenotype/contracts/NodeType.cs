// Contract definition — not compilable source code.
// Defines the node type enumeration for NEAT genome node genes.

namespace NeatSharp.Genetics;

/// <summary>
/// Classifies the role of a node gene within a NEAT genome.
/// </summary>
public enum NodeType
{
    /// <summary>
    /// Receives external input values during network activation.
    /// </summary>
    Input,

    /// <summary>
    /// Intermediate processing node between inputs and outputs.
    /// Created by structural mutations (node splits).
    /// </summary>
    Hidden,

    /// <summary>
    /// Produces network output values after activation.
    /// </summary>
    Output,

    /// <summary>
    /// Always outputs a constant value of 1.0, providing a learnable
    /// offset when connected to other nodes.
    /// </summary>
    Bias
}
