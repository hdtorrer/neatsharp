namespace NeatSharp.Configuration;

/// <summary>
/// Configuration for parent selection, elitism, and stagnation handling.
/// </summary>
public class SelectionOptions
{
    /// <summary>
    /// Minimum species size for champion preservation (elitism).
    /// Species with at least this many members have their champion copied unchanged.
    /// Must be at least 1.
    /// </summary>
    public int ElitismThreshold { get; set; } = 5;

    /// <summary>
    /// Number of generations without fitness improvement before a species is considered stagnant.
    /// Stagnant species receive zero offspring (unless in the top 2 by peak fitness).
    /// Must be at least 1.
    /// </summary>
    public int StagnationThreshold { get; set; } = 15;

    /// <summary>
    /// Fraction of species members eligible for reproduction.
    /// Only the top fraction by fitness can be selected as parents.
    /// Must be in (0.0, 1.0].
    /// </summary>
    public double SurvivalThreshold { get; set; } = 0.2;

    /// <summary>
    /// Number of random candidates compared in tournament selection.
    /// Must be at least 1.
    /// </summary>
    public int TournamentSize { get; set; } = 2;
}
