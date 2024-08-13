namespace Reaganism.FBI;

/// <summary>
///     A by-line numeric representation of a text range for contextualizing
///     a patch with a header.
/// </summary>
/// <param name="Start">The starting line.</param>
/// <param name="Length">The amount of lines from the start.</param>
public readonly record struct LineRange(int Start, int Length)
{
    /// <summary>
    ///     The ending line.
    /// </summary>
    public int End => Start + Length;
}