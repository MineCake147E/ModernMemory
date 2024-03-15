using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    public static partial class NativeMemoryExtensions
    {
        #region BinarySearch

        #region Comparable

        public static nuint BinarySearchComparable<T, TComparable>(this ReadOnlyNativeSpan<T> span, TComparable comparable, out bool exactMatch) where TComparable : IComparable<T>
            => BinarySearchComparable(ref NativeMemoryUtils.GetReference(span), span.Length, comparable, out exactMatch);

        public static nuint BinarySearchComparable<T, TComparable>(this NativeSpan<T> span, TComparable comparable, out bool exactMatch) where TComparable : IComparable<T>
            => BinarySearchComparable(ref NativeMemoryUtils.GetReference(span), span.Length, comparable, out exactMatch);

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static nuint BinarySearchComparable<T, TComparable>(ref readonly T head, nuint length, TComparable comparable, out bool exactMatch) where TComparable : IComparable<T>
        {
            nuint start = 0;
            var len = length;
            nint c = 1;
            while (len > 0)
            {
                var k = len;
                var m = start;
                len >>= 1;
                start += len;
                k &= 1;
                k += len;
                c = comparable.CompareTo(NativeMemoryUtils.Add(in head, start));
                if (c == 0) break;
                start = (nuint)(c >> ~0);
                k &= ~start;
                start = m + k;
            }
            exactMatch = c == 0;
            return start;
        }

        public static nuint BinarySearchComparableElements<T, TComparable>(this ReadOnlyNativeSpan<TComparable> span, T value, out bool exactMatch) where TComparable : IComparable<T>
            => BinarySearchComparableElements(ref NativeMemoryUtils.GetReference(span), span.Length, value, out exactMatch);

        public static nuint BinarySearchComparableElements<T, TComparable>(this NativeSpan<TComparable> span, T value, out bool exactMatch) where TComparable : IComparable<T>
            => BinarySearchComparableElements(ref NativeMemoryUtils.GetReference(span), span.Length, value, out exactMatch);

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static nuint BinarySearchComparableElements<T, TComparable>(ref readonly TComparable head, nuint length, T value, out bool exactMatch) where TComparable : IComparable<T>
        {
            nuint lo = 0;
            var hi = length - 1;
            while (lo <= hi)
            {
                var m = hi - lo;
                m = (m >> 1) + lo;
                var c = -NativeMemoryUtils.Add(in head, m).CompareTo(value);
                if (c == 0)
                {
                    exactMatch = true;
                    return m;
                }
                if (c < 0)
                {
                    hi = m - 1;
                }
                else
                {
                    lo = m + 1;
                }
            }
            exactMatch = false;
            return lo;
        }

        #endregion

        #region ComparisonOperators

        public static nuint BinarySearchComparisonOperators<T>(this ReadOnlyNativeSpan<T> span, T value, out bool exactMatch) where T : IComparisonOperators<T, T, bool>
            => BinarySearchComparisonOperators(ref NativeMemoryUtils.GetReference(span), span.Length, value, out exactMatch);

        public static nuint BinarySearchComparisonOperators<T>(this NativeSpan<T> span, T value, out bool exactMatch) where T : IComparisonOperators<T, T, bool>
            => BinarySearchComparisonOperators(ref NativeMemoryUtils.GetReference(span), span.Length, value, out exactMatch);

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static nuint BinarySearchComparisonOperators<T>(ref readonly T head, nuint length, T value, out bool exactMatch) where T : IComparisonOperators<T, T, bool>
        {
            nuint start = 0;
            var len = length;
            nint c = 1;
            while (len > 0)
            {
                var k = len;
                var m = start;
                len >>= 1;
                k &= 1;
                start += len;
                k += len;
                c = value.CompareToByComparisonOperators(NativeMemoryUtils.Add(in head, start));
                if (c == 0) break;
                start = (nuint)(c >> ~0);
                k &= ~start;
                start = m + k;
            }
            exactMatch = c == 0;
            return start;
        }
        #endregion

        #region RangeComparable
        public static nuint BinarySearchRangeComparable<TRange, TIndex>(this ReadOnlyNativeSpan<TRange> span, TIndex index, out bool exactMatch) where TRange : IRangeComparable<TIndex>
            => BinarySearchRangeComparable(ref NativeMemoryUtils.GetReference(span), span.Length, index, out exactMatch);

        public static nuint BinarySearchRangeComparable<TRange, TIndex>(this NativeSpan<TRange> span, TIndex index, out bool exactMatch) where TRange : IRangeComparable<TIndex>
            => BinarySearchRangeComparable(ref NativeMemoryUtils.GetReference(span), span.Length, index, out exactMatch);

        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static nuint BinarySearchRangeComparable<TRange, TIndex>(ref readonly TRange head, nuint length, TIndex index, out bool exactMatch) where TRange : IRangeComparable<TIndex>
        {
            nuint start = 0;
            var len = length;
            nint c = 1;
            while (len > 0)
            {
                var k = len;
                var m = start;
                len >>= 1;
                k &= 1;
                start += len;
                k += len;
                c = -NativeMemoryUtils.Add(in head, start).CompareTo(index);
                if (c == 0) break;
                start = (nuint)(c >> ~0);
                k &= ~start;
                start = m + k;
            }
            exactMatch = c == 0;
            return start;
        }
        #endregion

        #endregion
    }
}
