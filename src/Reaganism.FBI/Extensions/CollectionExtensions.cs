using System.Collections;
using System.Collections.Generic;

namespace Reaganism.FBI.Extensions;

internal static class CollectionExtensions
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