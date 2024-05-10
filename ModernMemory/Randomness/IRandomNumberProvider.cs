using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ModernMemory.DataFlow;

namespace ModernMemory.Randomness
{
    public interface IRandomNumberProvider
    {
        nuint GenerateUnit
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => GetSizeToGenerateAtLeast(1u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        nuint GetSizeToGenerateAtLeast(nuint elements);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        nuint GetSizeToGenerateAtLeast(nuint elements, out nuint units)
        {
            var res = GetSizeToGenerateAtLeast(elements);
            units = GetUnitsToGenerateAtLeast(res);
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        nuint GetSizeToGenerateAtMost(nuint elements);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        nuint GetSizeToGenerateAtMost(nuint elements, out nuint units)
        {
            var res = GetSizeToGenerateAtMost(elements);
            units = GetUnitsToGenerateAtMost(res);
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        nuint GetUnitsToGenerateAtLeast(nuint elements);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        nuint GetUnitsToGenerateAtMost(nuint elements);

        nuint Generate(scoped NativeSpan<byte> destination);

        void Generate<TBufferWriter>(TBufferWriter bufferWriter, nuint units = 1) where TBufferWriter : class, IBufferWriter<byte>;
        void Generate<TBufferWriter>(scoped ref TBufferWriter bufferWriter, nuint units = 1) where TBufferWriter : struct, IBufferWriter<byte>;

        void Generate<TBufferWriter>(scoped ref DataWriter<byte, TBufferWriter> dataWriter) where TBufferWriter : IBufferWriter<byte>;
    }
}
