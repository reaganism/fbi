using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

using JetBrains.Annotations;

using Reaganism.FBI.Utilities.Buffers;

using Standart.Hash.xxHash;

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
    public ReadOnlySpan<char> Span => mem.Span;

    /// <summary>
    ///     A <see langword="ref"/> <see cref="char"/> to the first element of
    ///     the underlying data.
    /// </summary>
    [PublicAPI]
    public ref char Ref => ref *Ptr;

    private char* Ptr
    {
        get
        {
            fixed (char* p = mem.Span)
            {
                return p;
            }
        }
    }

    /// <summary>
    ///     The length of the <see cref="Utf16String"/>.
    /// </summary>
    [PublicAPI]
    public int Length => mem.Length;

    private readonly ReadOnlyMemory<char> mem;

    private Utf16String(ReadOnlyMemory<char> mem)
    {
        this.mem = mem;
    }

#region Hashing & equality
    public override int GetHashCode()
    {
        return (int)xxHash32.ComputeHash(new ReadOnlySpan<byte>(Ptr, Length * 2), Length * 2);
    }

    [PublicAPI]
    public bool Equals(Utf16String other)
    {
        if (Length != other.Length)
        {
            return false;
        }

        if (Ptr == other.Ptr)
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
                : !Span.SequenceEqual(other.Span),
            $"Hash code collision; got 0x{GetHashCode():X8} for \"{Span.ToString()}\" and \"{other.Span.ToString()}\""
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

        return new Utf16String(mem.Slice(start, length));
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
    public static Utf16String FromString(string value)
    {
        return new Utf16String(value.AsMemory());
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
        var mem = new UnmanagedMemoryManager<char>(value);
        return new Utf16String(mem.Memory);
    }

    /// <summary>
    ///     Creates a <see cref="Utf16String"/> from a
    ///     <see cref="ReadOnlySpan{T}"/> without copying the data.
    /// </summary>
    /// <param name="value">The <see cref="ReadOnlySpan{T}"/>.</param>
    /// <returns>The <see cref="Utf16String"/>.</returns>
    [PublicAPI]
    public static Utf16String FromSpan(ReadOnlySpan<char> value)
    {
        var mem = new UnmanagedMemoryManager<char>(value);
        return new Utf16String(mem.Memory);
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