﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ModernMemory.DataFlow;

namespace ModernMemory.Randomness
{
    public sealed class StandardRandomNumberProvider(Random random) : IRandomNumberProvider
    {
        private Random Source { get; } = random ?? throw new ArgumentNullException(nameof(random));

        private const int BlockSize = 16;
        private const int LengthMask = 0x7fff_ffff & -BlockSize;
        private const int ShiftBits = 4;

        public uint GenerateUnit => BlockSize;

        public nuint Generate(scoped NativeSpan<byte> destination)
        {
            destination = destination.Slice(0, destination.Length & (~(nuint)BlockSize + 1));
            var dw = destination.AsDataWriter();
            Generate(ref dw);
            return dw.GetElementsWritten();
        }

        public void Generate<TBufferWriter>(scoped ref TBufferWriter bufferWriter, nuint units = 1) where TBufferWriter : struct, IBufferWriter<byte>
        {
            ArgumentNullException.ThrowIfNull(bufferWriter);
            var dw = DataWriter<byte>.CreateFrom(ref bufferWriter, units);
            try
            {
                Generate(ref dw);
            }
            finally
            {
                dw.Dispose();
            }
        }

        public void Generate<TBufferWriter>(TBufferWriter bufferWriter, nuint units = 1) where TBufferWriter : class, IBufferWriter<byte>
        {
            ArgumentNullException.ThrowIfNull(bufferWriter);
            var dw = DataWriter<byte>.CreateFrom(ref bufferWriter, units);
            try
            {
                Generate(ref dw);
            }
            finally
            {
                dw.Dispose();
            }
        }

        public void Generate<TBufferWriter>(scoped ref DataWriter<byte, TBufferWriter> dataWriter) where TBufferWriter : IBufferWriter<byte>
        {
            if (dataWriter.IsCompleted || !dataWriter.IsLengthConstrained) return;
            var r = Source;
            while (!dataWriter.IsCompleted)
            {
                var d = dataWriter.TryGetNativeSpan(BlockSize);
                if (d.Length < BlockSize) return;
                var d2 = d.GetHeadSpan();
                d2 = d2.Slice(0, d2.Length & LengthMask);
                r.NextBytes(d2);
                dataWriter.Advance(d.Length);
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
