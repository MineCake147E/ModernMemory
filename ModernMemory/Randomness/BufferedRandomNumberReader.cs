using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers.DataFlow;
using ModernMemory.Collections;
using ModernMemory.Randomness.Permutation;

namespace ModernMemory.Randomness
{
    public struct BufferedRandomNumberReader<TRandomNumberProvider> : IRandomNumberReader, IRandomNumberProvider where TRandomNumberProvider : IRandomNumberProvider
    {
        private NativeQueue<byte> Buffer { get; }

        private TRandomNumberProvider source;

        public BufferedRandomNumberReader(TRandomNumberProvider provider, nuint bufferSize = 0, nuint refillMultiplier = 0) : this()
        {
            source = provider;
            var ru = refillMultiplier >= 1 && refillMultiplier > provider.GetUnitsToGenerateAtLeast(64) ? refillMultiplier : provider.GetUnitsToGenerateAtLeast(256);
            RefillSize = provider.GetUnitsToGenerateAtLeast(ru);
            Buffer = new(RefillSize * 2);
        }

        private nuint RefillSize { get; }
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private void EnsureBuffer(nuint bytes, nuint refill = 0)
        {
            var bytesToAdd = bytes - Buffer.Count;
            if (bytesToAdd <= 0) return;
            if (refill > bytesToAdd) bytesToAdd = refill;
            Refill(bytesToAdd);
        }

