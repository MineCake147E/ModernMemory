using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Randomness;

namespace ModernMemory.Randomness
{
    public interface IRandomNumberReader
    {
        void ReadBytes(scoped NativeSpan<byte> destination);

        byte ReadByte()
        {
            Unsafe.SkipInit(out byte dst);
            ReadBytes(new(ref dst));
            return dst;
        }
        ulong GenerateBitsUInt64(int bits)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bits, sizeof(ulong) * 8u);
            if (bits == 0) return 0;
            Unsafe.SkipInit(out ulong localBuffer);
            var bytes = (bits + 7) / 8;
            var dst = MemoryMarshal.AsBytes(new Span<ulong>(ref localBuffer));
            ReadBytes(dst[^bytes..]);
            return BinaryPrimitives.ReadUInt64LittleEndian(dst) >> 8 * sizeof(ulong) - bits;
        }
        uint GenerateBitsUInt32(int bits)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bits, sizeof(uint) * 8u);
            if (bits == 0) return 0;
            Unsafe.SkipInit(out uint localBuffer);
            var bytes = (bits + 7) / 8;
            var dst = MemoryMarshal.AsBytes(new Span<uint>(ref localBuffer));
            ReadBytes(dst[^bytes..]);
            return BinaryPrimitives.ReadUInt32LittleEndian(dst) >> 8 * sizeof(uint) - bits;
        }
        nuint GenerateRange(nuint maxExclusive)
            => Unsafe.SizeOf<nuint>() == sizeof(ulong) ? (nuint)GenerateRange((ulong)maxExclusive) : GenerateRange((uint)maxExclusive);
        nuint GenerateRange(nuint minInclusive, nuint maxExclusive) => GenerateRange(maxExclusive - minInclusive) + minInclusive;

        ulong GenerateRange(ulong maxExclusive);
        ulong GenerateRange(ulong minInclusive, ulong maxExclusive) => GenerateRange(maxExclusive - minInclusive) + minInclusive;
        uint GenerateRange(uint maxExclusive) => (uint)GenerateRange((ulong)maxExclusive);
        uint GenerateRange(uint minInclusive, uint maxExclusive) => GenerateRange(maxExclusive - minInclusive) + minInclusive;
        ushort GenerateRange(ushort maxExclusive) => (ushort)GenerateRange((ulong)maxExclusive);
        ushort GenerateRange(ushort minInclusive, ushort maxExclusive) => (ushort)(GenerateRange((ushort)(maxExclusive - minInclusive)) + minInclusive);
        byte GenerateRange(byte maxExclusive) => (byte)GenerateRange((ulong)maxExclusive);
        byte GenerateRange(byte minInclusive, byte maxExclusive) => (byte)(GenerateRange((byte)(maxExclusive - minInclusive)) + minInclusive);

        void Shuffle<T>(scoped NativeSpan<T> values, scoped ReadOnlyNativeSpan<T> source)
        {
            source.CopyTo(values);
            Shuffle(values);
        }
        void Shuffle<T>(scoped NativeSpan<T> values);

        bool TryShuffle<T>(scoped NativeSpan<T> values, scoped ReadOnlyNativeSpan<T> source)
        {
            var res = source.TryCopyTo(values);
            if (res) Shuffle(values);
            return res;
        }
    }

    public static class RandomNumberReader
    {
        public static RandomNumberReaderBox<TRandomNumberReader> Box<TRandomNumberReader>(this TRandomNumberReader reader) where TRandomNumberReader : struct, IRandomNumberReader
            => new(reader);
    }
}
