using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using JetBrains.Annotations;

namespace Reaganism.FBI.Utilities;

/// <summary>
///     A UTF-16-encoded string.
/// </summary>
/// <remarks>
///     Essentially an alternative representation of a <see cref="string"/>
///     (also using <see cref="char"/>s).  Provides a <see cref="Span"/> view
///     into the underlying data.  Useful for providing APIs that may operate on
///     heap and stack data without copying the data and thereby removing the
///     benefit of stack allocations.
/// </remarks>
[PublicAPI]
public readonly unsafe struct Utf16String : IEquatable<Utf16String>
{
    /// <summary>
    ///     A <see cref="ReadOnlySpan{T}"/> view into the underlying data.
    /// </summary>
    [PublicAPI]
    public ReadOnlySpan<char> Span => new(ptr, Length);

    /// <summary>
    ///     A <see langword="ref"/> <see cref="char"/> to the first element of
    ///     the underlying data.
    /// </summary>
    [PublicAPI]
    public ref char Ref => ref *ptr;

    /// <summary>
    ///     The length of the <see cref="Utf16String"/>.  Equivalent to
    ///     <see cref="Span{T}.Length"/>.
    /// </summary>
    [PublicAPI]
    public int Length { get; }

    private readonly char* ptr;

    private Utf16String(char* ptr, int len)
    {
        this.ptr = ptr;
        Length   = len;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(unchecked((int)(long)ptr), Length);
    }

#region Hashing & equality
    [PublicAPI]
    public bool Equals(Utf16String other)
    {
        if (Length != other.Length)
        {
            return false;
        }

        if (ptr == other.ptr)
        {
            // We already check that the lengths match first.
            return true;
        }

        // Lazily check equality with the hash code (collision is possible but
        // unlikely and implies other issues).  We can check the equality of the
        // sequence while debugging to make sure, though.
        Debug.Assert(
            GetHashCode() == other.GetHashCode()
                ? Span.SequenceEqual(other.Span)
                : !Span.SequenceEqual(other.Span)
        );
        {
            return GetHashCode() == other.GetHashCode();
        }
    }

    public override bool Equals(object? obj)
    {
        return obj is Utf16String other && Equals(other);
    }
#endregion

    /// <summary>
    ///     Slices the <see cref="Utf16String"/> to the specified range without
    ///     copying the data.
    /// </summary>
    /// <param name="start">The starting index of the slice.</param>
    /// <param name="length">The length of the slice.</param>
    /// <returns>
    ///     A new <see cref="Utf16String"/> with no knowledge of the original
    ///     <see cref="Utf16String"/>.
    /// </returns>
    [PublicAPI]
    public Utf16String Slice(int start, int length)
    {
        Debug.Assert(start  >= 0 && start          <= Length);
        Debug.Assert(length >= 0 && start + length <= Length);

        return new Utf16String(ptr + start, length);
    }

    /// <summary>
    ///     Slices the <see cref="Utf16String"/> to the specified range without
    ///     copying the data.  Extends to the end of the
    ///     <see cref="Utf16String"/>.
    /// </summary>
    /// <param name="start">The starting index of the slice.</param>
    /// <returns>
    ///     A new <see cref="Utf16String"/> with no knowledge of the original
    ///     <see cref="Utf16String"/>.
    /// </returns>
    [PublicAPI]
    public Utf16String Slice(int start)
    {
        return Slice(start, Length - start);
    }

    /// <summary>
    ///     Creates a <see cref="Utf16String"/> from a <see cref="string"/>
    ///     without copying the data.
    /// </summary>
    /// <param name="value">The <see cref="string"/>.</param>
    /// <returns>The <see cref="Utf16String"/>.</returns>
    [PublicAPI]
    public static Utf16String FromReference(string value)
    {
        fixed (char* pValue = &value.GetPinnableReference())
        {
            return new Utf16String(pValue, value.Length);
        }
    }

    /// <summary>
    ///     Creates a <see cref="Utf16String"/> from a <see cref="char"/> array
    ///     without copying the data.
    /// </summary>
    /// <param name="value">The array.</param>
    /// <returns>The <see cref="Utf16String"/>.</returns>
    [PublicAPI]
    public static Utf16String FromArray(char[] value)
    {
        fixed (char* pValue = value)
        {
            return new Utf16String(pValue, value.Length);
        }
    }

    /// <summary>
    ///     Creates a <see cref="Utf16String"/> from a <see cref="Span{T}"/>
    ///     without copying the data.
    /// </summary>
    /// <param name="value">The <see cref="Span{T}"/>.</param>
    /// <returns>The <see cref="Utf16String"/>.</returns>
    [PublicAPI]
    public static Utf16String FromSpan(Span<char> value)
    {
        fixed (char* pValue = value)
        {
            return new Utf16String(pValue, value.Length);
        }
    }

    [PublicAPI]
    public static bool operator ==(Utf16String left, Utf16String right)
    {
        return left.Equals(right);
    }

    [PublicAPI]
    public static bool operator !=(Utf16String left, Utf16String right)
    {
        return !(left == right);
    }

    internal static int LevenshteinDistance(Utf16String s, Utf16String t)
    {
        // Degenerate cases.
        if (s == t)
        {
            return 0;
        }

        if (s.Length == 0)
        {
            return t.Length;
        }

        if (t.Length == 0)
        {
            return s.Length;
        }

        // Create two work vectors of integer distances.
        var v0 = (Span<int>)stackalloc int[t.Length + 1]; // Previous
        var v1 = (Span<int>)stackalloc int[t.Length + 1]; // Current

        // Initialize v1 (the current row of distances).  This row is
        // A[0][i]: edit distance for an empty `s`.  The distance is just
        // the number of characters to delete from `t`.
        for (var i = 0; i < v1.Length; i++)
        {
            v1[i] = i;
        }

        for (var i = 0; i < s.Length; i++)
        {
            // Swap v1 to v0, reuse old v0 as new v1.
            var temp = v0;
            v0 = v1;
            v1 = temp;

            // Calculate v1 (current row distances) from the previous row
            // v0.

            // First element of v1 is A[i + 1][0].  Edit distance is delete
            // (i + 1) chars from `s` to match empty `t`.
            v1[0] = i + 1;

            // Use formulate to fill in the rest of the row.
            for (var j = 0; j < t.Length; j++)
            {
                var del = v0[j + 1] + 1;
                var ins = v1[j]     + 1;
                var sub = v0[j]     + (s.Span[i] == t.Span[j] ? 0 : 1);
                v1[j + 1] = Math.Min(del, Math.Min(ins, sub));
            }
        }

        return v1[t.Length];
    }
}

