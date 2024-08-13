namespace Reaganism.FBI;

/// <summary>
///     Represents a line diff.
/// </summary>
public readonly record struct Diff(Operation Operation, string Text)
{
    /// <summary>
    ///     The diff operation of this diff.
    /// </summary>
    public Operation Operation { get; } = Operation;

    /// <summary>
    ///     The text of this diff; that is, the actual line (not including the
    ///     operation prefix).
    /// </summary>
    public string Text { get; } = Text;

    // PERF: Cache original line concatenation here to avoid extra allocations.
    private readonly string line = Operation.LinePrefix + Text;

    public override string ToString()
    {
        return line;
    }
}