using System.Diagnostics;

using JetBrains.Annotations;

namespace Reaganism.FBI;

/// <summary>
///     A diffing operation.
/// </summary>
[PublicAPI]
public readonly record struct Operation
{
    // PERF: String literals are used here in favor of character literals to
    // avoid any unnecessary allocations down the line when concatenating with
    // other strings.

    /// <summary>
    ///     "Delete" (<c>-</c>) operation; removes the line.
    /// </summary>
    [PublicAPI]
    public static readonly Operation DELETE = new("-");

    /// <summary>
    ///     "Insert" (<c>+</c>) operation; adds the line.
    /// </summary>
    [PublicAPI]
    public static readonly Operation INSERT = new("+");

    /// <summary>
    ///     "Equals" (<c> </c>) operation (no-op); the line is unchanged.
    /// </summary>
    [PublicAPI]
    public static readonly Operation EQUALS = new(" ");

    /// <summary>
    ///     The character that prefixes a line.
    /// </summary>
    /// <remarks>
    ///     "No operation" is also represented by an operation, in which case
    ///     this value is '<c> </c>' (<b>NOT</b> empty/no value).
    /// </remarks>
    [PublicAPI]
    public string LinePrefix { [PublicAPI] get; }

    private Operation(string linePrefix)
    {
        Debug.Assert(linePrefix.Length == 1);
        LinePrefix = linePrefix;
    }
}