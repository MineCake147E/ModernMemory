using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Randomness
{
    public interface IRandomNumberProvider
    {
        uint GenerateUnit
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => (uint)GetSizeToGenerateAtLeast(1u);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        int GetSizeToGenerateAtLeast(int elements)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);
            var r = GetSizeToGenerateAtLeast((nuint)elements);
            return r > int.MaxValue ? -1 : (int)r;
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
        int GetSizeToGenerateAtMost(int elements)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);
            return (int)GetSizeToGenerateAtMost((nuint)elements);
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
        int GetUnitsToGenerateAtLeast(int elements)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);
            return (int)GetUnitsToGenerateAtLeast((nuint)elements);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        nuint GetUnitsToGenerateAtLeast(nuint elements);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        int GetUnitsToGenerateAtMost(int elements)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elements);
            return (int)GetUnitsToGenerateAtMost((nuint)elements);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        nuint GetUnitsToGenerateAtMost(nuint elements);

        int Generate(Span<byte> destination);

        void Generate<TBufferWriter>(TBufferWriter bufferWriter, nuint units = 1) where TBufferWriter : IBufferWriter<byte>;
    }
}
