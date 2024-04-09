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
    public readonly struct ShellSort : ISortAlgorithm
    {
        internal static ReadOnlySpan<ulong> ExtendedGaps => [12335712331615664267, 5482538814051406341, 2436683917356180597, 1082970629936080265, 481320279971591229, 213920124431818325, 95075610858585923, 42255827048260411, 18780367577004627, 8346830034224279, 3709702237433013, 1648756549970229, 732780688875657, 325680306166959, 144746802740871, 64331912329277, 28591961035235, 12707538237883, 5647794772393, 2510131009953, 1115613782201, 495828347645, 220368154509, 97941402005, 43529512003, 19346449779, 8598422125, 3821520945, 1698453753, 754868335, 335497039, 149109795, 66271021, 29453787, 13090573, 5818033, 2585793, 1149241, 510775, 227011, 100895, 44843, 19931, 8859, 3937];
        internal static ReadOnlySpan<ushort> Gaps => [1751, 701, 301, 132, 57, 23, 10, 4];
        public static void SortByStaticProxy<T, TStaticComparisonProxy>(NativeSpan<T> values) where TStaticComparisonProxy : IStaticComparisonProxy<T>
        {
            if (values.Length <= 1) return;
            var gaps = Gaps.AsReadOnlyNativeSpan();
            var gp = gaps.BinarySearch<ushort, ReverseStaticComparisonProxy<ushort, ComparisonOperatorsStaticComparisonProxy<ushort>>>
                ((ushort)nuint.Min(ushort.MaxValue, (values.Length >> 1) - 1), out _);
            if (gp == 0)
            {
                var eg = ExtendedGaps.AsReadOnlyNativeSpan();
                gp = eg.BinarySearch<ulong, ReverseStaticComparisonProxy<ulong, ComparisonOperatorsStaticComparisonProxy<ulong>>>
                ((values.Length >> 1) - 1, out _);
                var mg = eg.Slice(gp);
                foreach (var gap in mg)
                {
                    ShellSortPass<T, TStaticComparisonProxy>(values, (nuint)gap);
                }
                gp = 0;
            }
            gaps = gaps.Slice(gp);
            foreach (var gap in gaps)
            {
                ShellSortPass<T, TStaticComparisonProxy>(values, gap);
            }
            GallopInsertionSort<T, TStaticComparisonProxy>(values);
        }

        private static void ShellSortPass<T, TStaticComparisonProxy>(NativeSpan<T> values, nuint gap) where TStaticComparisonProxy : IStaticComparisonProxy<T>
        {
            var vs = values;
            for (var i = gap; i < vs.Length; i++)
            {
                var j = i;
                var tmp = vs.ElementAtUnchecked(i);
                for (; j >= gap; j -= gap)
                {
                    Debug.Assert(j < vs.Length);
                    var v = vs.ElementAtUnchecked(j - gap);
                    var m = TStaticComparisonProxy.Compare(tmp, v);
                    if (m >= 0)
                    {
                        break;
                    }
                    vs.ElementAtUnchecked(j) = v;
                }
                vs.ElementAtUnchecked(j) = tmp;
            }
        }

        public static void GallopInsertionSort<T, TProxy>(NativeSpan<T> values) where TProxy : IStaticComparisonProxy<T>
        {
            ref var head = ref values.Head;
            var length = values.Length;
            nuint sorted = 0;
            while (++sorted < length)
            {
                var newItem = values.ElementAtUnchecked(sorted);
                var tailItem = values.ElementAtUnchecked(sorted - 1);
                if (TProxy.Compare(tailItem, newItem) <= 0) continue;
                var index = FindFirstElementGreaterThanOrEqualsToStatic<T, TProxy>(ref head, sorted - 1, newItem);
                Debug.Assert(index < sorted);
                Debug.Assert(sorted - index <= sorted);
                NativeMemoryUtils.MoveMemory(ref Unsafe.Add(ref head, index + 1), ref Unsafe.Add(ref head, index), sorted - index);
                Unsafe.Add(ref head, index) = newItem;
            }
        }

        [SkipLocalsInit]
        internal static nuint FindFirstElementGreaterThanOrEqualsToStatic<T, TProxy>(ref readonly T head, nuint length, T value) where TProxy : IStaticComparisonProxy<T>
        {
            var span = NativeMemoryUtils.CreateReadOnlyNativeSpan(in head, length);
            if (length == 0) return 0;
            if (length == 1)
            {
                var c = TProxy.Compare(value, head);
                return c > 0 ? (nuint)1 : 0;
            }
            //Reverse Exponential Search
            nuint start = 0;
            var gps = ~(nuint)0;
            while ((gps & length) > 0)
            {
                Debug.Assert(length + gps < length);
                var c = TProxy.Compare(value, NativeMemoryUtils.Add(in head, length + gps));
                if (c > 0) break;
                if (c == 0) return length + gps;
                gps <<= 1;
            }
            var len = length;
            if ((gps & length) > 0)
            {
                start = length + gps + 1;
            }
            gps = (nuint)((nint)gps >> 1);
            len += gps;
            if (start >= length || len > length) return length;
            if (start + len > length)
            {
                start = 0;
                len = length;
            }
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
                if (c == 0) break;
                start = (nuint)(c >> ~0);
                k &= ~start;
                start = m + k;
            }
            //if (len > 0)
            //{
            //    var c = TProxy.Compare(value, NativeMemoryUtils.Add(in head, start));
            //    start += c > 0 ? (nuint)1 : 0;
            //}
            _ = span;
            return start;
        }
    }
}
