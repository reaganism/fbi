using System;
using System.Diagnostics;

using JetBrains.Annotations;

namespace Reaganism.FBI.Utility;

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
public readonly unsafe struct Utf16String
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
        Debug.Assert(start  > 0  && start          <= Length);
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
}