using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Utils;

namespace ModernMemory.Sorting
{
    /*
     * Holy Grailsort
     * MIT License
     *
     * Copyright (c) 2013 Andrey Astrelin
     * Copyright (c) 2020-2021 The Holy Grail Sort Project
     *
     * Permission is hereby granted, free of charge, to any person obtaining a copy
     * of this software and associated documentation files (the "Software"), to deal
     * in the Software without restriction, including without limitation the rights
     * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
     * copies of the Software, and to permit persons to whom the Software is
     * furnished to do so, subject to the following conditions:
     *
     * The above copyright notice and this permission notice shall be included in all
     * copies or substantial portions of the Software.
     *
     * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
     * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
     * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
     * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
     * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
     * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
     * SOFTWARE.
     */

    public readonly struct AdaptiveOptimizedGrailSort
    {
        internal readonly ref struct GrailSortContext<T, TArray> where TArray : struct, IFixedGenericInlineArray<T, TArray>
        {
            private readonly ref TArray array;
            private readonly NativeSpan<T> values;

            public GrailSortContext(NativeSpan<T> values, ref TArray array)
            {
                this.values = values;
                this.array = ref array;
            }

            public NativeSpan<T> NativeSpan => values;
            internal static Span<T> GetBuffer(ref GrailSortContext<T, TArray> context) => TArray.AsSpan(ref context.array);
        }
        internal static void Rotate<T>(ref T head, nuint leftLength, nuint rightLength)
        {
            var totalLength = leftLength + rightLength;
            var span = new NativeSpan<T>(ref head, totalLength);
            nuint start = 0;
            var headLength = leftLength;
            var tailLength = rightLength;
            var mid = start + headLength;
            var end = totalLength;
            var minLen = nuint.Min(headLength, tailLength);
            while (true)
            {
                if (tailLength == headLength)
                {
                    if (headLength > 0)
                    {
                        NativeMemoryUtils.SwapValues(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, mid), headLength);
                    }
                    return;
                }
                if (minLen <= 1) break;
                if (headLength <= tailLength)
                {
                    var tail = end - headLength;
                    while (tail <= end && tail >= mid)
                    {
                        NativeMemoryUtils.SwapValues(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, tail), headLength);
                        end = tail;
                        tail -= headLength;
                    }
                    if (end <= mid)
                    {
                        return;
                    }
                    else
                    {
                        var ml = end - mid;
                        end = mid;
                        mid -= ml;
                        NativeMemoryUtils.SwapValues(ref Unsafe.Add(ref head, mid), ref Unsafe.Add(ref head, end), ml);
                        tailLength = ml;
                        headLength -= ml;
                    }
                }
                else
                {
                    var tail = start + tailLength;
                    while (tail >= start && tail <= mid)
                    {
                        NativeMemoryUtils.SwapValues(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, mid), tailLength);
                        start = tail;
                        tail += tailLength;
                    }
                    if (start >= mid)
                    {
                        return;
                    }
                    else
                    {
                        var ml = mid - start;
                        NativeMemoryUtils.SwapValues(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, mid), ml);
                        start = mid;
                        headLength = ml;
                        tailLength -= ml;
                    }
                }
                mid = start + headLength;
                minLen = nuint.Min(headLength, tailLength);
            }
            Debug.Assert(minLen <= 1);
            if (minLen == 1)
            {
                if (headLength == 1)
                {
                    Debug.Assert(start + tailLength + 1 <= totalLength);
                    InsertForwards(ref Unsafe.Add(ref head, start), tailLength);
                }
                else
                {
                    Debug.Assert(start + headLength + 1 <= totalLength);
                    InsertBackwards(ref Unsafe.Add(ref head, start), headLength);
                }
            }
            _ = span;
        }

        internal static void RotateReferences<T>(ref T head, nuint leftLength, nuint rightLength)
        {
            Unsafe.SkipInit(out FixedArray64<T> buf);
            RotateBuffered(ref head, leftLength, rightLength, ref buf);
        }

        private static void RotateBuffered<T, TArray>(ref T head, nuint leftLength, nuint rightLength, ref TArray buf)
            where TArray : struct, IFixedGenericInlineArray<T, TArray>
        {
            var totalLength = leftLength + rightLength;
            var span = new NativeSpan<T>(ref head, totalLength);
            nuint start = 0;
            var headLength = leftLength;
            var tailLength = rightLength;
            var mid = start + headLength;
            var end = totalLength;
            var minLen = nuint.Min(headLength, tailLength);
            var sizeThreshold = (nuint)TArray.Count;
            while (true)
            {
                if (tailLength == headLength)
                {
                    if (headLength > 0)
                    {
                        NativeMemoryUtils.SwapReferenceBuffered(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, mid), headLength, ref buf);
                    }
                    return;
                }
                if (minLen <= sizeThreshold) break;
                if (headLength <= tailLength)
                {
                    var tail = end - headLength;
                    while (tail <= end && tail >= mid)
                    {
                        NativeMemoryUtils.SwapReferenceBuffered(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, tail), headLength, ref buf);
                        end = tail;
                        tail -= headLength;
                    }
                    if (end <= mid)
                    {
                        return;
                    }
                    else
                    {
                        var ml = end - mid;
                        end = mid;
                        mid -= ml;
                        NativeMemoryUtils.SwapReferenceBuffered(ref Unsafe.Add(ref head, mid), ref Unsafe.Add(ref head, end), ml, ref buf);
                        tailLength = ml;
                        headLength -= ml;
                    }
                }
                else
                {
                    var tail = start + tailLength;
                    while (tail >= start && tail <= mid)
                    {
                        NativeMemoryUtils.SwapReferenceBuffered(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, mid), tailLength, ref buf);
                        start = tail;
                        tail += tailLength;
                    }
                    if (start >= mid)
                    {
                        return;
                    }
                    else
                    {
                        var ml = mid - start;
                        NativeMemoryUtils.SwapReferenceBuffered(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, mid), ml, ref buf);
                        start = mid;
                        headLength = ml;
                        tailLength -= ml;
                    }
                }
                mid = start + headLength;
                minLen = nuint.Min(headLength, tailLength);
            }
            Debug.Assert(minLen <= sizeThreshold);
            if (minLen > 0)
            {
                if (headLength <= sizeThreshold)
                {
                    Debug.Assert(start + tailLength + headLength <= totalLength);
                    InsertForwardsBuffered(ref Unsafe.Add(ref head, start), tailLength, headLength, ref buf);
                }
                else
                {
                    Debug.Assert(start + headLength + tailLength <= totalLength);
                    InsertBackwardsBuffered(ref Unsafe.Add(ref head, start), headLength, tailLength, ref buf);
                }
            }
            _ = span;
        }

        internal static void InsertBufferForwardsUnordered<T>(ref T head, nuint bufferLength, nuint leftLength)
        {
            if (bufferLength == 0 || leftLength == 0) return;
            var totalLength = leftLength + bufferLength;
            ArgumentOutOfRangeException.ThrowIfLessThan(totalLength, bufferLength);
            var span = new NativeSpan<T>(ref head, totalLength);
            var headLength = bufferLength;
            var tailLength = leftLength;
            var bufferPos = (nuint)0;
            while (tailLength >= headLength)
            {
                var bp = bufferPos;
                bufferPos += headLength;
                NativeMemoryUtils.SwapValues(ref Unsafe.Add(ref head, bp), ref Unsafe.Add(ref head, bufferPos), headLength);
                tailLength -= headLength;
            }
            if (tailLength > 0)
            {
                NativeMemoryUtils.SwapValues(ref Unsafe.Add(ref head, bufferPos), ref Unsafe.Add(ref head, bufferPos + headLength), tailLength);
            }
            _ = span;
        }

        internal static void InsertBufferBackwardsUnordered<T>(ref T head, nuint rightLength, nuint bufferLength)
        {
            if (bufferLength == 0 || rightLength == 0) return;
            var totalLength = rightLength + bufferLength;
            var span = new NativeSpan<T>(ref head, totalLength);
            var headLength = rightLength;
            var hp = headLength;
            while (hp >= bufferLength && hp <= headLength)
            {
                var mid = hp;
                hp -= bufferLength;
                NativeMemoryUtils.SwapValues(ref Unsafe.Add(ref head, hp), ref Unsafe.Add(ref head, mid), bufferLength);
            }
            headLength = hp > headLength ? 0 : hp;
            if (headLength > 0)
            {
                NativeMemoryUtils.SwapValues(ref head, ref Unsafe.Add(ref head, bufferLength), headLength);
            }
            _ = span;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="head"></param>
        /// <param name="count">The number of elements to be shifted.</param>
        internal static void InsertForwards<T>(ref T head, nuint count)
        {
            var item = head;
            NativeMemoryUtils.MoveMemory(ref head, ref Unsafe.Add(ref head, 1), count);
            Unsafe.Add(ref head, count) = item;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="head"></param>
        /// <param name="count">The number of elements to be shifted.</param>
        internal static void InsertForwardsBuffered<T, TArray>(ref T head, nuint count, nuint headLength, ref TArray buf)
            where TArray : struct, IFixedGenericInlineArray<T, TArray>
        {
            var bfs = TArray.AsSpan(ref buf).Slice(0, (int)headLength);
            NativeMemoryUtils.MoveMemory(ref MemoryMarshal.GetReference(bfs), ref head, headLength);
            NativeMemoryUtils.MoveMemory(ref head, ref Unsafe.Add(ref head, headLength), count);
            NativeMemoryUtils.MoveMemory(ref Unsafe.Add(ref head, count), ref MemoryMarshal.GetReference(bfs), headLength);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="head"></param>
        /// <param name="count">The number of elements to be shifted.</param>
        internal static void InsertBackwards<T>(ref T head, nuint count)
        {
            var item = Unsafe.Add(ref head, count);
            NativeMemoryUtils.MoveMemory(ref Unsafe.Add(ref head, 1), ref head, count);
            head = item;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="head"></param>
        /// <param name="count">The number of elements to be shifted.</param>
        internal static void InsertBackwardsBuffered<T, TArray>(ref T head, nuint count, nuint tailLength, ref TArray buf)
            where TArray : struct, IFixedGenericInlineArray<T, TArray>
        {
            var bfs = TArray.AsSpan(ref buf).Slice(0, (int)tailLength);
            NativeMemoryUtils.MoveMemory(ref MemoryMarshal.GetReference(bfs), ref Unsafe.Add(ref head, count), tailLength);
            NativeMemoryUtils.MoveMemory(ref Unsafe.Add(ref head, tailLength), ref head, count);
            NativeMemoryUtils.MoveMemory(ref head, ref MemoryMarshal.GetReference(bfs), tailLength);
        }

        /// <summary>
        /// A.K.A. grailBinarySearchLeft
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProxy"></typeparam>
        /// <param name="head"></param>
        /// <param name="length"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static nuint FindFirstElementGreaterThanOrEqualToStatic<T, TProxy>(ref readonly T head, nuint length, T value) where TProxy : IStaticComparisonProxy<T>
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
                nint c = TProxy.Compare(NativeMemoryUtils.Add(in head, start), value);
                start = (nuint)(c >> ~0);
                k &= start;
                start = m + k;
            }
            return start;
        }

        /// <summary>
        /// A.K.A. grailBinarySearchRight
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProxy"></typeparam>
        /// <param name="head"></param>
        /// <param name="length"></param>
        /// <param name="value"></param>
        /// <returns></returns>
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

        internal static nuint CollectKeys<T, TProxy>(NativeSpan<T> values, nuint idealKeyCount) where TProxy : IStaticComparisonProxy<T>
        {
            nuint keysFound = 1;
            nuint firstKey = 0;
            nuint current = 0;
            ref var head = ref values[0];
            var length = values.Length;
            while (++current < length && keysFound < idealKeyCount)
            {
                var insert = NativeMemoryExtensions.BinarySearch<T, TProxy>(ref Unsafe.Add(ref head, firstKey), keysFound, Unsafe.Add(ref head, current), out var exactMatch);
                if (!exactMatch)
                {
                    Rotate(ref Unsafe.Add(ref head, firstKey), keysFound, current - (firstKey + keysFound));
                    firstKey = current - keysFound;
                    if (keysFound != insert) // Same optimization as holy grail sort
                    {
                        InsertBackwards(ref Unsafe.Add(ref head, firstKey + insert), keysFound - insert);
                    }
                    keysFound++;
                }
            }
            Rotate(ref head, firstKey, keysFound);
            return keysFound;
        }

        internal static void SortPairsWithKeys<T, TProxy>(NativeSpan<T> values) where TProxy : IStaticComparisonProxy<T>
        {
            var v0 = values[0];
            var v1 = values[1];
            SortPairs<T, TProxy>(values.Slice(0, values.Length - 2));
            values[values.Length - 2] = v0;
            values[values.Length - 1] = v1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void SortPairs<T, TProxy>(NativeSpan<T> values) where TProxy : IStaticComparisonProxy<T>
        {
            nuint i = 2;
            ref var head = ref values[0];
            var length = values.Length;
            var ol = length - 1;
            if (ol < length)
            {
                for (; i < ol; i += 2)
                {
                    var left = i;
                    var right = i + 1;
                    Debug.Assert(right < length);
                    ref var lh = ref Unsafe.Add(ref head, left);
                    ref var rh = ref Unsafe.Add(ref head, right);
                    Debug.Assert(left - 2 < length);
                    if (TProxy.Compare(in lh, in rh) > 0)
                    {
                        Unsafe.Add(ref head, left - 2) = rh;
                        Unsafe.Add(ref head, right - 2) = lh;
                    }
                    else
                    {
                        Unsafe.Add(ref head, left - 2) = lh;
                        Unsafe.Add(ref head, right - 2) = rh;
                    }
                }
                if (i < length)
                {
                    Debug.Assert(i - 2 < length);
                    Unsafe.Add(ref head, i - 2) = Unsafe.Add(ref head, i);
                }
            }
        }

        internal static void MergeForwardsLargeStruct<T, TProxy>(ref T bufferHead, nuint bufferLength, nuint leftLength, nuint rightLength) where TProxy : IStaticComparisonProxy<T>
        {
            ref var head = ref bufferHead;
            nuint buffer = 0;
            nuint left = bufferLength;
            var leftEnd = left + leftLength;
            var right = leftEnd;
            var end = leftEnd + rightLength;
            var span = new NativeSpan<T>(ref bufferHead, bufferLength + leftLength + rightLength);
            while (right < end && left < leftEnd)
            {
                ref var rB = ref Unsafe.Add(ref bufferHead, buffer);
                ref var rL = ref Unsafe.Add(ref head, left);
                ref var rR = ref Unsafe.Add(ref head, right);
                if (TProxy.Compare(in rL, in rR) > 0)
                {
                    (rB, rR) = (rR, rB);
                    right++;
                }
                else
                {
                    (rB, rL) = (rL, rB);
                    left++;
                }
                buffer++;
            }

            if (right < end)
            {
                if (left >= leftEnd)
                {
                    InsertBufferForwardsUnordered(ref Unsafe.Add(ref bufferHead, buffer), right - buffer, end - right);
                }
                else
                {
                    Debug.Assert(left >= leftEnd);
                    while (right < end)
                    {
                        ref var rB = ref Unsafe.Add(ref bufferHead, buffer);
                        ref var rR = ref Unsafe.Add(ref head, right);
                        (rB, rR) = (rR, rB);
                        right++;
                        buffer++;
                    }
                }
#if !DEBUG
                return;
#endif
            }
            if (buffer != left)
            {
                InsertBufferForwardsUnordered(ref Unsafe.Add(ref bufferHead, buffer), left - buffer, leftEnd - left);
            }
            _ = span;
        }

        internal static void MergeBackwardsLargeStruct<T, TProxy>(ref T head, nuint leftLength, nuint rightLength, nuint bufferLength) where TProxy : IStaticComparisonProxy<T>
        {
            var left = leftLength - 1;
            var right = rightLength - 1;
            var buffer = leftLength + rightLength + bufferLength - 1;
            var span = new NativeSpan<T>(ref head, bufferLength + leftLength + rightLength);
            while (left < leftLength && right < rightLength)
            {
                var ro = right + leftLength;
                ref var rB = ref Unsafe.Add(ref head, buffer);
                ref var rL = ref Unsafe.Add(ref head, left);
                ref var rR = ref Unsafe.Add(ref head, ro);
                if (TProxy.Compare(in rL, in rR) > 0)
                {
                    (rB, rL) = (rL, rB);
                    left--;
                }
                else
                {
                    (rB, rR) = (rR, rB);
                    right--;
                }
                buffer--;
            }
            right += leftLength;
            if (left < leftLength)
            {
                InsertBufferBackwardsUnordered(ref head, left + 1, buffer - left);
                return;
            }
            if (right != buffer)
            {
                InsertBufferBackwardsUnordered(ref Unsafe.Add(ref head, leftLength), right - leftLength + 1, buffer - right);
            }
            _ = span;
        }

    }
}
