using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
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
    #region Third-Party License Notice
    /* Holy Grailsort
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
    /* Rewritten Grailsort
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
    #endregion

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

        public enum Subarray : byte
        {
            Left = 0,
            Right = 1
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable S3265 // Non-flags enums should not be used in bitwise operations
        internal static Subarray SwitchSubarray(Subarray subarray) => subarray ^ Subarray.Right;
#pragma warning restore S3265 // Non-flags enums should not be used in bitwise operations

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Subarray SwitchSubarrayIf(Subarray subarray, bool condition)
        {
            var c = condition ? Subarray.Right : Subarray.Left;
#pragma warning disable S3265 // Non-flags enums should not be used in bitwise operations
            return subarray ^ c;
#pragma warning restore S3265 // Non-flags enums should not be used in bitwise operations
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static Subarray GetSubarray<T, TProxy>(in T currentKey, T medianKey) where TProxy : IStaticComparisonProxy<T>
            => TProxy.Compare(in currentKey, medianKey) < 0 ? Subarray.Left : Subarray.Right;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static nuint CalculateBlockSize(nuint length, out nuint blocks)
        {
            var k = length - 1;
            if (k > length) k = length;
            var m = (BitOperations.LeadingZeroCount((nuint)0) - BitOperations.LeadingZeroCount(k) + 1) >>> 1;
            blocks = ((length - 1) >> m) + 1;
            return (nuint)1 << m;
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
            if (length > 0)
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
            return 0;
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
            if (length > 0)
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
            return 0;
        }

        [SkipLocalsInit]
        internal static nuint FindFirstElementMergingStatic<T, TProxy>(ref readonly T head, nuint length, Subarray subarray, T value) where TProxy : IStaticComparisonProxy<T>
        {
            if (length > 0)
            {
                nuint start = 0;
                var v = value;
                var len = length;
                var threshold = subarray == Subarray.Right ? 0 : (nint)1;
                while (len > 0)
                {
                    var k = len;
                    var m = start;
                    len >>= 1;
                    k &= 1;
                    start += len;
                    k += len;
                    Debug.Assert(start < length);
                    var c = TProxy.Compare(v, in NativeMemoryUtils.Add(in head, start)) - threshold;
                    start = (nuint)(c >> ~0);
                    k &= ~start;
                    start = m + k;
                }
                return start;
            }
            return length;
        }

        internal static nuint CollectKeys<T, TProxy>(NativeSpan<T> values, nuint idealKeyCount) where TProxy : IStaticComparisonProxy<T>
        {
            if (values.Length <= 1) return values.Length;
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
                    var kf = keysFound++;
                    var rightLength = current - (firstKey + kf);
                    // Rotate the new key as well if key should be inserted at position 0.
                    rightLength += insert == 0 ? (nuint)1 : 0;
                    Rotate(ref Unsafe.Add(ref head, firstKey), kf, rightLength);
                    firstKey = current - kf;
                    if (kf == insert || insert == 0) // Same optimization as holy grail sort
                    {
                        continue;
                    }
                    Debug.Assert(firstKey + kf <= length);
                    InsertBackwards(ref Unsafe.Add(ref head, firstKey + insert), kf - insert);
                }
            }
            Rotate(ref head, firstKey, keysFound);
            return keysFound;
        }

        internal static void SortPairsWithKeys<T, TProxy>(NativeSpan<T> values) where TProxy : IStaticComparisonProxy<T>
        {
            var v0 = values[0];
            var v1 = values[1];
            SortPairs<T, TProxy>(values);
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
                        ref var mh = ref rh;
                        rh = ref lh;
                        lh = ref mh;
                    }
                    Unsafe.Add(ref head, left - 2) = lh;
                    Unsafe.Add(ref head, right - 2) = rh;
                }
                if (i < length)
                {
                    Debug.Assert(i - 2 < length);
                    values[i - 2] = values[i];
                }
            }
        }

        internal static void BuildInPlace<T, TProxy>(NativeSpan<T> values, nuint currentBufferOffset, nuint currentLengthExponent, nuint bufferLengthExponent)
            where TProxy : IStaticComparisonProxy<T>
            => BuildInPlace<T, TProxy>(values, currentBufferOffset, ((currentLengthExponent & 0xff) << 8) | (bufferLengthExponent & 0xff));

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void BuildInPlace<T, TProxy>(NativeSpan<T> values, nuint currentBufferOffset, nuint parameter)
            where TProxy : IStaticComparisonProxy<T>
        {
            var currentLengthExponent = (int)(parameter >> 8);
            var currentLength = (nuint)1 << currentLengthExponent;
            var length = values.Length;
            int bufferLengthExponent = (byte)parameter;
            var bufferLength = (nuint)1 << bufferLengthExponent;
            var remainingHeadBuffer = currentBufferOffset;
            var tailBuffer = bufferLength - remainingHeadBuffer;
            length -= tailBuffer;
            ref var head = ref values[0];
            nuint i;
            nuint sizeAfterMerge;
            while (remainingHeadBuffer >= currentLength)
            {
                sizeAfterMerge = currentLength + currentLength;
                i = remainingHeadBuffer - currentLength;
                var mergeEnd = length - sizeAfterMerge - currentLength + 1;
                Debug.Assert(mergeEnd < length);
                for (; i < mergeEnd; i += sizeAfterMerge)
                {
                    MergeForwardsLargeStruct<T, TProxy>(ref Unsafe.Add(ref head, i), currentLength, currentLength, currentLength);
                }
                var leftOver = length - i - currentLength;
                if (leftOver > currentLength)
                {
                    MergeForwardsLargeStruct<T, TProxy>(ref Unsafe.Add(ref head, i), currentLength, currentLength, leftOver - currentLength);
                }
                else
                {
                    InsertBufferForwardsUnordered(ref Unsafe.Add(ref head, i), currentLength, leftOver);
                }
                remainingHeadBuffer -= currentLength;
                length -= currentLength;
                tailBuffer += currentLength;
                currentLength += currentLength;
            }
            Debug.Assert(remainingHeadBuffer == 0);
            Debug.Assert(BitOperations.IsPow2(tailBuffer));
            var lastOffset = length & (~(tailBuffer * 2) + 1);
            var lastBlock = length - lastOffset;
            sizeAfterMerge = tailBuffer * 2;
            i = lastOffset;
            if (lastBlock <= tailBuffer)
            {
                InsertBufferBackwardsUnordered(ref Unsafe.Add(ref head, lastOffset), lastBlock, tailBuffer);
            }
            else
            {
                MergeBackwardsLargeStruct<T, TProxy>(ref Unsafe.Add(ref head, i), tailBuffer, lastBlock - tailBuffer, tailBuffer);
            }
            i -= sizeAfterMerge;
            while (i <= length)
            {
                MergeBackwardsLargeStruct<T, TProxy>(ref Unsafe.Add(ref head, i), tailBuffer, tailBuffer, tailBuffer);
                i -= sizeAfterMerge;
            }
        }

        internal static void BuildBlocks<T, TProxy>(NativeSpan<T> values, nuint bufferLengthExponent)
            where TProxy : IStaticComparisonProxy<T>
        {
            SortPairsWithKeys<T, TProxy>(values.Slice(((nuint)1 << (int)bufferLengthExponent) - 2));
            BuildInPlace<T, TProxy>(values, ((nuint)1 << (int)bufferLengthExponent) - 2, 1, bufferLengthExponent);
        }

        internal static void MergeForwardsLargeStruct<T, TProxy>(ref T bufferHead, nuint bufferLength, nuint leftLength, nuint rightLength) where TProxy : IStaticComparisonProxy<T>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferLength, nuint.Max(leftLength, rightLength));
            ref var head = ref bufferHead;
            nuint buffer = 0;
            var left = bufferLength;
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
                InsertBufferForwardsUnordered(ref Unsafe.Add(ref bufferHead, buffer), right - buffer, end - right);
                return;
            }
            if (buffer != left)
            {
                InsertBufferForwardsUnordered(ref Unsafe.Add(ref bufferHead, buffer), left - buffer, leftEnd - left);
            }
            _ = span;
        }

        internal static void MergeBackwardsLargeStruct<T, TProxy>(ref T head, nuint leftLength, nuint rightLength, nuint bufferLength) where TProxy : IStaticComparisonProxy<T>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferLength, nuint.Max(leftLength, rightLength));
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

        internal static (Subarray newOrigin, nuint currentBlockLength) LocalMergeForwardsLargeStruct<T, TProxy>(ref T bufferHead, nuint bufferLength, nuint leftLength, Subarray leftOrigin, nuint rightLength) where TProxy : IStaticComparisonProxy<T>
        {
            ref var head = ref bufferHead;
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferLength, nuint.Max(leftLength, rightLength));
            nuint buffer = 0;
            var left = bufferLength;
            var leftEnd = left + leftLength;
            var right = leftEnd;
            var end = leftEnd + rightLength;
            var span = new NativeSpan<T>(ref bufferHead, bufferLength + leftLength + rightLength);
            var threshold = leftOrigin == Subarray.Left ? 1 : 0;
            while (right < end && left < leftEnd)
            {
                ref var rB = ref Unsafe.Add(ref bufferHead, buffer);
                ref var rL = ref Unsafe.Add(ref head, left);
                ref var rR = ref Unsafe.Add(ref head, right);
                if (TProxy.Compare(in rL, in rR) < threshold)
                {
                    (rB, rL) = (rL, rB);
                    left++;
                }
                else
                {
                    (rB, rR) = (rR, rB);
                    right++;
                }
                buffer++;
            }
            _ = span;
            if (left < leftEnd)
            {
                var leftRem = leftEnd - left;
                InsertBufferBackwardsUnordered(ref Unsafe.Add(ref head, left), leftRem, rightLength);
                return (leftOrigin, leftRem);
            }
            else
            {
                return (SwitchSubarray(leftOrigin), end - right);
            }
        }

        internal static (Subarray newOrigin, nuint currentBlockLength) LocalMergeBackwardsLargeStruct<T, TProxy>(ref T head, nuint leftLength, nuint rightLength, nuint bufferLength, Subarray rightOrigin) where TProxy : IStaticComparisonProxy<T>
        {
            Debug.Assert(leftLength > 0);
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferLength, nuint.Max(leftLength, rightLength));
            var left = leftLength - 1;
            var right = rightLength - 1;
            var buffer = leftLength + rightLength + bufferLength - 1;
            var span = new NativeSpan<T>(ref head, bufferLength + leftLength + rightLength);
            var threshold = rightOrigin == Subarray.Right ? 1 : 0;
            while (left < leftLength && right < rightLength)
            {
                var ro = right + leftLength;
                ref var rB = ref Unsafe.Add(ref head, buffer);
                ref var rL = ref Unsafe.Add(ref head, left);
                ref var rR = ref Unsafe.Add(ref head, ro);
                if (TProxy.Compare(in rL, in rR) >= threshold)
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
            _ = span;
            if (left >= leftLength)  // Loop broken with right < rightLength satisfied, hence bonus values should come from right
            {
                var rightRem = right - leftLength + 1;  // rightRem is 1 when right == leftLength
                InsertBufferForwardsUnordered(ref head, leftLength, rightRem);
                return (rightOrigin, rightRem);
            }
            else
            {
                return (SwitchSubarray(rightOrigin), left + 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static (Subarray newOrigin, nuint currentBlockLength) LocalMergeLazyLargeStruct<T, TProxy>(ref T head, nuint leftLength, nuint rightLength, Subarray leftOrigin)
            where TProxy : IStaticComparisonProxy<T>
        {
            var middle = leftLength;
            ArgumentOutOfRangeException.ThrowIfZero(leftLength);
            var span = NativeMemoryUtils.CreateNativeSpan(ref head, leftLength + rightLength);
            var start = (nuint)0;
            var leftLen = leftLength;
            var rightLen = rightLength;
            var threshold = leftOrigin == Subarray.Right ? 0 : 1;
            if (leftLen > 0 && TProxy.Compare(in Unsafe.Add(ref head, middle - 1), in Unsafe.Add(ref head, middle)) >= threshold)
            {
                do
                {
                    ref var s = ref Unsafe.Add(ref head, start);
                    ref var rm = ref Unsafe.Add(ref head, middle);
                    var mergeLen = FindFirstElementMergingStatic<T, TProxy>(ref rm, rightLen, leftOrigin, s);
                    if (mergeLen - 1 < rightLen)
                    {
                        Rotate(ref s, leftLen, mergeLen);
                        start += mergeLen;
                        middle += mergeLen;
                        rightLen -= mergeLen;
                        rm = ref Unsafe.Add(ref head, middle)!;
                    }
                    if (rightLen - 1 >= rightLength)
                    {
                        // rightLen is either 0 or > rightLength
                        // `condition` outside the loop is false after reaching this path.
                        break;
                    }
                    do
                    {
                        start++;
                        leftLen--;
                    } while (leftLen > 0 && TProxy.Compare(in Unsafe.Add(ref head, start), in rm) < threshold);
                } while (leftLen > 0);
            }
            var condition = rightLen - 1 < rightLength;
            leftOrigin = SwitchSubarrayIf(leftOrigin, condition);
            rightLen = condition ? rightLen : leftLen;
            _ = span;
            return (leftOrigin, rightLen);
        }

        internal static (Subarray newOrigin, nuint currentBlockLength) MergeBlocksForwardsLargeStruct<T, TProxy, TIsLast>(ref readonly T keys, in T medianKey, NativeSpan<T> values, nuint blockLength, nuint lastMergeBlocks, nuint lastLength)
            where TProxy : IStaticComparisonProxy<T>
            where TIsLast : unmanaged, IGenericBoolParameter<TIsLast>
        {
            ref readonly var keyHead = ref keys;
            var nextBlock = blockLength * 2;
            var currentBlockLength = blockLength;
            var currentBlockOrigin = GetSubarray<T, TProxy>(in keys, medianKey);
            var ol = values.Length - blockLength + 1;
            nuint blockIndex = 1;
            ArgumentOutOfRangeException.ThrowIfGreaterThan(ol, values.Length);

            for (; nextBlock < ol; blockIndex++, nextBlock += blockLength)
            {
                var nextBlockOrigin = GetSubarray<T, TProxy>(in NativeMemoryUtils.Add(in keyHead, blockIndex), medianKey);
                var buffer = nextBlock - blockLength - currentBlockLength;
                Debug.Assert(nextBlock + blockLength <= values.Length);
                Debug.Assert(buffer < values.Length);
                if (nextBlockOrigin != currentBlockOrigin)
                {
                    (currentBlockOrigin, currentBlockLength) = LocalMergeForwardsLargeStruct<T, TProxy>(ref values.ElementAtUnchecked(buffer), blockLength, currentBlockLength, currentBlockOrigin, blockLength);
                }
                else
                {
                    InsertBufferForwardsUnordered(ref values.ElementAtUnchecked(buffer), blockLength, currentBlockLength);
                    currentBlockLength = blockLength;
                }
            }
            if (TIsLast.Value)
            {
                if (lastLength > 0)
                {
                    if (currentBlockOrigin == Subarray.Right)
                    {
                        InsertBufferForwardsUnordered(ref values.ElementAtUnchecked(nextBlock), blockLength, currentBlockLength);
                        nextBlock += currentBlockLength;
                        currentBlockLength = 0;
                    }
                    currentBlockLength += blockLength * lastMergeBlocks;
                    currentBlockOrigin = Subarray.Left;
                    MergeForwardsLargeStruct<T, TProxy>(ref values.ElementAtUnchecked(nextBlock), blockLength, currentBlockLength, lastLength);
                }
            }
            else
            {
                InsertBufferForwardsUnordered(ref values.ElementAtUnchecked(nextBlock), blockLength, currentBlockLength);
            }
            return (currentBlockOrigin, currentBlockLength);
        }

        internal static (Subarray newOrigin, nuint currentBlockLength) MergeBlocksBackwardsLargeStruct<T, TProxy>(ReadOnlyNativeSpan<T> keys, in T medianKey, NativeSpan<T> values, nuint blockLength, nuint lastLength)
            where TProxy : IStaticComparisonProxy<T>
        {
            var head = values.Length - blockLength * 2 - lastLength;
            var currentBlockLength = lastLength;
            var currentBlockOrigin = Subarray.Right;
            var ol = values.Length - blockLength + 1;
            var blockIndex = keys.Length - 1;
            Debug.Assert(ol <= values.Length);

            for (; head < values.Length && blockIndex < keys.Length; blockIndex--, head -= blockLength)
            {
                var nextBlockOrigin = GetSubarray<T, TProxy>(in keys.ElementAtUnchecked(blockIndex), medianKey);
                if (nextBlockOrigin != currentBlockOrigin)
                {
                    (currentBlockOrigin, currentBlockLength) = LocalMergeBackwardsLargeStruct<T, TProxy>(ref values.ElementAtUnchecked(head), blockLength, currentBlockLength, blockLength, currentBlockOrigin);
                }
                else
                {
                    InsertBufferBackwardsUnordered(ref values.ElementAtUnchecked(head + blockLength), currentBlockLength, blockLength);
                    currentBlockLength = blockLength;
                }
            }
            InsertBufferBackwardsUnordered(ref values.ElementAtUnchecked(head + blockLength), currentBlockLength, blockLength);
            return (currentBlockOrigin, currentBlockLength);
        }

    }
}
