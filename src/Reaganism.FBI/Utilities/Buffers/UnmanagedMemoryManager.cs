using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Reaganism.FBI.Utilities.Buffers;

/// <summary>
///     A <see cref="MemoryManager{T}"/> that wraps a pointer.
/// </summary>
/// <typeparam name="T">The type.</typeparam>
/// <remarks>
///     This is designed to facilitate using <see cref="ReadOnlyMemory{T}"/>
///     with stack-allocated memory (in place of <see cref="Span{T}"/>s).
/// </remarks>
internal sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T> where T : unmanaged
{
    private readonly T*  ptr;
    private readonly int len;

    public UnmanagedMemoryManager(Span<T> span)
    {
        ptr = (T*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
        len = span.Length;
    }

    public UnmanagedMemoryManager(ReadOnlySpan<T> span)
    {
        ptr = (T*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(span));
        len = span.Length;
    }

    public UnmanagedMemoryManager(T* ptr, int len)
    {
        this.ptr = ptr;
        this.len = len;
    }

    public override Span<T> GetSpan()
    {
        return new Span<T>(ptr, len);
    }

#region IPinnable
    public override MemoryHandle Pin(int elementIndex = 0)
    {
        if (elementIndex < 0 || elementIndex >= len)
        {
            throw new ArgumentOutOfRangeException(nameof(elementIndex));
        }
        return new MemoryHandle(Unsafe.Add<T>(ptr, elementIndex));
    }

    public override void Unpin()
    {
        // no-op
    }
#endregion

#region IDisposable
    protected override void Dispose(bool disposing)
    {
        // no-op
    }
#endregion
}