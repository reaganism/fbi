using System;

namespace Reaganism.FBI;

/// <summary>
///     Represents a line diff.
/// </summary>
public readonly record struct DiffLine
{
    /// <summary>
    ///     The diff operation of this diff.
    /// </summary>
    public Operation Operation { get; }

    /*/// <summary>
    ///     The text of this diff; that is, the actual line (not including the
    ///     operation prefix).
    /// </summary>
    public string Text { get; }*/

    // PERF: Cache original line concatenation here to avoid extra allocations.
    private readonly string line;

    public DiffLine(Operation operation, string text)
    {
        Operation = operation;
        line      = operation.LinePrefix + text;
    }

    public DiffLine(string line)
    {
        Operation = line[0] switch
        {
            '+' => Operation.INSERT,
            '-' => Operation.DELETE,
            ' ' => Operation.EQUALS,
            _   => throw new ArgumentException("Invalid diff line.", nameof(line)),
        };
        this.line = line;
    }

    public override string ToString()
    {
        return line;
    }
}