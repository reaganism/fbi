using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Reaganism.FBI;

/// <summary>
///     Describes an inclusive-exclusive range of lines.
/// </summary>
/// <param name="Start">The starting line index (inclusive).</param>
/// <param name="End">The ending line index (exclusive).</param>
[DebuggerDisplay("{ToString()}")]
public readonly record struct LineRange(int Start, int End)
{
    /// <summary>
    ///     The number of lines in the range.
    /// </summary>
    public int Length => End - Start;

    /// <summary>
    ///     The first line in the range.
    /// </summary>
    /// <remarks>
    ///     Identical to <see cref="Start"/>.
    /// </remarks>
    public int First => Start;

    /// <summary>
    ///     The last line in the range.
    /// </summary>
    /// <remarks>
    ///     Use this to get the actual last line in the range.
    /// </remarks>
    public int Last => End - 1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LineRange WithLength(int length)
    {
        return this with { End = Start + length };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LineRange WithFirst(int first)
    {
        return this with { Start = first };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public LineRange WithLast(int last)
    {
        return this with { End = last + 1 };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(int index)
    {
        return Start <= index && index < End;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(LineRange range)
    {
        return Start <= range.Start && range.End <= End;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Intersects(LineRange range)
    {
        return Start < range.End || range.End > Start;
    }

    public override string ToString()
    {
        return $"[{Start},{End})";
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
            yield return this with { Start = start };
        }
    }

    public static LineRange operator +(LineRange range, int i)
    {
        return new LineRange(Start: range.Start + i, End: range.End + i);
    }

    public static LineRange operator -(LineRange range, int i)
    {
        return new LineRange(Start: range.Start - i, End: range.End - i);
    }

    public static implicit operator Range(LineRange range)
    {
        return new Range(Index.FromStart(range.Start), Index.FromStart(range.End));
    }
}

public static class LineRangeExtensions
{
    private sealed class ListSlice<T>(IReadOnlyList<T> list, LineRange range) : IReadOnlyList<T>
    {
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            for (var i = range.Start; i < range.End; i++)
            {
                yield return list[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }

        int IReadOnlyCollection<T>.Count => range.Length;

        T IReadOnlyList<T>.this[int index] => list[index + range.Start];
    }

    public static IReadOnlyList<T> Slice<T>(this IReadOnlyList<T> @this, int start, int length)
    {
        return @this.Slice(new LineRange(start, start + length));
    }

    public static IReadOnlyList<T> Slice<T>(this IReadOnlyList<T> @this, LineRange range)
    {
        return range.Start == 0 && range.Length == @this.Count ? @this : new ListSlice<T>(@this, range);
    }
}