using System;
using System.Collections;
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
    public readonly struct InsertionSort
    {
        public static void Sort<T>(NativeSpan<T?> values) where T : IComparisonOperators<T, T, bool>
            => Sort<T, ComparisonOperatorsStaticComparisonProxy<T>>(values);

        public static void Sort<T, TProxy>(NativeSpan<T?> values) where TProxy : IStaticComparisonProxy<T>
        {
            var span = values;
            ref var head = ref span.Head;
            nuint length = span.Length;
            nuint sorted = 0;
            while (++sorted < length)
            {
                var newItem = span.ElementAtUnchecked(sorted);
                var tailItem = span.ElementAtUnchecked(sorted - 1);
                if (TProxy.Compare(tailItem, newItem) <= 0) continue;
                var index = FindFirstElementGreaterThanStatic<T, TProxy>(span.SliceWhileIfLongerThan(sorted - 1), newItem);
                Debug.Assert(index < sorted);
                Debug.Assert(sorted - index <= sorted);
                NativeMemoryUtils.MoveMemory(ref Unsafe.Add(ref head, index + 1), ref Unsafe.Add(ref head, index), sorted - index);
                Unsafe.Add(ref head, index) = newItem;
            }
        }

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static nuint FindFirstElementGreaterThanStatic<T, TProxy>(ReadOnlyNativeSpan<T?> values, T? value) where TProxy : IStaticComparisonProxy<T>
        {
            ref readonly var head = ref values.Head;
            nuint length = values.Length;
            nuint i = length - 1;
            while (i < length)
            {
                nint c = TProxy.Compare(value, NativeMemoryUtils.Add(in head, i));
                if (c >= 0) break;
                i--;
            }
            return i + 1;
        }
    }
}
