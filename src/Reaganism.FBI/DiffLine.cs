using System;
using System.Diagnostics;

using JetBrains.Annotations;

namespace Reaganism.FBI;

/// <summary>
///     Represents a line diff.
/// </summary>
/// <remarks>
///     The string content should be accessed through <see cref="ToString"/>.
/// </remarks>
[PublicAPI]
public readonly record struct DiffLine
{
    /// <summary>
    ///     The diff operation of this diff.
    /// </summary>
    [PublicAPI]
    public Operation Operation { [PublicAPI] get; }

    /// <summary>
    ///     The text content of this diff line.
    /// </summary>
    [PublicAPI]
    public string Text { [PublicAPI] get; }

    private readonly string line;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiffLine"/> struct.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="text">The text <b>without</b> an operation prefix.</param>
    [PublicAPI]
    public DiffLine(Operation operation, string text)
    {
        Operation = operation;
        Text      = text;
        line      = operation.LinePrefix + text;
    }

    // Reduces allocations somewhat by allowing the original line with the
    // prefix to be passed as to not force it to be sliced just to be allocated
    // again.  Goes from a slice operation and a concatenation to just a slice.
    internal DiffLine(Operation operation, string text, bool hasPrefix)
    {
        Debug.Assert(hasPrefix ? text[0] == operation.LinePrefix[0] : text[0] != operation.LinePrefix[0]);

        Operation = operation;
        Text      = hasPrefix ? text[1..] : text;
        line      = hasPrefix ? text : operation.LinePrefix + text;
    }

    /// <summary>
    ///     The text content of this diff line.
    /// </summary>
    [PublicAPI]
    public override string ToString()
    {
        return line;
    }
}