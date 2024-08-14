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

    /// <summary>
    ///     The text content of this diff line.
    /// </summary>
    [PublicAPI]
    public override string ToString()
    {
        return line;
    }
}