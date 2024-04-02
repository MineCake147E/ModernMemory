using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Threading
{
    public static class AtomicUtils
    {
        /// <inheritdoc cref="Interlocked.Exchange(ref uint, uint)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exchange(ref uint location1, bool value)
            => unchecked(Unsafe.BitCast<byte, bool>((byte)Interlocked.Exchange(ref location1, Unsafe.BitCast<bool, byte>(value))));

        /// <inheritdoc cref="Interlocked.Exchange(ref int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Exchange(ref int location1, bool value)
            => unchecked(Unsafe.BitCast<byte, bool>((byte)Interlocked.Exchange(ref location1, Unsafe.BitCast<bool, byte>(value))));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetValue(bool value) => Unsafe.BitCast<bool, byte>(value);
    }
}
