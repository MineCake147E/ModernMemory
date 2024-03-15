using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Randomness
{
    public sealed class CryptographicRandomNumberProvider(RandomNumberGenerator source) : IRandomNumberProvider
    {
        RandomNumberGenerator Source { get; } = source ?? throw new ArgumentNullException(nameof(source));
        private const int BlockSize = 256;

        private const int LengthMask = 0x7FFF_FFFF & -BlockSize;
        private const int ShiftBits = 8;

        public uint GenerateUnit => BlockSize;

        public int Generate(Span<byte> destination)
        {
            destination = destination.Slice(0, destination.Length & LengthMask);
            Source.GetBytes(destination);
            return destination.Length;
        }

        public void Generate<TBufferWriter>(TBufferWriter bufferWriter, nuint units = 1) where TBufferWriter : IBufferWriter<byte>
        {
            ArgumentNullException.ThrowIfNull(bufferWriter);
            var y = units;
            var r = Source;
            while (y > 0)
            {
                var d = bufferWriter.GetSpan();
                if (d.Length < BlockSize) d = bufferWriter.GetSpan(BlockSize);
                if (y * BlockSize <= int.MaxValue && d.Length > (int)y * BlockSize) d = d.Slice(0, (int)y * BlockSize);
                d = d.Slice(0, d.Length & LengthMask);
                r.GetBytes(d);
                bufferWriter.Advance(d.Length);
                y -= (uint)d.Length / BlockSize;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public int GetSizeToGenerateAtLeast(int elements)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);
            var r = GetSizeToGenerateAtLeast((nuint)elements);
            return r > int.MaxValue ? -1 : (int)r;
        }

        public nuint GetSizeToGenerateAtLeast(nuint elements)
        {
            var x0 = elements + BlockSize - 1;
            return x0 & ~(nuint)(BlockSize - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public nuint GetSizeToGenerateAtLeast(nuint elements, out nuint units)
        {
            var x0 = elements + BlockSize - 1;
            units = x0 >>> ShiftBits;
            return x0 & ~(nuint)(BlockSize - 1);
        }

        public int GetSizeToGenerateAtMost(int elements)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);
            return elements & LengthMask;
        }

        public nuint GetSizeToGenerateAtMost(nuint elements) => elements & ~(nuint)(BlockSize - 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public nuint GetSizeToGenerateAtMost(nuint elements, out nuint units)
        {
            units = elements >>> ShiftBits;
            return elements & ~(nuint)(BlockSize - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public int GetUnitsToGenerateAtLeast(int elements)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);
            return (int)GetUnitsToGenerateAtLeast((nuint)elements);
        }

        public nuint GetUnitsToGenerateAtLeast(nuint elements)
        {
            var x0 = elements + BlockSize - 1;
            return x0 >>> ShiftBits;
        }

        public nuint GetUnitsToGenerateAtMost(nuint elements) => elements >>> ShiftBits;
    }
}
