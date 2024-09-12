using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

using JetBrains.Annotations;

namespace Reaganism.FBI;

/// <summary>
///     A by-line numeric representation of a text range for contextualizing
///     a patch with a header.
/// </summary>
/// <param name="Start">The starting line.</param>
/// <param name="End">The ending line.</param>
[PublicAPI]
public readonly record struct LineRange([PublicAPI] int Start, [PublicAPI] int End)
{
    [PublicAPI]
    public int Length
    {
        [PublicAPI] get => End - Start;
    }

    [PublicAPI]
    public int First
    {
        [PublicAPI] get => Start;
    }

    [PublicAPI]
    public int Last
    {
        [PublicAPI] get => End - 1;
    }

    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LineRange WithLength(int length)
    {
        return this with { End = Start + length };
    }

    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LineRange WithLast(int last)
    {
        return this with { End = last + 1 };
    }

    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(int index)
    {
        return Start <= index && index < End;
    }

    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(LineRange range)
    {
        return range.Start >= Start && range.End <= End;
    }

    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(LineRange range)
    {
        return range.Start < End || range.End > Start;
    }

    [PublicAPI]
    public override string ToString()
    {
        return $"[{Start},{End})";
    }

    [PublicAPI]
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
            yield return this with { Start = start };
        }
    }

    [PublicAPI]
    public static LineRange operator +(LineRange range, int i)
    {
        return new LineRange(range.Start + i, range.End + i);
    }

    [PublicAPI]
    public static LineRange operator -(LineRange range, int i)
    {
        return new LineRange(range.Start - i, range.End - i);
    }

    [PublicAPI]
    public static implicit operator Range(LineRange range)
    {
        return new Range(Index.FromStart(range.Start), Index.FromStart(range.End));
    }
}