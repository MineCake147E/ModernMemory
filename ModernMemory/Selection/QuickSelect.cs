using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Selection
{
    public readonly partial struct QuickSelect
    {

        internal static nuint Select<T>(NativeSpan<T> values, nuint rank, bool randomizedHead = false)
            where T : IComparable<T>
        {
            nuint result = 0;
            rank = nuint.Min(values.Length - 1, rank);
            var sliced = values;
            nuint steps = 0;
            bool badPartition = false;
            bool localRandomizedHead = randomizedHead;
            while (sliced.Length > 16)
            {
                steps++;
                var desiredBoundary = rank - result;
                var v = 32768 / sliced.Length;
                var minimumWidthFromBoundaries = nuint.Min(desiredBoundary, sliced.Length - desiredBoundary - 1);
                nuint boundary;
                if (minimumWidthFromBoundaries == 0)
                {
                    if (desiredBoundary == 0)
                    {
                        MoveMinimumToHead(sliced);
                        sliced = default;
                        break;
                    }
                    else
                    {
                        MoveMaximumToTail(sliced);
                        result += desiredBoundary;
                        sliced = default;
                        break;
                    }
                }
                if (minimumWidthFromBoundaries <= v)
                {
                    if (desiredBoundary > minimumWidthFromBoundaries)
                    {
                        result += MaximumSelect(sliced, desiredBoundary);
                    }
                    else
                    {
                        result += MinimumSelect(sliced, desiredBoundary);
                    }
                    sliced = default;
                    break;
                }
                var pivotMom = !badPartition && sliced.Length < 256/* || MathUtils.AbsDiff(desiredBoundary, sliced.Length >>> 1) < sliced.Length / 8*/;
                var pivotIndex = pivotMom
                    ? PickPivotMedianOfMedians(sliced)
                    : PickPivotSampleSelect(sliced, desiredBoundary, badPartition, localRandomizedHead);
                //localRandomizedHead |= !pivotMom;
                (boundary, var alreadyPartitioned, var pivotExactMatches) = Partition(sliced, pivotIndex);
                desiredBoundary = rank - result;
                if (boundary == desiredBoundary)
                {
                    result += boundary;
                    sliced = default;
                    break;
                }
                var sizeBefore = sliced.Length;
                if (boundary == 0 && alreadyPartitioned && pivotExactMatches >= desiredBoundary)
                {
                    // There could be so many items to be the same as the pivot
                    var boundary2 = PartitionByMinimumValues(sliced);
                    if (boundary2 >= desiredBoundary)
                    {
                        result += desiredBoundary;
                        sliced = default;
                        break;
                    }
                    result += boundary2 + 1;
                    sliced = sliced.Slice(boundary2 + 1);
                }
                else
                {
                    if (boundary <= desiredBoundary)
                    {
                        result += boundary + 1;
                        sliced = sliced.Slice(boundary + 1);
                    }
                    else
                    {
                        sliced = sliced.Slice(0, boundary);
                        localRandomizedHead = false;
                    }
                }
                badPartition = sizeBefore / 8 <= sliced.Length / 7;
            }
            if (!sliced.IsEmpty)
            {
                sliced.GetHeadSpan().Sort();
                result = rank;
            }
            return result;
        }

        private static nuint PartitionByMinimumValues<T>(NativeSpan<T> values)
            where T : IComparable<T>
        {
            ref var head = ref NativeMemoryUtils.GetReference(values);
            var length = values.Length;
            var pivotValue = head;
            nuint writeIndex = 0;
            for (nuint i = 1; i < length; i++)
            {
                var t = Unsafe.Add(ref head, i);
                var c = pivotValue.CompareTo(t);
                var k = (nuint)(c > 0 ? 1u : 0) - 1;
                var newWriteIndex = writeIndex + 1;
                newWriteIndex &= k;
                if (c < 0) continue;
                Unsafe.Add(ref head, i) = pivotValue;
                Unsafe.Add(ref head, newWriteIndex) = t;
                pivotValue = t;
                writeIndex = newWriteIndex;
            }
            return writeIndex;
        }

        private static nuint MinimumSelect<T>(NativeSpan<T> values, nuint rank)
            where T : IComparable<T>
        {
            ref var head = ref NativeMemoryUtils.GetReference(values);
            var length = values.Length;
            nuint result = 0;
            while (true)
            {
                var desiredBoundary = rank - result;
                var pivotValue = head;
                nuint writeIndex = 0;
                for (nuint i = 1; i < length; i++)
                {
                    var t = Unsafe.Add(ref head, i);
                    var c = pivotValue.CompareTo(t);
                    var k = (nuint)(c > 0 ? 1u : 0) - 1;
                    var newWriteIndex = writeIndex + 1;
                    newWriteIndex &= k;
                    if (c < 0) continue;
                    Unsafe.Add(ref head, i) = pivotValue;
                    Unsafe.Add(ref head, newWriteIndex) = t;
                    pivotValue = t;
                    writeIndex = newWriteIndex;
                }
                if (writeIndex >= desiredBoundary)
                {
                    result += desiredBoundary;
                    break;
                }
                writeIndex++;
                result += writeIndex;
                head = ref Unsafe.Add(ref head, writeIndex)!;
                length -= writeIndex;
            }
            return result;
        }

        private static nuint MaximumSelect<T>(NativeSpan<T> values, nuint rank)
            where T : IComparable<T>
        {
            ref var head = ref NativeMemoryUtils.GetReference(values);
            var length = values.Length;
            var desiredBoundary = rank;
            while (true)
            {
                var pivotValue = Unsafe.Add(ref head, length - 1);
                var writeIndex = length - 1;
#pragma warning disable S2251 // A "for" loop update clause should move the counter in the right direction (false positive since it utilizes negative overflow)
                for (var i = length - 2; i < length; i--)
#pragma warning restore S2251 // A "for" loop update clause should move the counter in the right direction
                {
                    var t = Unsafe.Add(ref head, i);
                    var c = pivotValue.CompareTo(t);
                    var m = writeIndex ^ length;
                    var k = (nuint)(c >= 0 ? 1u : 0) - 1;
                    var newWriteIndex = writeIndex ^ (m & k);
                    newWriteIndex--;
                    if (c > 0) continue;
                    Unsafe.Add(ref head, i) = pivotValue;
                    Unsafe.Add(ref head, newWriteIndex) = t;
                    pivotValue = t;
                    writeIndex = newWriteIndex;
                }
                if (writeIndex <= desiredBoundary)
                {
                    break;
                }
                length = writeIndex;
            }
            return rank;
        }

        private static void MoveMinimumToHead<T>(NativeSpan<T> values)
            where T : IComparable<T>
        {
            ref var head = ref NativeMemoryUtils.GetReference(values);
            var length = values.Length;
            var pivotValue = head;
            for (nuint i = 1; i < length; i++)
            {
                var t = Unsafe.Add(ref head, i);
                var c = pivotValue.CompareTo(t);
                if (c <= 0) continue;
                (head, Unsafe.Add(ref head, i)) = (t, pivotValue);
                pivotValue = t;
            }
        }
        private static void MoveMaximumToTail<T>(NativeSpan<T> values)
            where T : IComparable<T>
        {
            ref var head = ref NativeMemoryUtils.GetReference(values);
            var length = values.Length;
            ref var tail = ref Unsafe.Add(ref head, length - 1);
            var pivotValue = tail;
            unchecked
            {
#pragma warning disable S2251 // A "for" loop update clause should move the counter in the right direction (false positive since it utilizes negative overflow)
                for (var i = length - 2; i < length; i--)
                {
                    var t = Unsafe.Add(ref head, i);
                    var c = pivotValue.CompareTo(t);
                    if (c >= 0) continue;
                    (tail, Unsafe.Add(ref head, i)) = (t, pivotValue);
                    pivotValue = t;
                }
#pragma warning restore S2251 // A "for" loop update clause should move the counter in the right direction
            }
        }

        private static (nuint index, bool alreadyPartitioned, nuint pivotExactMatches) Partition<T>(NativeSpan<T> values, nuint pivotIndex)
            where T : IComparable<T>
        {
            ref var head = ref NativeMemoryUtils.GetReference(values);
            var length = values.Length;
            pivotIndex = nuint.Min(pivotIndex, length - 1);
            var pivotValue = values[pivotIndex];
            (head, values[pivotIndex]) = (pivotValue, head);
            nuint pL = 0;
            var pR = length;
            nuint pivotExactMatches = 0;
            while (++pL < length)
            {
                var c = pivotValue.CompareTo(Unsafe.Add(ref head, pL));
                pivotExactMatches += c == 0 ? 1u : 0;
                if (c <= 0) break;
            }
            while (--pR > pL)
            {
                var c = pivotValue.CompareTo(Unsafe.Add(ref head, pR));
                pivotExactMatches += c == 0 ? 1u : 0;
                if (c > 0) break;
            }

            var alreadyPartitioned = pL >= pR;

            while (pL < pR && pR < length)
            {
                NativeMemoryUtils.SwapSingle(ref Unsafe.Add(ref head, pL), ref Unsafe.Add(ref head, pR));
                while (++pL < length)
                {
                    var c = pivotValue.CompareTo(Unsafe.Add(ref head, pL));
                    pivotExactMatches += c == 0 ? 1u : 0;
                    if (c <= 0) break;
                }
                while (--pR < length)
                {
                    var c = pivotValue.CompareTo(Unsafe.Add(ref head, pR));
                    pivotExactMatches += c == 0 ? 1u : 0;
                    if (c > 0) break;
                }
            }
            pL--;
            NativeMemoryUtils.SwapSingle(ref Unsafe.Add(ref head, pL), ref head);
            return (pL, alreadyPartitioned, pivotExactMatches);
        }

        private static nuint PickPivotSampleSelect<T>(NativeSpan<T> values, nuint desiredBoundary, bool badPartition = false, bool randomizedHead = false)
            where T : IComparable<T>
        {
            var length = values.Length;
            var v = BitOperations.Log2(length) / 2;
            var midpoint = length / 2;
            var newLength = length >> v;
            var newMidpoint = newLength / 2;
            var nb = (nint)(desiredBoundary - midpoint);
            if (badPartition || !randomizedHead) PermuteValues(values, length, newLength);
            if (newLength < 16) return PickPivotMedianOfMedians(values);
            var k = nb >> ~0;
            k = (v ^ k) - k;
            var sp = (nuint)((nb >> v) - k) + newMidpoint;
            var headSpan = values.SliceWhileIfLongerThan(newLength);
            return Select(headSpan, sp, true);
        }

        private static void PermuteValues<T>(NativeSpan<T> values, nuint length, nuint headLength) where T : IComparable<T>
        {
            for (nuint i = 0; i < headLength; i++)
            {
                nuint y = 0;
                Random.Shared.NextBytes(MemoryMarshal.AsBytes(new Span<nuint>(ref y)));
                y %= length - i;
                NativeMemoryUtils.SwapSingle(ref values[i], ref values[y + i]);
            }
        }

        private static nuint PickPivotSampleSort<T>(NativeSpan<T> values, nuint desiredBoundary)
            where T : IComparable<T>
        {
            var sp = values.Length >= 32 ? desiredBoundary / (values.Length / 31) : desiredBoundary;
            values.SliceWhileIfLongerThan(32).GetHeadSpan().Sort();
            return sp;
        }

        private static nuint PickPivotMedianOfMedians<T>(NativeSpan<T> values)
            where T : IComparable<T>
        {
            var midpoint = values.Length >>> 1;
            if (values.Length < 9)
            {
                values.GetHeadSpan().Sort();
            }
            else
            {
                SortThree(ref values[0]);
                SortThree(ref values[midpoint - 1]);
                SortThree(ref values[values.Length - 3]);
                SortThree(ref values[1], ref values[midpoint], ref values[values.Length - 2]);
            }
            return midpoint;
        }

        private static void SortThree<T>(scoped ref T head)
            where T : IComparable<T>
        {
            ref var v0 = ref head;
            ref var v1 = ref Unsafe.Add(ref head, 1);
            ref var v2 = ref Unsafe.Add(ref head, 2);
            SortThree(ref v0, ref v1, ref v2);
        }

        private static void SortThree<T>(ref T v0, ref T v1, ref T v2) where T : IComparable<T>
        {
            if (v0.CompareTo(v1) > 0)
            {
                NativeMemoryUtils.SwapSingle(ref v0, ref v1);
            }
            if (v0.CompareTo(v2) > 0)
            {
                NativeMemoryUtils.SwapSingle(ref v0, ref v2);
            }
            if (v1.CompareTo(v2) > 0)
            {
                NativeMemoryUtils.SwapSingle(ref v1, ref v2);
            }
        }
    }
}