[PublicAPI]
public static class Utf16StringExtensions
{
#region StringBuilder extensions
    // TODO: Currently dependent on implementation details of StringBuilder.

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Append")]
    private static extern void Append(
        StringBuilder @this,
        ref char      value,
        int           valueCount
    );

    /// <summary>
    ///     Appends a given <see cref="Utf16String"/> (<paramref name="value"/>)
    ///     to a <see cref="StringBuilder"/> without unnecessary allocation of
    ///     <see cref="string"/> objects.
    /// </summary>
    /// <param name="this">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="value">The <see cref="Utf16String"/> to append.</param>
    [PublicAPI]
    public static StringBuilder AppendUtf16(
        this StringBuilder @this,
        Utf16String        value
    )
    {
        Append(@this, ref value.Ref, value.Length);
        return @this;
    }

    /// <summary>
    ///     Appends a given <see cref="Utf16String"/> (<paramref name="value"/>)
    ///     to a <see cref="StringBuilder"/> without unnecessary allocation of
    ///     <see cref="string"/> objects followed by a newline.
    /// </summary>
    /// <param name="this">The <see cref="StringBuilder"/> to append to.</param>
    /// <param name="value">The <see cref="Utf16String"/> to append.</param>
    [PublicAPI]
    public static StringBuilder AppendUtf16Line(
        this StringBuilder @this,
        Utf16String        value
    )
    {
        Append(@this, ref value.Ref, value.Length);
        return @this.AppendLine();
    }
#endregion
}