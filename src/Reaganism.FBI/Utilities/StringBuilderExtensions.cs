using System.Runtime.CompilerServices;
using System.Text;

namespace Reaganism.FBI.Utilities;

internal static class StringBuilderExtensions
{
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
    public static StringBuilder AppendUtf16Line(
        this StringBuilder @this,
        Utf16String        value
    )
    {
        Append(@this, ref value.Ref, value.Length);
        return @this.AppendLine();
    }
}