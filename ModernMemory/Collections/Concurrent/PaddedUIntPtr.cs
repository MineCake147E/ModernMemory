using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections.Concurrent
{
    [StructLayout(LayoutKind.Explicit, Pack =64)]
    internal readonly struct PaddedUIntPtr
    {
        [FieldOffset(0)]
        internal readonly nuint content;
        [FieldOffset(0)]
        private readonly Vector512<byte> dummy;

        [SkipLocalsInit]
        public PaddedUIntPtr(nuint value)
        {
            Unsafe.SkipInit(out dummy);
            content = value;
        }

        public static implicit operator nuint(PaddedUIntPtr value) => value.content;
        public static implicit operator PaddedUIntPtr(nuint value) => new(value);
    }

    internal static class PaddedUIntPtrUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Set(this ref PaddedUIntPtr field, nuint value) => Unsafe.As<PaddedUIntPtr, nuint>(ref field) = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VolatileSet(this ref PaddedUIntPtr field, nuint value) => Volatile.Write(ref Unsafe.As<PaddedUIntPtr, nuint>(ref field), value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref nuint GetRef(this ref PaddedUIntPtr field) => ref Unsafe.As<PaddedUIntPtr, nuint>(ref field);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint Load(this ref PaddedUIntPtr field) => field.content;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint VolatileLoad(this ref PaddedUIntPtr field) => Volatile.Read(ref Unsafe.As<PaddedUIntPtr, nuint>(ref field));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint Exchange(this ref PaddedUIntPtr field, nuint value) => Interlocked.Exchange(ref Unsafe.As<PaddedUIntPtr, nuint>(ref field), value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint CompareExchange(this ref PaddedUIntPtr field, nuint value, nuint comparand) => Interlocked.CompareExchange(ref Unsafe.As<PaddedUIntPtr, nuint>(ref field), value, comparand);

    }
}
