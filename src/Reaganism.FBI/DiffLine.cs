using System;

namespace Reaganism.FBI;

/// <summary>
///     Represents a line diff.
/// </summary>
/// <remarks>
///     The string content should be accessed through <see cref="ToString"/>.
/// </remarks>
public readonly record struct DiffLine
{
    /// <summary>
    ///     The diff operation of this diff.
    /// </summary>
    public Operation Operation { get; }

    /// <summary>
    ///     The text content of this diff line.
    /// </summary>
    public string Text { get; }

    private readonly string line;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiffLine"/> struct.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="text">The text <b>without</b> an operation prefix.</param>
    public DiffLine(Operation operation, string text)
    {
        Operation = operation;
        Text      = text;
        line      = operation.LinePrefix + text;
    }

    /// <summary>
    ///     The text content of this diff line.
    /// </summary>
    public override string ToString()
    {
        return line;
    }
}