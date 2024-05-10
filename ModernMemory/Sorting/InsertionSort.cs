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
        public static void SortByStaticProxy<T, TProxy>(NativeSpan<T?> values) where TProxy : unmanaged, IStaticComparer<T>
        {
            var span = values;
            ref var head = ref span.Head;
            nuint length = span.Length;
            nuint sorted = 0;
            while (++sorted < length)
            {
                nuint i = sorted;
                var newItem = span.ElementAtUnchecked(sorted);
                do
                {
                    i--;
                } while (i < sorted && TProxy.Compare(newItem, span.ElementAtUnchecked(i)) < 0);
                var index = i + 1;
                if (index == sorted) continue;
                NativeMemoryUtils.MoveMemory(ref Unsafe.Add(ref head, index + 1), ref Unsafe.Add(ref head, index), sorted - index);
                Unsafe.Add(ref head, index) = newItem;
            }
        }

        public static void Sort<T>(NativeSpan<T?> values) where T : IComparisonOperators<T, T, bool>
            => SortByStaticProxy<T, ComparisonOperatorsStaticComparer<T>>(values);

        public static void Sort<T, TProxy>(NativeSpan<T?> values, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var span = values;
            ref var head = ref span.Head;
            nuint length = span.Length;
            nuint sorted = 0;
            var cmp = TProxy.Load(in proxy);
            while (++sorted < length)
            {
                nuint i = sorted;
                var newItem = span.ElementAtUnchecked(sorted);
                do
                {
                    i--;
                } while (i < sorted && TProxy.Compare(newItem, span.ElementAtUnchecked(i), cmp) < 0);
                var index = i + 1;
                if (index == sorted) continue;
                NativeMemoryUtils.MoveMemory(ref Unsafe.Add(ref head, index + 1), ref Unsafe.Add(ref head, index), sorted - index);
                Unsafe.Add(ref head, index) = newItem;
            }
        }

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static nuint FindFirstElementGreaterThanStatic<T, TProxy>(ReadOnlyNativeSpan<T?> values, T? value) where TProxy : unmanaged, IStaticComparer<T>
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
