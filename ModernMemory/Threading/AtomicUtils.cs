﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
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

        /// <inheritdoc cref="Interlocked.CompareExchange(ref uint, uint, uint)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareExchange(ref uint location1, bool value, bool comparand)
            => unchecked(Unsafe.BitCast<byte, bool>((byte)Interlocked.CompareExchange(ref location1, Unsafe.BitCast<bool, byte>(value), Unsafe.BitCast<bool, byte>(comparand))));

        /// <inheritdoc cref="Interlocked.CompareExchange(ref int, int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareExchange(ref int location1, bool value, bool comparand)
            => unchecked(Unsafe.BitCast<byte, bool>((byte)Interlocked.CompareExchange(ref location1, Unsafe.BitCast<bool, byte>(value), Unsafe.BitCast<bool, byte>(comparand))));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint GetValue(bool value) => Unsafe.BitCast<bool, byte>(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetValue(uint value) => Unsafe.BitCast<byte, bool>((byte)value);

        /// <inheritdoc cref="Volatile.Read(ref readonly uint)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool LoadValue(ref readonly uint location) => Unsafe.BitCast<byte, bool>((byte)Volatile.Read(in location));

        /// <inheritdoc cref="Volatile.Write(ref uint, uint)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreValue(ref uint location, bool value) => Volatile.Write(ref location, Unsafe.BitCast<bool, byte>(value));

        /// <inheritdoc cref="Interlocked.Add(ref ulong, ulong)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static nuint Add(ref nuint location1, nuint value)
        {
            return Unsafe.SizeOf<nuint>() switch
            {
                sizeof(ulong) => (nuint)Interlocked.Add(ref Unsafe.As<nuint, ulong>(ref location1), value),
                sizeof(uint) => Interlocked.Add(ref Unsafe.As<nuint, uint>(ref location1), (uint)value),
                _ => AddSlow(ref location1, value)
            };

            static nuint AddSlow(ref nuint location, nuint value)
            {
                unchecked
                {
                    nuint v, nv;
                    do
                    {
                        v = location;
                        nv = v + value;
                    } while (Interlocked.CompareExchange(ref location, nv, v) == v);
                    return nv;
                }
            }
        }
    }
}
