using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Utils;

namespace ModernMemory.Sorting
{
    public readonly struct BinaryInsertionSort
    {
        public static void Sort<T>(NativeSpan<T> values) where T : IComparisonOperators<T, T, bool>
            => Sort<T, ComparisonOperatorsStaticComparisonProxy<T>>(values);

        public static void Sort<T, TProxy>(NativeSpan<T> values) where TProxy : IStaticComparisonProxy<T>
        {
            ref var head = ref values.Head;
            nuint length = values.Length;
            nuint sorted = 0;
            while (++sorted < length)
            {
                var newItem = values.ElementAtUnchecked(sorted);
                var tailItem = values.ElementAtUnchecked(sorted - 1);
                if (TProxy.Compare(tailItem, newItem) <= 0) continue;
                var index = FindFirstElementGreaterThanStatic<T, TProxy>(ref head, sorted, newItem);
                Debug.Assert(index < sorted);
                Debug.Assert(sorted - index <= sorted);
                NativeMemoryUtils.MoveMemory(ref Unsafe.Add(ref head, index + 1), ref Unsafe.Add(ref head, index), sorted - index);
                Unsafe.Add(ref head, index) = newItem;
            }
        }

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static nuint FindFirstElementGreaterThanStatic<T, TProxy>(ref readonly T head, nuint length, T value) where TProxy : IStaticComparisonProxy<T>
        {
            nuint start = 0;
            var len = length;
            while (len > 0)
            {
                var k = len;
                var m = start;
                len >>= 1;
                k &= 1;
                start += len;
                k += len;
                Debug.Assert(start < length);
                nint c = TProxy.Compare(value, NativeMemoryUtils.Add(in head, start));
                start = (nuint)(c >> ~0);
                k &= ~start;
                start = m + k;
            }
            return start;
        }
    }
}
