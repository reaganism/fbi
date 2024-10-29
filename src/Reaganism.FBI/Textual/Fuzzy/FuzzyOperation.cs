using JetBrains.Annotations;

namespace Reaganism.FBI.Textual.Fuzzy;

/// <summary>
///     A diff operation.
/// </summary>
public readonly record struct FuzzyOperation
{
    /// <summary>
    ///     "Delete" (<c>-</c>) operation; removes the line.
    /// </summary>
    public static readonly FuzzyOperation DELETE = new('-');

    /// <summary>
    ///     "Insert" (<c>+</c>) operation; adds the line.
    /// </summary>
    public static readonly FuzzyOperation INSERT = new('+');

    /// <summary>
    ///     "Equals" (<c> </c>) operation (no-op); the line is unchanged.
    /// </summary>
    public static readonly FuzzyOperation EQUALS = new(' ');

    /// <summary>
    ///     The line prefix for the operation.
    /// </summary>
    public char LinePrefix { [PublicAPI] get; }

    private FuzzyOperation(char linePrefix)
    {
        LinePrefix = linePrefix;
    }
}