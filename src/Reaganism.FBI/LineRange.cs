using System;
using System.Collections.Generic;
using System.Linq;

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

    public bool Contains(int index)
    {
        return Start <= index && index < End;
    }

    public bool Contains(LineRange range)
    {
        return range.Start >= Start && range.End <= End;
    }

    public bool Intersects(LineRange range)
    {
        return range.Start < End || range.End > Start;
    }

    public override string ToString()
    {
        return $"[{Start},{End})";
    }

    public static LineRange operator +(LineRange range, int i)
    {
        return new LineRange(range.Start + i, range.End + i);
    }

    public static LineRange operator -(LineRange range, int i)
    {
        return new LineRange(range.Start - i, range.End - i);
    }

    public IEnumerable<LineRange> Except(IEnumerable<LineRange> except, bool presorted = false)
    {
        if (!presorted)
        {
            except = except.OrderBy(x => x.Start);
        }

        var start = Start;
        foreach (var range in except)
        {
            if (range.Start - start > 0)
            {
                yield return new LineRange(start, range.Start);
            }

            start = range.End;
        }

        if (End - start > 0)
        {
            yield return new LineRange(start, End);
        }
    }

    public static implicit operator Range(LineRange range)
    {
        return new Range(Index.FromStart(range.Start), Index.FromStart(range.End));
    }
}