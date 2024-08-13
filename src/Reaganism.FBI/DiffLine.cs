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

    private readonly string lineWithOperation;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiffLine"/> struct.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="text">The text <b>without</b> an operation prefix.</param>
    public DiffLine(Operation operation, string text)
    {
        Operation         = operation;
        Text              = text;
        lineWithOperation = operation.LinePrefix + text;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="DiffLine"/> struct.
    /// </summary>
    /// <param name="lineWithOperation">The line <b>with</b> an operation prefix.</param>
    public DiffLine(string lineWithOperation)
    {
        Operation = lineWithOperation[0] switch
        {
            '+' => Operation.INSERT,
            '-' => Operation.DELETE,
            ' ' => Operation.EQUALS,
            _   => throw new ArgumentException("Invalid diff line.", nameof(lineWithOperation)),
        };
        Text                   = lineWithOperation[1..];
        this.lineWithOperation = lineWithOperation;
    }

    /// <summary>
    ///     The text content of this diff line.
    /// </summary>
    public override string ToString()
    {
        return lineWithOperation;
    }
}