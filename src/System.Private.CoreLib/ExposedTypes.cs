/* System.Private.CoreLib
 *  This project exists to allow Reaganism.FBI to access runtime internals
 *  through means similar to an assembly publicizer.
 *  For more information, see:
 *  <https://github.com/krafs/Publicizer/issues/101>
 */

namespace System;

public static class Marvin
{
    public static ulong DefaultSeed => throw new NotImplementedException();

    public static int ComputeHash32(ref byte data, uint count, uint p0, uint p1)
    {
        throw new NotImplementedException();
    }
}