        private void Refill(nuint bytesToAdd)
        {
            var units = source.GetUnitsToGenerateAtLeast(bytesToAdd);
            source.Generate(Buffer, units);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void ReadBytes(scoped NativeSpan<byte> destination) => Generate(destination);

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public byte ReadByte()
        {
            EnsureBuffer(1, RefillSize);
            return Buffer.Dequeue();
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public byte PeekByte()
        {
            EnsureBuffer(1, RefillSize);
            return Buffer.Peek();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ulong GenerateBitsUInt64(int bits)
        {
            if (bits == 0) return 0;
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)bits, 64u);
            if (bits <= 8) return (ulong)ReadByte() >> 8 - bits;
            Unsafe.SkipInit(out ulong localBuffer);
            var bytes = (bits + 7) / 8;
            var dst = MemoryMarshal.AsBytes(new Span<ulong>(ref localBuffer));
            ReadBytes(dst[^bytes..]);
            return BinaryPrimitives.ReadUInt64LittleEndian(dst) >> (8 * sizeof(ulong) - bits);
        }

        public ulong GenerateRange(ulong maxExclusive)
        {
            var maxInclusive = maxExclusive - 1;
            var bits = 8 * sizeof(ulong) - BitOperations.LeadingZeroCount(maxInclusive);
            var localBuffer = GenerateBitsUInt64(bits);
            if (bits < 2 || localBuffer <= maxInclusive) return localBuffer;
            //var bitMaxExclusive = (ulong.MaxValue >> (8 * sizeof(ulong) - bits)) + 1;
            do
            {
                //var maxDiff = bitMaxExclusive - maxExclusive;
                //var usableBits = 8 * sizeof(ulong) - BitOperations.LeadingZeroCount(maxDiff - 1);
                //var bitsToAdd = bits - usableBits;
                //var m = maxDiff << bitsToAdd;
                //bitsToAdd += (m < maxExclusive).ToByte();
                //var g = GenerateBitsUInt64(bitsToAdd);
                //localBuffer += g * maxDiff;
                localBuffer = GenerateBitsUInt64(bits);
            } while (localBuffer > maxInclusive);
            return localBuffer;
        }
        public ulong GenerateRange(ulong minInclusive, ulong maxExclusive) => GenerateRange(maxExclusive - minInclusive) + minInclusive;
        public uint GenerateRange(uint maxExclusive) => (uint)GenerateRange((ulong)maxExclusive);
        public uint GenerateRange(uint minInclusive, uint maxExclusive) => GenerateRange(maxExclusive - minInclusive) + minInclusive;
        public ushort GenerateRange(ushort maxExclusive) => (ushort)GenerateRange((ulong)maxExclusive);
        public ushort GenerateRange(ushort minInclusive, ushort maxExclusive) => (ushort)(GenerateRange((ushort)(maxExclusive - minInclusive)) + minInclusive);
        public byte GenerateRange(byte maxExclusive) => (byte)GenerateRange((ulong)maxExclusive);
        public byte GenerateRange(byte minInclusive, byte maxExclusive) => (byte)(GenerateRange((byte)(maxExclusive - minInclusive)) + minInclusive);

        readonly nuint IRandomNumberProvider.GetSizeToGenerateAtLeast(nuint elements) => elements;
        readonly nuint IRandomNumberProvider.GetUnitsToGenerateAtLeast(nuint elements) => elements;

        readonly nuint IRandomNumberProvider.GetSizeToGenerateAtMost(nuint elements) => elements;
        readonly nuint IRandomNumberProvider.GetUnitsToGenerateAtMost(nuint elements) => elements;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public nuint Generate(scoped NativeSpan<byte> destination)
        {
            var dst = destination;
            if (dst.IsEmpty) return 0;
            // Read from buffer
            var dequeued = Buffer.DequeueRange(dst);
            if (dequeued < dst.Length)
            {
                dst = dst.Slice(dequeued);
                // Read directly from source
                var generated = source.Generate(dst);
                if (generated < dst.Length)
                {
                    dst = dst.Slice(generated);
                    // Refill the buffer and read from buffer again
                    EnsureBuffer(dst.Length, RefillSize);
                    Buffer.DequeueRange(dst);
                }
            }
            return destination.Length;
        }
        public void Generate<TBufferWriter>(TBufferWriter bufferWriter, nuint units = 1U) where TBufferWriter : class, IBufferWriter<byte>
        {
            ArgumentNullException.ThrowIfNull(bufferWriter);
            var y = units;
            if (y == 0) return;
            if (y == 1)
            {
                bufferWriter.Write([ReadByte()]);
                return;
            }
            var dataWriter = DataWriter<byte>.CreateFrom(ref bufferWriter, units);
            try
            {
                Generate(ref dataWriter);
            }
            finally
            {
                dataWriter.Dispose();
            }
            return;
        }

        public void Generate<TBufferWriter>(scoped ref TBufferWriter bufferWriter, nuint units = 1U) where TBufferWriter : struct, IBufferWriter<byte>
        {
            ArgumentNullException.ThrowIfNull(bufferWriter);
            var y = units;
            if (y == 0) return;
            if (y == 1)
            {
                bufferWriter.Write([ReadByte()]);
                return;
            }
            var dataWriter = DataWriter<byte>.CreateFrom(ref bufferWriter, units);
            try
            {
                Generate(ref dataWriter);
            }
            finally
            {
                dataWriter.Dispose();
            }
            return;
        }

        public void Generate<TBufferWriter>(scoped ref DataWriter<byte, TBufferWriter> dataWriter) where TBufferWriter : IBufferWriter<byte>
        {
            var f = dataWriter.TryGetRemainingElementsToWrite(out var y);
            if (dataWriter.IsCompleted || y == 0 || !f) return;
            if (y == 1)
            {
                dataWriter.WriteAtMost([ReadByte()]);
                return;
            }
            try
            {
                var buffer = Buffer;
                buffer.DequeueRangeAtMost(ref dataWriter);
                if (!dataWriter.IsCompleted)
                {
                    var provider = source;
                    provider.Generate(ref dataWriter);
                    source = provider;
                    f = dataWriter.TryGetRemainingElementsToWrite(out y);
                    Debug.Assert(f);
                    if (y > 0)
                    {
                        EnsureBuffer(y, RefillSize);
                        buffer.DequeueRangeAtMost(ref dataWriter);
                    }
                }
            }
            finally
            {
                dataWriter.Dispose();
            }
            return;
        }

        public void Shuffle<T>(scoped NativeSpan<T> values)
        {
            switch (values.Length)
            {
                case < 2:
                    return;
                case 2:
                    PermutationUtils.Shuffle2(values, (GenerateBitsUInt64(1) & 1) > 0);
                    return;
                case 7:
                    PermutationUtils.Shuffle7(values, GenerateRange(5040u));
                    return;
                default:
                    var factorials = MathUtils.Factorials;
                    if ((uint)values.Length < (uint)factorials.Length)
                    {
                        var f = factorials[(int)values.Length];
                        if (f > uint.MaxValue)
                        {
                            PermutationUtils.ShuffleAnySmallUInt64(values, GenerateRange(f));
                        }
                        else
                        {
                            PermutationUtils.ShuffleAnySmall(values, GenerateRange((uint)f));
                        }
                        return;
                    }
                    ShuffleAnyLarge(values);
                    return;
            }
        }

        internal void ShuffleAnyLarge<T>(scoped NativeSpan<T> d)
        {
            for (var i = d.Length - 1; i > 0; i--)
            {
                var v = (nuint)GenerateRange(i + 1);
                Debug.Assert(v < d.Length);
                var m = v;
                ref var l = ref d.ElementAtUnchecked(m);
                ref var k = ref d.ElementAtUnchecked(i);
                (k, l) = (l, k);
            }
        }


    }
}
