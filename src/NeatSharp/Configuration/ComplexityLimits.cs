namespace NeatSharp.Configuration;

/// <summary>
/// Configurable bounds on network size to prevent unbounded growth.
/// </summary>
public class ComplexityLimits
{
    /// <summary>
    /// Maximum number of nodes allowed in a genome's network.
    /// Must be greater than 0 if set. <c>null</c> means unbounded.
    /// </summary>
    public int? MaxNodes { get; set; }

    /// <summary>
    /// Maximum number of connections allowed in a genome's network.
    /// Must be greater than 0 if set. <c>null</c> means unbounded.
    /// </summary>
    public int? MaxConnections { get; set; }
}
