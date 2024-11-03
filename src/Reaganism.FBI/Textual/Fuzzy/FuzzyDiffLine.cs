using System.Diagnostics;
using System.Text;

using JetBrains.Annotations;

using Reaganism.FBI.Utilities;

namespace Reaganism.FBI.Textual.Fuzzy;

/// <summary>
///     Represents a whole diff line (operation and line contents).
/// </summary>
/// <remarks>
///     The line content is not directly accessible by design (depending on the
///     context, the literal line may or may not already contain the operation
///     character). As such, use <see cref="ToString"/> to get the full line
///     (which creates a heap allocation for a whole string). If you only need
///     the operation character, use <see cref="Operation"/>. If you are
///     constructing a string with a <see cref="StringBuilder"/>, you can use
///     <see cref="Append"/> and <see cref="AppendLine"/> to avoid allocating
///     the actual text as a new string.
/// </remarks>
[PublicAPI]
public readonly struct FuzzyDiffLine
{
    // PERF: The actual text of this line is stored as a memory reference.  This
    //       means it may be from a string, may be a stack-allocated span of
    //       chars, who knows.

    /// <summary>
    ///     The operation of this line.
    /// </summary>
    [PublicAPI]
    public FuzzyOperation Operation { get; }

    private readonly Utf16String text;
    private readonly bool        hasPrefix;

    /// <summary>
    ///     Creates a new <see cref="FuzzyDiffLine"/> with the given operation
    ///     and text.
    /// </summary>
    /// <param name="operation">The operation.</param>
    /// <param name="text">The text <b>WITHOUT</b> the operation character.</param>
    public FuzzyDiffLine(FuzzyOperation operation, Utf16String text)
    {
        Operation = operation;
        this.text = text;

        // In the public API, only allow constructing diff lines with the text
        // and operation separate.  The separate, internal API is provided for
        // specifically our parsing of patch files.  Regular API consumers
        // should not have to do this themselves.
        hasPrefix = false;
    }

    internal FuzzyDiffLine(
        FuzzyOperation operation,
        Utf16String    text,
        bool           hasPrefix
    )
    {
        Debug.Assert(
            hasPrefix ? text.Span[0] == operation.LinePrefix : text.Span[0] != operation.LinePrefix,
            "Constructed FuzzyDiffLine with invalid prefix (either no prefix expected or prefix didn't match)"
        );

        Operation      = operation;
        this.text      = text;
        this.hasPrefix = hasPrefix;
    }

#region Serialization
    [PublicAPI]
    public StringBuilder Append(StringBuilder sb)
    {
        if (!hasPrefix)
        {
            sb.Append(Operation.LinePrefix);
        }

        return sb.AppendUtf16(text);
    }

    [PublicAPI]
    public StringBuilder AppendLine(StringBuilder sb)
    {
        Append(sb);
        return sb.AppendLine();
    }

    // PERF: Avoid using ToString when possible.  Prefer Append[Line] APIs when
    //       concatenating strings.
    public override string ToString()
    {
        return hasPrefix ? text.Span.ToString() : Operation.LinePrefix + text.Span.ToString();
    }
#endregion
}