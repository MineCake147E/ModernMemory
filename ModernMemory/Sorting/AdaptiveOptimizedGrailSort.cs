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

using static System.Reflection.Metadata.BlobBuilder;

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

    public readonly partial struct AdaptiveOptimizedGrailSort
    {
        public enum Subarray : byte
        {
            Left = 0,
            Right = 1
        }

        public enum MergeDirection : byte
        {
            Forward = 0,
            Backward = 1
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
        internal static Subarray GetSubarray<T, TProxy>(T? currentKey, T? medianKey, TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
            => TProxy.Compare(currentKey, medianKey, proxy) < 0 ? Subarray.Left : Subarray.Right;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static nuint CalculateBlockSize(nuint length, out nuint blocks) => (nuint)1 << CalculateBlockSizeExponent(length, out blocks);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int CalculateBlockSizeExponent(nuint length, out nuint blocks)
        {
            var k = length - 1;
            if (k > length) k = length;
            var m = (BitOperations.LeadingZeroCount((nuint)0) - BitOperations.LeadingZeroCount(k) + 1) >>> 1;
            blocks = ((length - 1) >> m) + 1;
            return m;
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
        internal static nuint FindFirstElementGreaterThanOrEqualTo<T, TProxy>(ref readonly T? head, nuint length, T? value, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            if (length > 0)
            {
                var cmp = TProxy.Load(in proxy);
                nuint start = 0;
                var len = length;
                while (len > 0)
                {
                    var k = len;
                    var m = start;
                    len >>= 1;
                    k &= 1;
                    k += len;
                    start += len;
                    Debug.Assert(start < length);
                    var c = TProxy.Compare(NativeMemoryUtils.Add(in head, start), value, cmp);
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
        internal static nuint FindFirstElementGreaterThanStatic<T, TProxy>(ref readonly T? head, nuint length, T? value, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            if (length > 0)
            {
                nuint start = 0;
                var len = length;
                var cmp = TProxy.Load(in proxy);
                while (len > 0)
                {
                    var k = len;
                    var m = start;
                    len >>= 1;
                    k &= 1;
                    start += len;
                    k += len;
                    Debug.Assert(start < length);
                    var c = TProxy.Compare(value, NativeMemoryUtils.Add(in head, start), cmp);
                    start = (nuint)(c >> ~0);
                    k &= ~start;
                    start = m + k;
                }
                return start;
            }
            return 0;
        }

        [SkipLocalsInit]
        internal static nuint FindFirstElementMergingStatic<T, TProxy>(ref readonly T? head, nuint length, Subarray subarray, T? value, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            if (length > 0)
            {
                var cmp = TProxy.Load(in proxy);
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
                    var c = TProxy.Compare(v, NativeMemoryUtils.Add(in head, start), cmp) - threshold;
                    start = (nuint)(c >> ~0);
                    k &= ~start;
                    start = m + k;
                }
                return start;
            }
            return length;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static nuint CollectKeys<T, TProxy>(NativeSpan<T?> values, nuint idealKeyCount, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            if (values.Length <= 1) return values.Length;
            nuint keysFound = 1;
            nuint firstKey = 0;
            nuint current = 0;
            ref var head = ref values[0];
            var length = values.Length;
            var cmp = TProxy.Load(in proxy);
            while (++current < length && keysFound < idealKeyCount)
            {
                var insert = NativeMemoryExtensions.BinarySearch(in Unsafe.Add(ref head, firstKey), keysFound, Unsafe.Add(ref head, current), out var exactMatch, cmp);
                if (!exactMatch)
                {
                    var kf = keysFound++;
                    var rightLength = current - (firstKey + kf);
                    // Rotate the new key as well if key should be inserted at position 0.
                    rightLength += insert == 0 ? (nuint)1 : 0;
                    NativeMemoryUtils.Rotate(ref Unsafe.Add(ref head, firstKey), kf, rightLength);
                    firstKey = current - kf;
                    if (kf == insert || insert == 0) // Same optimization as holy grail sort
                    {
                        continue;
                    }
                    Debug.Assert(firstKey + kf <= length);
                    NativeMemoryUtils.InsertBackwards(ref Unsafe.Add(ref head, firstKey + insert), kf - insert);
                }
            }
            NativeMemoryUtils.Rotate(ref head, firstKey, keysFound);
            return keysFound;
        }

        internal static void SortKeys<T, TProxy>(NativeSpan<T?> keys, NativeSpan<T?> buffer, T? medianKey, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            if (!buffer.IsEmpty)
            {
                var cmp = TProxy.Load(in proxy);
                var median = medianKey;
                var keySpan = keys;
                var bufferSpan = buffer;
                nuint i = 0;
                nuint bufferSwaps = 0;
                var length = keySpan.Length;
                var bufferLength = bufferSpan.Length;
                ref var firstSwapPos = ref bufferSpan[0];
                for (; i < length; i++)
                {
                    ref var pos = ref keySpan.ElementAtUnchecked(i);
                    if (TProxy.Compare(pos, median, cmp) >= 0)
                    {
                        (pos, firstSwapPos) = (firstSwapPos, pos);
                        bufferSwaps++;
                        i++;
                        break;
                    }
                }
                for (; i < length && bufferSwaps < bufferLength; i++)
                {
                    ref var pos = ref keySpan.ElementAtUnchecked(i);
                    var value = pos;
                    // bufferSwaps never grows faster than i
                    ref var swapPos = ref keySpan.ElementAtUnchecked(i - bufferSwaps);
                    ref var bufferSwapPos = ref bufferSpan.ElementAtUnchecked(bufferSwaps);
                    var v = TProxy.Compare(value, median, cmp) >= 0;
                    if (v)
                    {
                        swapPos = ref bufferSwapPos;
                    }
                    bufferSwaps += v ? (nuint)1 : 0;
                    (pos, swapPos) = (swapPos, value);
                }
                if (bufferSwaps > 0)
                {
                    // bufferSwaps never grows faster than i
                    NativeMemoryUtils.SwapValues(ref keySpan[i - bufferSwaps], ref bufferSpan[0], bufferSwaps);
                }
                if (i == length)
                {
                    return;
                }
            }
            // insufficient buffer or malfunctional comparison proxy
            SortKeysWithoutBuffer(keys, medianKey, in proxy);
        }

        internal static void SortKeysWithoutBuffer<T, TProxy>(NativeSpan<T?> keys, T? medianKey, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            // TODO: replace with more advanced algorithm
            ShellSort.Sort(keys, in proxy);
            _ = medianKey;
        }

        internal static void SortPairsWithKeys<T, TProxy>(NativeSpan<T?> values, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var v0 = values[0];
            var v1 = values[1];
            SortPairs(values, in proxy);
            values[values.Length - 2] = v0;
            values[values.Length - 1] = v1;
        }

        internal static void SortPairs<T, TProxy>(NativeSpan<T?> values, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            nuint i = 2;
            ref var head = ref values[0];
            var length = values.Length;
            var ol = length - 1;
            if (ol < length)
            {
                var cmp = TProxy.Load(in proxy);
                for (; i < ol; i += 2)
                {
                    var left = i;
                    var right = i + 1;
                    Debug.Assert(right < length);
                    ref var lh = ref Unsafe.Add(ref head, left);
                    ref var rh = ref Unsafe.Add(ref head, right);
                    Debug.Assert(left - 2 < length);
                    if (TProxy.Compare(lh, rh, cmp) > 0)
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

        internal static void BuildInPlace<T, TProxy>(NativeSpan<T?> values, nuint currentBufferOffset, int currentLengthExponent, int bufferLengthExponent, in TProxy proxy)
            where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var currentLength = (nuint)1 << currentLengthExponent;
            var length = values.Length;
            var bufferLength = (nuint)1 << bufferLengthExponent;
            var remainingHeadBuffer = currentBufferOffset;
            var tailBuffer = bufferLength - remainingHeadBuffer;
            length -= tailBuffer;
            ref var head = ref values[0];
            nuint i;
            nuint sizeAfterMerge;
            ref readonly var p = ref proxy;
            while (remainingHeadBuffer >= currentLength)
            {
                sizeAfterMerge = currentLength + currentLength;
                i = remainingHeadBuffer - currentLength;
                var mergeEnd = length - sizeAfterMerge - currentLength + 1;
                if (mergeEnd < length)
                {
                    for (; i < mergeEnd; i += sizeAfterMerge)
                    {
                        MergeForwardsLargeStruct(ref Unsafe.Add(ref head, i), currentLength, currentLength, currentLength, in p);
                    }
                }
                var leftOver = length - i - currentLength;
                if (leftOver > currentLength)
                {
                    MergeForwardsLargeStruct(ref Unsafe.Add(ref head, i), currentLength, currentLength, leftOver - currentLength, in p);
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
                MergeBackwardsLargeStruct(ref Unsafe.Add(ref head, i), tailBuffer, lastBlock - tailBuffer, tailBuffer, in p);
            }
            i -= sizeAfterMerge;
            while (i <= length)
            {
                MergeBackwardsLargeStruct(ref Unsafe.Add(ref head, i), tailBuffer, tailBuffer, tailBuffer, in p);
                i -= sizeAfterMerge;
            }
        }

        internal static void BuildBlocks<T, TProxy>(NativeSpan<T?> values, int bufferLengthExponent, in TProxy proxy)
            where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            SortPairsWithKeys(values.Slice(((nuint)1 << bufferLengthExponent) - 2), in proxy);
            BuildInPlace(values, ((nuint)1 << bufferLengthExponent) - 2, 1, bufferLengthExponent, in proxy);
        }

        internal static nuint SortBlocks<T, TProxy, TIsBlockValueAtTail>(ref T? keyHead, NativeSpan<T?> values, int blockSizeExponent, in TProxy proxy)
            where TProxy : struct, ILightweightComparer<T, TProxy>
            where TIsBlockValueAtTail : unmanaged, IGenericBoolParameter<TIsBlockValueAtTail>
        {
            var bse = blockSizeExponent;
            var length = values.Length;
            var blocks = length >> bse;
            if (blocks < 2) return blocks;
            ArgumentOutOfRangeException.ThrowIfZero(blocks);
            var leftBlockCount = (nuint)1 << ~BitOperations.LeadingZeroCount(blocks - 1);
            var keys = new NativeSpan<T?>(ref keyHead, blocks);
            nuint blockIndex = 0;
            nuint keyIndex = 0;
            ref var head = ref values[0];
            var sorted = true;
            var blockSize = (nuint)1 << bse;
            var blockValuePos = TIsBlockValueAtTail.Value ? blockSize - 1 : 0;
            var rightBlockIndex = leftBlockCount << bse;
            var rightKeyIndex = leftBlockCount;
            var rightHeadBlockValue = Unsafe.Add(ref head, rightBlockIndex + blockValuePos);
            var cmp = TProxy.Load(in proxy);
            do
            {
                Debug.Assert(blockIndex + blockValuePos < length);
                if (TProxy.Compare(rightHeadBlockValue, Unsafe.Add(ref head, blockIndex + blockValuePos), cmp) < 0)
                {
                    NativeMemoryUtils.SwapSingle(ref Unsafe.Add(ref keyHead, keyIndex), ref Unsafe.Add(ref keyHead, rightKeyIndex));
                    NativeMemoryUtils.SwapValues(ref Unsafe.Add(ref head, blockIndex), ref Unsafe.Add(ref head, rightBlockIndex), blockSize);
                    sorted = false;
                }
                keyIndex++;
                blockIndex = keyIndex << bse;
            } while (sorted && keyIndex < rightKeyIndex);
            if (!sorted)
            {
                SortBlocksPhase2(ref keyHead, ref head, in TProxy.PassReference(in cmp), bse, blocks, blockIndex, keyIndex, rightKeyIndex);
            }
            _ = keys;
            return blocks;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static nuint FindBlock(ref T? keyHead, ref T? head, TProxy proxy, nuint keyLength, int blockSizeExponent)
            {
                var cmp = TProxy.Load(in proxy);
                var bse = blockSizeExponent;
                var blockSize = (nuint)1 << bse;
                nuint selectedKey = 0;
                var currentKey = selectedKey + 1;
                var currentBlock = blockSize;
                var keyTail = keyLength;
                var blockValuePos = TIsBlockValueAtTail.Value ? blockSize - 1 : 0;
                ref var offsetHead = ref Unsafe.Add(ref head, blockValuePos);
                var selectedBlockValue = offsetHead;
                var selectedKeyValue = Unsafe.Add(ref keyHead, selectedKey);
                for (; currentKey < keyTail; currentKey++, currentBlock += blockSize)
                {
                    var currentBlockValue = Unsafe.Add(ref offsetHead, currentBlock);
                    var c = TProxy.Compare(currentBlockValue, selectedBlockValue, cmp);
                    if (c > 0) continue;
                    var currentKeyValue = Unsafe.Add(ref keyHead, currentKey);
                    if (c == 0 && TProxy.Compare(currentKeyValue, selectedKeyValue, cmp) >= 0) continue;
                    selectedKeyValue = currentKeyValue;
                    selectedBlockValue = currentBlockValue;
                    selectedKey = currentKey;
                }
                return selectedKey;
            }

            static void SortBlocksPhase2(ref T? keyHead, ref T? head, in TProxy proxy, int bse, nuint blocks, nuint initialBlockIndex, nuint initialKeyIndex, nuint leftBlockCount)
            {
                var lastKey = blocks - 1;
                var rightKeyIndex = leftBlockCount;
                var scrambledEnd = rightKeyIndex + 1;
                var blockSize = (nuint)1 << bse;
                if (rightKeyIndex >= lastKey) scrambledEnd = rightKeyIndex;
                var cmp = TProxy.Load(in proxy);
                while (initialKeyIndex < rightKeyIndex)
                {
                    Debug.Assert(scrambledEnd + 1 <= blocks);
                    Debug.Assert(rightKeyIndex <= scrambledEnd + 1);
                    var selectedKey = FindBlock(ref Unsafe.Add(ref keyHead, rightKeyIndex), ref Unsafe.Add(ref head, rightKeyIndex << bse), TProxy.Pass(cmp), scrambledEnd + 1 - rightKeyIndex, bse) + rightKeyIndex;
                    var selectedBlock = selectedKey << bse;
                    if (initialKeyIndex != selectedKey)
                    {
                        NativeMemoryUtils.SwapSingle(ref Unsafe.Add(ref keyHead, initialKeyIndex), ref Unsafe.Add(ref keyHead, selectedKey));
                        NativeMemoryUtils.SwapValues(ref Unsafe.Add(ref head, initialBlockIndex), ref Unsafe.Add(ref head, selectedBlock), blockSize);
                    }
                    if (selectedKey == scrambledEnd && scrambledEnd < lastKey)
                    {
                        scrambledEnd++;
                    }
                    initialKeyIndex++;
                    initialBlockIndex = initialKeyIndex << bse;
                }
                Debug.Assert(scrambledEnd + 1 <= blocks);
                SortBlocksPhase3(ref keyHead, ref head, in proxy, bse, initialKeyIndex, lastKey, scrambledEnd);
            }

            static void SortBlocksPhase3(ref T? keyHead, ref T? head, in TProxy proxy, int bse, nuint initialKeyIndex, nuint lastKey, nuint initialScrambledEnd)
            {
                var blockSize = (nuint)1 << bse;
                var keyIndex = initialKeyIndex;
                var blockIndex = keyIndex << bse;
                var scrambledEnd = initialScrambledEnd;
                var tailKey = lastKey;
                var cmp = TProxy.Load(in proxy);
                while (scrambledEnd < tailKey && keyIndex < scrambledEnd)
                {
                    Debug.Assert(keyIndex <= scrambledEnd + 1);
                    var selectedKey = FindBlock(ref Unsafe.Add(ref keyHead, keyIndex), ref Unsafe.Add(ref head, blockIndex), TProxy.Pass(cmp), scrambledEnd + 1 - keyIndex, bse) + keyIndex;
                    var selectedBlock = selectedKey << bse;
                    if (keyIndex != selectedKey)
                    {
                        NativeMemoryUtils.SwapSingle(ref Unsafe.Add(ref keyHead, keyIndex), ref Unsafe.Add(ref keyHead, selectedKey));
                        NativeMemoryUtils.SwapValues(ref Unsafe.Add(ref head, blockIndex), ref Unsafe.Add(ref head, selectedBlock), blockSize);
                        if (selectedKey == scrambledEnd)
                        {
                            scrambledEnd++;
                        }
                    }
                    keyIndex++;
                    blockIndex = keyIndex << bse;
                }
                if (keyIndex < scrambledEnd)
                {
                    SortBlocksPhase4(ref keyHead, ref head, in cmp, bse, keyIndex, lastKey);
                }
            }

            static void SortBlocksPhase4(ref T? keyHead, ref T? head, in TProxy proxy, int bse, nuint initialKeyIndex, nuint lastKey)
            {
                var blockSize = (nuint)1 << bse;
                var keyIndex = initialKeyIndex;
                var blockIndex = keyIndex << bse;
                var tailKey = lastKey;
                var cmp = TProxy.Load(in proxy);
                do
                {
                    var selectedKey = FindBlock(ref Unsafe.Add(ref keyHead, keyIndex), ref Unsafe.Add(ref head, blockIndex), TProxy.Pass(cmp), tailKey + 1 - keyIndex, bse) + keyIndex;
                    var selectedBlock = selectedKey << bse;
                    if (keyIndex != selectedKey)
                    {
                        NativeMemoryUtils.SwapSingle(ref Unsafe.Add(ref keyHead, keyIndex), ref Unsafe.Add(ref keyHead, selectedKey));
                        NativeMemoryUtils.SwapValues(ref Unsafe.Add(ref head, blockIndex), ref Unsafe.Add(ref head, selectedBlock), blockSize);
                    }
                    keyIndex++;
                    blockIndex = keyIndex << bse;
                } while (keyIndex < tailKey);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static nuint GetBlockCount(nuint length, int blockSizeExponent)
        {
            var m = (nuint)1 << blockSizeExponent;
            m--;
            length += m;
            return length >> blockSizeExponent;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void MergeForwardsLargeStruct<T, TProxy>(ref T? bufferHead, nuint bufferLength, nuint leftLength, nuint rightLength, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferLength, nuint.Min(leftLength, rightLength));
            ref var head = ref bufferHead;
            nuint buffer = 0;
            var left = bufferLength;
            var leftEnd = left + leftLength;
            var right = leftEnd;
            var end = leftEnd + rightLength;
            var span = new NativeSpan<T?>(ref bufferHead, bufferLength + leftLength + rightLength);
            var cmp = TProxy.Load(in proxy);
            while (right < end && left < leftEnd)
            {
                ref var rB = ref Unsafe.Add(ref bufferHead, buffer);
                ref var rL = ref Unsafe.Add(ref head, left);
                ref var rR = ref Unsafe.Add(ref head, right);
                ref var rS = ref rL;
                var c = TProxy.Compare(rL, rR, cmp) > 0;
                if (c)
                {
                    rS = ref rR;
                }
                var v = c ? (nuint)1 : 0;
                right += v;
                left += v ^ 1;
                buffer++;
                (rB, rS) = (rS, rB);
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void MergeForwardsLazyLargeStruct<T, TProxy>(ref T? head, nuint leftLength, nuint rightLength, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var totalLength = leftLength + rightLength;
            var span = new ReadOnlyNativeSpan<T?>(in head, totalLength);
            nuint start = 0;
            var middle = leftLength;
            var cmp = TProxy.Load(in proxy);
            ref readonly var p = ref cmp;
            while (middle < totalLength && start < middle)
            {
                var rightLen = totalLength - middle;
                var mergeSize = FindFirstElementGreaterThanOrEqualTo(in Unsafe.Add(ref head, middle), rightLen, Unsafe.Add(ref head, start), in p);
                if (mergeSize - 1 < rightLen)
                {
                    var leftLen = middle - start;
                    NativeMemoryUtils.Rotate(ref Unsafe.Add(ref head, start), leftLen, mergeSize);
                    start += mergeSize;
                    middle += mergeSize;
                }
                if (middle >= totalLength) break;
                var middleValue = Unsafe.Add(ref head, middle);
                do
                {
                    start++;
                } while (start < middle && TProxy.Compare(Unsafe.Add(ref head, start), middleValue, cmp) <= 0);
            }
            _ = span;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void MergeBackwardsLargeStruct<T, TProxy>(ref T? head, nuint leftLength, nuint rightLength, nuint bufferLength, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferLength, nuint.Max(leftLength, rightLength));
            var left = leftLength - 1;
            var right = rightLength - 1;
            var buffer = leftLength + rightLength + bufferLength - 1;
            var span = new NativeSpan<T?>(ref head, bufferLength + leftLength + rightLength);
            var cmp = TProxy.Load(in proxy);
            while (left < leftLength && right < rightLength)
            {
                var ro = right + leftLength;
                ref var rB = ref Unsafe.Add(ref head, buffer);
                ref var rL = ref Unsafe.Add(ref head, left);
                ref var rR = ref Unsafe.Add(ref head, ro);
                ref var rS = ref rR;
                var c = TProxy.Compare(rL, rR, cmp) > 0;
                if (c)
                {
                    rS = ref rL;
                }
                var v = c ? (nuint)1 : 0;
                left -= v;
                right += v - 1;
                buffer--;
                (rB, rS) = (rS, rB);
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void MergeBackwardsLazyLargeStruct<T, TProxy>(ref T? head, nuint leftLength, nuint rightLength, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var totalLength = leftLength + rightLength;
            var span = new ReadOnlyNativeSpan<T?>(in head, totalLength);
            var tail = totalLength - 1;
            var leftLen = leftLength;
            var cmp = TProxy.Load(in proxy);
            ref readonly var p = ref cmp;
            while (tail - leftLen < rightLength && leftLen - 1 < leftLength)
            {
                var mergePos = FindFirstElementGreaterThanStatic(in head, leftLen, Unsafe.Add(ref head, tail), in p);
                if (mergePos < leftLen)
                {
                    var mergeSize = leftLen - mergePos;
                    NativeMemoryUtils.Rotate(ref Unsafe.Add(ref head, mergePos), mergeSize, tail - leftLen + 1);
                    tail -= mergeSize;
                    leftLen = mergePos;
                }
                var middle = leftLen - 1;
                if (middle >= leftLength)
                {
                    break;
                }
                var middleValue = Unsafe.Add(ref head, middle);
                do
                {
                    tail--;
                } while (tail < totalLength && TProxy.Compare(middleValue, Unsafe.Add(ref head, tail), cmp) <= 0);
            }
            _ = span;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void MergeBufferBackwardsLazyLargeStruct<T, TProxy>(ref T? head, nuint leftLength, nuint rightLength, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var totalLength = leftLength + rightLength;
            var span = new ReadOnlyNativeSpan<T?>(in head, totalLength);
            var tail = totalLength - 1;
            var leftLen = leftLength;
            var cmp = TProxy.Load(in proxy);
            ref readonly var p = ref cmp;
            while (tail - leftLen < rightLength && leftLen - 1 < leftLength)
            {
                var mergePos = FindFirstElementGreaterThanOrEqualTo(in head, leftLen, Unsafe.Add(ref head, tail), in p);
                if (mergePos < leftLen)
                {
                    var mergeSize = leftLen - mergePos;
                    NativeMemoryUtils.Rotate(ref Unsafe.Add(ref head, mergePos), mergeSize, tail - leftLen + 1);
                    tail -= mergeSize;
                    leftLen = mergePos;
                }
                var middle = leftLen - 1;
                if (middle >= leftLength)
                {
                    break;
                }
                var middleValue = Unsafe.Add(ref head, middle);
                do
                {
                    tail--;
                } while (tail < totalLength && TProxy.Compare(middleValue, Unsafe.Add(ref head, tail), cmp) < 0);
            }
            _ = span;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static (Subarray newOrigin, nuint currentBlockLength) LocalMergeForwardsLargeStruct<T, TProxy>(ref T? bufferHead, nuint bufferLength, nuint leftLength, Subarray leftOrigin, nuint rightLength, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            ref var head = ref bufferHead;
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferLength, nuint.Max(leftLength, rightLength));
            nuint buffer = 0;
            var left = bufferLength;
            var leftEnd = left + leftLength;
            var right = leftEnd;
            var end = leftEnd + rightLength;
            var span = new NativeSpan<T?>(ref bufferHead, bufferLength + leftLength + rightLength);
            var threshold = leftOrigin == Subarray.Left ? 1 : 0;
            var cmp = TProxy.Load(in proxy);
            while (right < end && left < leftEnd)
            {
                ref var rB = ref Unsafe.Add(ref bufferHead, buffer);
                ref var rL = ref Unsafe.Add(ref head, left);
                ref var rR = ref Unsafe.Add(ref head, right);
                ref var rS = ref rR;
                var c = TProxy.Compare(rL, rR, cmp) < threshold;
                if (c)
                {
                    rS = ref rL;
                }
                var v = c ? (nuint)1 : 0;
                left += v;
                right += v ^ 1;
                buffer++;
                (rB, rS) = (rS, rB);
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static (Subarray newOrigin, nuint currentBlockLength) LocalMergeBackwardsLargeStruct<T, TProxy>(ref T? head, nuint leftLength, nuint rightLength, nuint bufferLength, Subarray rightOrigin, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            Debug.Assert(leftLength > 0);
            ArgumentOutOfRangeException.ThrowIfLessThan(bufferLength, nuint.Max(leftLength, rightLength));
            var threshold = rightOrigin == Subarray.Right ? 1 : 0;
            var left = leftLength - 1;
            var right = rightLength - 1;
            var totalLength = leftLength + rightLength + bufferLength;
            var buffer = totalLength - 1;
            var span = new NativeSpan<T?>(ref head, totalLength);
            var cmp = TProxy.Load(in proxy);
            while (left < leftLength && right < rightLength && buffer < totalLength)
            {
                var ro = right + leftLength;
                ref var rB = ref Unsafe.Add(ref head, buffer);
                ref var rL = ref Unsafe.Add(ref head, left);
                ref var rR = ref Unsafe.Add(ref head, ro);
                ref var rS = ref rL;
                var c = TProxy.Compare(rL, rR, cmp) >= threshold;
                if (!c)
                {
                    rS = ref rR;
                }
                var v = c ? (nuint)1 : 0;
                left -= v;      // left--; if c
                right += v - 1; // right--; if !c
                buffer--;
                (rB, rS) = (rS, rB);
            }
            right += leftLength;
            _ = span;
            if (left >= leftLength)  // Loop broken with right < rightLength satisfied, hence bonus span should come from right
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

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static (Subarray newOrigin, nuint currentBlockLength) LocalMergeLazyLargeStruct<T, TProxy>(ref T? head, nuint leftLength, nuint rightLength, Subarray leftOrigin, in TProxy proxy)
            where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var threshold = leftOrigin == Subarray.Right ? 0 : 1;
            var middle = leftLength;
            ArgumentOutOfRangeException.ThrowIfZero(leftLength);
            var span = NativeMemoryUtils.CreateNativeSpan(ref head, leftLength + rightLength);
            var start = (nuint)0;
            var leftLen = leftLength;
            var rightLen = rightLength;
            var cmp = TProxy.Load(in proxy);
            ref readonly var p = ref cmp;
            if (leftLen > 0 && TProxy.Compare(Unsafe.Add(ref head, middle - 1), Unsafe.Add(ref head, middle), cmp) >= threshold)
            {
                do
                {
                    ref var s = ref Unsafe.Add(ref head, start);
                    ref var rm = ref Unsafe.Add(ref head, middle);
                    var mergeLen = FindFirstElementMergingStatic(ref rm, rightLen, leftOrigin, s, in p);
                    if (mergeLen - 1 < rightLen)
                    {
                        NativeMemoryUtils.Rotate(ref s, leftLen, mergeLen);
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
                    } while (leftLen > 0 && TProxy.Compare(Unsafe.Add(ref head, start), rm, cmp) < threshold);
                } while (leftLen > 0);
            }
            var condition = rightLen - 1 < rightLength;
            leftOrigin = SwitchSubarrayIf(leftOrigin, condition);
            rightLen = condition ? rightLen : leftLen;
            _ = span;
            return (leftOrigin, rightLen);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static nuint CountLastMergeBlocks<T, TProxy>(NativeSpan<T?> values, int blockSizeExponent, in TProxy proxy)
            where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var blockSize = (nuint)1 << blockSizeExponent;
            nuint blocksToMerge = 0;
            var length = values.Length;
            var lastRightFragment = (length - 1) & (~blockSize + 1);
            var cmp = TProxy.Load(in proxy);
            if (lastRightFragment > 0 && lastRightFragment < length)
            {
                var previousLeftBlock = lastRightFragment - blockSize;
                while (previousLeftBlock < length && TProxy.Compare(values.ElementAtUnchecked(lastRightFragment), values.ElementAtUnchecked(previousLeftBlock), cmp) < 0)
                {
                    blocksToMerge++;
                    previousLeftBlock -= blockSize;
                }
            }
            return blocksToMerge;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static (Subarray newOrigin, nuint currentBlockLength) MergeBlocksForwardsLargeStruct<T, TProxy, TIsLast>(int blockSizeExponent, NativeSpan<T?> values, ref readonly T? keys, T? medianKey, nuint lastMergeBlocks, in TProxy proxy)
            where TProxy : struct, ILightweightComparer<T, TProxy>
            where TIsLast : unmanaged, IGenericBoolParameter<TIsLast>
        {
            ref readonly var keyHead = ref keys;
            var span = values;
            var blockSize = (nuint)1 << blockSizeExponent;
            var nextBlock = blockSize * 2;
            var currentBlockLength = blockSize;
            var median = medianKey;
            var cmp = TProxy.Load(in proxy);
            ref readonly var p = ref cmp;
            var currentBlockOrigin = GetSubarray(keys, median, cmp);
            var lastMergeSize = lastMergeBlocks << blockSizeExponent;
            if (!TIsLast.Value)
            {
                lastMergeSize = 0;
            }
            var length = span.Length;
            var ol = length - blockSize + 1 - lastMergeSize;
            nuint blockIndex = 1;
            nuint buffer;
            if (ol < length)
            {
                for (; nextBlock < ol; blockIndex++, nextBlock += blockSize)
                {
                    var nextBlockOrigin = GetSubarray(NativeMemoryUtils.Add(in keyHead, blockIndex), median, cmp);
                    buffer = nextBlock - blockSize - currentBlockLength;
                    Debug.Assert(nextBlock + blockSize <= length);
                    Debug.Assert(buffer < length);
                    if (nextBlockOrigin != currentBlockOrigin)
                    {
                        (currentBlockOrigin, currentBlockLength) = LocalMergeForwardsLargeStruct(ref span.ElementAtUnchecked(buffer), blockSize, currentBlockLength, currentBlockOrigin, blockSize, in p);
                    }
                    else
                    {
                        InsertBufferForwardsUnordered(ref span.ElementAtUnchecked(buffer), blockSize, currentBlockLength);
                        currentBlockLength = blockSize;
                    }
                }
            }
            buffer = nextBlock - blockSize - currentBlockLength;
            if (TIsLast.Value)
            {
                if (currentBlockOrigin == Subarray.Right)
                {
                    InsertBufferForwardsUnordered(ref span.ElementAtUnchecked(buffer), blockSize, currentBlockLength);
                    buffer += currentBlockLength;
                }
                currentBlockOrigin = Subarray.Left;
                var lastLength = length & (blockSize - 1);
                currentBlockLength = length - buffer - blockSize - lastLength;
                Debug.Assert(currentBlockLength <= length - buffer - blockSize);
                MergeForwardsLargeStruct(ref span.ElementAtUnchecked(buffer), blockSize, currentBlockLength, lastLength, in p);
            }
            else
            {
                InsertBufferForwardsUnordered(ref span.ElementAtUnchecked(buffer), blockSize, currentBlockLength);
            }
            return (currentBlockOrigin, currentBlockLength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static (Subarray newOrigin, nuint currentBlockLength) MergeBlocksBackwardsLargeStruct<T, TProxy>(int blockSizeExponent, NativeSpan<T?> values, ref readonly T? keyHead, T? medianKey, in TProxy proxy)
            where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var blockSize = (nuint)1 << blockSizeExponent;
            var span = values;
            var length = span.Length;
            var blocks = (length - blockSize) >> blockSizeExponent; // span contains currentBlock
            var keys = new ReadOnlyNativeSpan<T?>(in keyHead, blocks);
            var lastBlockLength = length & (blockSize - 1);
            var head = length - blockSize * 2 - lastBlockLength;
            var currentBlockLength = lastBlockLength;
            var currentBlockOrigin = Subarray.Right;
            var ol = length - blockSize + 1;
            var blockIndex = keys.Length - 1;
            var median = medianKey;
            Debug.Assert(ol <= length);
            var cmp = TProxy.Load(in proxy);
            ref readonly var p = ref cmp;
            for (; head < length && blockIndex < keys.Length; blockIndex--, head -= blockSize)
            {
                var nextBlockOrigin = GetSubarray(keys.ElementAtUnchecked(blockIndex), median, cmp);
                if (nextBlockOrigin != currentBlockOrigin)
                {
                    (currentBlockOrigin, currentBlockLength) = LocalMergeBackwardsLargeStruct(ref span.ElementAtUnchecked(head), blockSize, currentBlockLength, blockSize, currentBlockOrigin, in p);
                }
                else
                {
                    InsertBufferBackwardsUnordered(ref span.ElementAtUnchecked(head + blockSize), currentBlockLength, blockSize);
                    currentBlockLength = blockSize;
                }
            }
            if (head + blockSize < length)
            {
                InsertBufferBackwardsUnordered(ref span.ElementAtUnchecked(head + blockSize), currentBlockLength, blockSize);
            }
            return (currentBlockOrigin, currentBlockLength);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static (Subarray newOrigin, nuint currentBlockLength) MergeBlocksLazyLargeStruct<T, TProxy, TIsLast>(int blockSizeExponent, NativeSpan<T?> values, ref readonly T? keys, T? medianKey, nuint lastMergeBlocks, in TProxy proxy)
            where TProxy : struct, ILightweightComparer<T, TProxy>
            where TIsLast : unmanaged, IGenericBoolParameter<TIsLast>
        {
            ref readonly var keyHead = ref keys;
            var span = values;
            var blockSize = (nuint)1 << blockSizeExponent;
            var nextBlock = blockSize;
            var currentBlockLength = blockSize;
            var median = medianKey;
            var cmp = TProxy.Load(in proxy);
            ref readonly var p = ref cmp;
            var currentBlockOrigin = GetSubarray(keys, median, cmp);
            var lastMergeSize = lastMergeBlocks << blockSizeExponent;
            if (!TIsLast.Value)
            {
                lastMergeSize = 0;
            }
            var length = span.Length;
            var ol = length - blockSize + 1 - lastMergeSize;
            nuint blockIndex = 1;
            if (ol < length)
            {
                for (; nextBlock < ol; blockIndex++, nextBlock += blockSize)
                {
                    var nextBlockOrigin = GetSubarray(NativeMemoryUtils.Add(in keyHead, blockIndex), median, cmp);
                    var currentBlock = nextBlock - currentBlockLength;
                    Debug.Assert(nextBlock + blockSize <= length);
                    Debug.Assert(currentBlock < length);
                    if (nextBlockOrigin != currentBlockOrigin)
                    {
                        (currentBlockOrigin, currentBlockLength) = LocalMergeLazyLargeStruct(ref span.ElementAtUnchecked(currentBlock), currentBlockLength, blockSize, currentBlockOrigin, in p);
                    }
                    else
                    {
                        currentBlockLength = blockSize;
                    }
                }
            }
            if (TIsLast.Value)
            {
                var currentBlock = nextBlock - currentBlockLength;
                if (currentBlockOrigin == Subarray.Right)
                {
                    currentBlock += currentBlockLength;
                    currentBlockLength = 0;
                }
                var lastLength = length & (blockSize - 1);
                if (lastLength > 0)
                {
                    currentBlockLength = length - currentBlock - lastLength;
                    Debug.Assert(currentBlockLength <= length - currentBlock);
                    MergeBackwardsLazyLargeStruct(ref span.ElementAtUnchecked(currentBlock), currentBlockLength, lastLength, in p);
                }
            }
            return (currentBlockOrigin, currentBlockLength);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProxy"></typeparam>
        /// <param name="keyHead"></param>
        /// <param name="values"></param>
        /// <param name="subarrayLengthExponent"></param>
        /// <param name="blockSizeExponent"></param>
        /// <returns>The final position of buffer.</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static nuint CombineSubarraysForwards<T, TProxy>(ref T? keyHead, NativeSpan<T?> values, int subarrayLengthExponent, int blockSizeExponent, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var subarrayLength = (nuint)1 << subarrayLengthExponent;
            var blockSize = (nuint)1 << blockSizeExponent;
            var mergeLength = subarrayLength * 2;
            var span = values;
            var length = span.Length;
            length = checked(length - blockSize);
            var leftBlocksExponent = (int)checked(unchecked((uint)subarrayLengthExponent) - unchecked((uint)blockSizeExponent));
            var leftBlocks = (nuint)1 << leftBlocksExponent;
            var keys = new NativeSpan<T?>(ref keyHead, (nuint)1 << (leftBlocksExponent + 1));
            var medianKey = keys.ElementAtUnchecked(leftBlocks);
            nuint mergeBlock = 0;
            var ol = length - mergeLength + 1;
            ref readonly var p = ref TProxy.PassReference(in proxy);
            if (ol < length)
            {
                for (; mergeBlock < ol; mergeBlock += mergeLength)
                {
                    var blocks = span.Slice(mergeBlock + blockSize, mergeLength);
                    SortBlocks<T, TProxy, TypeFalse>(ref keyHead, blocks, blockSizeExponent, in p);
                    var mergeSpan = span.Slice(mergeBlock, mergeLength + blockSize);
                    MergeBlocksForwardsLargeStruct<T, TProxy, TypeFalse>(blockSizeExponent, mergeSpan, ref keyHead, medianKey, 0, in p);
                    SortKeys(keys, mergeSpan.Slice(mergeLength), medianKey, in p);
                }
            }
            var fullMerges = mergeBlock >> (subarrayLengthExponent + 1);
            if (mergeBlock <= length)
            {
                var buffer = mergeBlock;
                var finalBlocks = span.Slice(mergeBlock + blockSize);
                var mergeSpan = span.Slice(mergeBlock);

                if (finalBlocks.Length > subarrayLength)
                {
                    var cBlocks = SortBlocks<T, TProxy, TypeFalse>(ref keyHead, finalBlocks, blockSizeExponent, in p);
                    var lastMergeBlocks = CountLastMergeBlocks(finalBlocks, blockSizeExponent, in p);
                    MergeBlocksForwardsLargeStruct<T, TProxy, TypeTrue>(blockSizeExponent, mergeSpan, ref keyHead, medianKey, lastMergeBlocks, in p);
                    SortKeys(new(ref keyHead, cBlocks), mergeSpan.Slice(finalBlocks.Length), medianKey, in p);
                    mergeBlock += finalBlocks.Length;
                    if (fullMerges - 1 < (fullMerges ^ 1))  // fullMerges is neither zero nor odd
                    {
                        mergeBlock = buffer;
                        InsertBufferBackwardsUnordered(ref mergeSpan.Head, finalBlocks.Length, blockSize);
                    }
                }
                else
                {
                    if ((fullMerges & 1) > 0)
                    {
                        if (!finalBlocks.IsEmpty)
                        {
                            // buffer should be at very end of array for next CombineSubarraysBackwards
                            mergeBlock += finalBlocks.Length;
                            InsertBufferForwardsUnordered(ref mergeSpan.Head, blockSize, finalBlocks.Length);
                        }
                        else if (buffer > mergeLength)
                        {
                            Debug.Assert(mergeLength > 0);
                            mergeBlock = buffer - mergeLength;
                            mergeSpan = span.Slice(mergeBlock);
                            InsertBufferBackwardsUnordered(ref mergeSpan.Head, mergeLength, blockSize);
                        }
                    }
                }
            }
            _ = keys;
            return mergeBlock;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CombineSubarraysBackwards<T, TProxy>(ref T? keyHead, NativeSpan<T?> values, int subarrayLengthExponent, int blockSizeExponent, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var subarrayLength = (nuint)1 << subarrayLengthExponent;
            var blockSize = (nuint)1 << blockSizeExponent;
            var mergeLength = subarrayLength * 2;
            var span = values;
            var length = span.Length;
            length = checked(length - blockSize);
            var leftBlocks = subarrayLength >> blockSizeExponent;
            var medianKey = Unsafe.Add(ref keyHead, leftBlocks);
            var keys = new NativeSpan<T?>(ref keyHead, mergeLength >> blockSizeExponent);
            var mergeBlock = length & (~mergeLength + 1);
            ref readonly var p = ref TProxy.PassReference(in proxy);
            if (mergeBlock < length)    // tail undersized merge
            {
                var mergeSpan = span.Slice(mergeBlock);
                var finalBlocks = mergeSpan.Slice(0, mergeSpan.Length - blockSize);
                if (finalBlocks.Length > subarrayLength)
                {
                    var cBlocks = SortBlocks<T, TProxy, TypeTrue>(ref keyHead, finalBlocks, blockSizeExponent, in p);
                    MergeBlocksBackwardsLargeStruct(blockSizeExponent, mergeSpan, ref keyHead, medianKey, in p);
                    SortKeys(new(ref keyHead, cBlocks), mergeSpan.Slice(0, blockSize), medianKey, in p);
                }
                else
                {
                    // Real sort shouldn'currentBlockValue reach here, but it remains for the sake of fool proof measures and unit tests.
                    HandleInsufficientTailSubarray(blockSize, mergeSpan, finalBlocks);
                }
            }
            mergeBlock -= mergeLength;
            for (; mergeBlock < length; mergeBlock -= mergeLength)
            {
                var mergeSpan = span.Slice(mergeBlock, mergeLength + blockSize);
                var blocks = mergeSpan.Slice(0, mergeSpan.Length - blockSize);
                SortBlocks<T, TProxy, TypeTrue>(ref keyHead, blocks, blockSizeExponent, in p);
                MergeBlocksBackwardsLargeStruct(blockSizeExponent, mergeSpan, ref keyHead, medianKey, in p);
                SortKeys(keys, mergeSpan.Slice(0, blockSize), medianKey, in p);
            }

            static void HandleInsufficientTailSubarray(nuint blockSize, NativeSpan<T?> mergeSpan, NativeSpan<T?> finalBlocks) => InsertBufferBackwardsUnordered(ref mergeSpan.Head, finalBlocks.Length, blockSize);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CombineSubarraysLazy<T, TProxy>(ref T? keyHead, NativeSpan<T?> values, int subarrayLengthExponent, int blockSizeExponent, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var subarrayLength = (nuint)1 << subarrayLengthExponent;
            var bse = blockSizeExponent;
            var mergeLength = subarrayLength * 2;
            var span = values;
            var length = span.Length;
            var leftBlocks = subarrayLength >> bse;
            var medianKey = Unsafe.Add(ref keyHead, leftBlocks);
            var keys = new NativeSpan<T?>(ref keyHead, mergeLength >> bse);
            nuint mergeBlock = 0;
            var ol = length - mergeLength + 1;
            ref readonly var p = ref TProxy.PassReference(in proxy);
            if (ol < length)
            {
                for (; mergeBlock < ol; mergeBlock += mergeLength)
                {
                    var blocks = span.Slice(mergeBlock, mergeLength);
                    SortBlocks<T, TProxy, TypeFalse>(ref keyHead, blocks, bse, in p);
                    MergeBlocksLazyLargeStruct<T, TProxy, TypeFalse>(bse, blocks, ref keyHead, medianKey, 0, in p);
                    SortKeys(keys, default, medianKey, in p);
                }
            }
            if (mergeBlock <= length)
            {
                var finalBlocks = span.Slice(mergeBlock);

                if (finalBlocks.Length > subarrayLength)
                {
                    var cBlocks = SortBlocks<T, TProxy, TypeFalse>(ref keyHead, finalBlocks, bse, in p);
                    var lastMergeBlocks = CountLastMergeBlocks(finalBlocks, bse, in p);
                    MergeBlocksLazyLargeStruct<T, TProxy, TypeTrue>(bse, finalBlocks, ref keyHead, medianKey, lastMergeBlocks, in p);
                    SortKeys(new(ref keyHead, cBlocks), default, medianKey, in p);
                }
            }
            _ = keys;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static MergeDirection CombineBlocksFullBuffer<T, TProxy>(NativeSpan<T?> span, int subarrayLengthExponent, int blockSizeExponent, nuint keyLength, in TProxy proxy)
             where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var blockSize = (nuint)1 << blockSizeExponent;
            var keys = span.Slice(0, keyLength);
            var values = span.Slice(keyLength);
            var length = checked(values.Length - blockSize);
            var subarrayLength = (nuint)1 << subarrayLengthExponent;
            var direction = MergeDirection.Backward;
            ref readonly var p = ref TProxy.PassReference(in proxy);
            while (subarrayLength > 0 && length > subarrayLength)
            {
                var tail = CombineSubarraysForwards(ref keys.Head, values, subarrayLengthExponent, blockSizeExponent, in p);
                subarrayLengthExponent++;
                subarrayLength <<= 1;
                direction = tail > 0 ? MergeDirection.Forward : direction;
                if (subarrayLength == 0 || length <= subarrayLength) break;
                CombineSubarraysBackwards(ref keys.Head, values.Slice(0, tail + blockSize), subarrayLengthExponent, blockSizeExponent, in p);
                subarrayLengthExponent++;
                subarrayLength <<= 1;
                direction = MergeDirection.Backward;
            }
            return direction;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CombineBlocksNotEnoughBuffer<T, TProxy>(NativeSpan<T?> span, int subarrayLengthExponent, int keyLengthExponent, in TProxy proxy)
             where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var keyLength = (nuint)1 << keyLengthExponent;
            var keys = span.Slice(0, keyLength);
            var values = span.Slice(keyLength);
            var length = values.Length;
            var subarrayLength = (nuint)1 << subarrayLengthExponent;
            var halfKey = keyLength >> 1;
            ref readonly var p = ref TProxy.PassReference(in proxy);
            ShellSort.Sort(keys.Slice(0, halfKey), in p);
            var mergeSpan = span.Slice(halfKey);
            nuint tail = 0;
            var blockSizeExponent = keyLengthExponent - 1;
            while (subarrayLength > 0 && length > subarrayLength && (uint)(subarrayLengthExponent - blockSizeExponent) < (uint)blockSizeExponent)
            {
                tail = CombineSubarraysForwards(ref keys.Head, mergeSpan, subarrayLengthExponent, blockSizeExponent, in p);
                subarrayLengthExponent++;
                subarrayLength <<= 1;
                if (subarrayLength == 0 || length <= subarrayLength || subarrayLengthExponent - blockSizeExponent >= blockSizeExponent) break;
                CombineSubarraysBackwards(ref keys.Head, mergeSpan.Slice(0, tail + halfKey), subarrayLengthExponent, blockSizeExponent, in p);
                subarrayLengthExponent++;
                subarrayLength <<= 1;
                tail = 0;
            }
            if (tail > 0)
            {
                InsertBufferBackwardsUnordered(ref mergeSpan.Head, tail, halfKey);
            }
            ShellSort.Sort(keys, in p);
            while (subarrayLength > 0 && length > subarrayLength)
            {
                CombineSubarraysLazy(ref keys.Head, values, subarrayLengthExponent, subarrayLengthExponent + 1 - keyLengthExponent, in p);
                subarrayLengthExponent++;
                subarrayLength <<= 1;
            }
        }

        internal static void LazyStableSort<T, TProxy>(NativeSpan<T?> values, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            var span = values;
            if (span.Length > 1)
            {
                var sR = span;
                ref readonly var p = ref TProxy.PassReference(in proxy);
                while (!sR.IsEmpty)
                {
                    var k = sR.SliceWhileIfLongerThan(16);
                    InsertionSort.Sort(k, in p);
                    _ = sR.TrySlice(out sR, k.Length);
                }
                nuint mergeLength = 16;
                var fullMerge = mergeLength + mergeLength;
                while (mergeLength > 0 && mergeLength < span.Length)
                {
                    var ss = span;
                    do
                    {
                        var sm = ss;
                        if (fullMerge > 0 && sm.TrySliceWhile(out var sm2, fullMerge)) sm = sm2;
                        if (sm.Length <= mergeLength) break;
                        MergeBackwardsLazyLargeStruct(ref sm.Head, mergeLength, sm.Length - mergeLength, in p);
                        _ = ss.TrySlice(out ss, sm.Length);
                    } while (!ss.IsEmpty);
                    mergeLength = fullMerge;
                    fullMerge <<= 1;
                }
            }
        }

        public static void Sort<T, TProxy>(NativeSpan<T?> values, in TProxy proxy) where TProxy : struct, ILightweightComparer<T, TProxy>
        {
            ref readonly var p = ref TProxy.PassReference(in proxy);
            if (values.Length <= 16)
            {
                InsertSort(ref values, in p);
                return;
            }
            var blockSizeExponent = CalculateBlockSizeExponent(values.Length, out var blocks);
            var keys = blocks;
            var blockSize = (nuint)1 << blockSizeExponent;
            var idealKeys = blocks + blockSize;
            var bufferSize = idealKeys;
            var keysFound = CollectKeys(values, idealKeys, in p);
            var idealBuffer = keysFound >= idealKeys;
            if (!idealBuffer)
            {
                if (keysFound <= 4)
                {
                    if (keysFound <= 1) return;
                    LazyStableSort(values, in p);
                    return;
                }
                bufferSize = MathUtils.RoundDownToPowerOfTwo(keysFound, out blockSizeExponent);
                keys = 0;
            }
            BuildBlocks(values.Slice(keys), blockSizeExponent, in p);
            if (idealBuffer)
            {
                var direction = CombineBlocksFullBuffer(values, blockSizeExponent, blockSizeExponent, keys, in p);
                if (direction == MergeDirection.Forward)
                {
                    MergeForwardsLazyLargeStruct(ref values.Head, keys, values.Length - keys, in p);

                    ShellSort.Sort(values.Slice(values.Length - blockSize), in p);
                    MergeBufferBackwardsLazyLargeStruct(ref values.Head, values.Length - blockSize, blockSize, in p);
                    return;
                }
            }
            else
            {
                CombineBlocksNotEnoughBuffer(values, blockSizeExponent, blockSizeExponent, in p);
            }
            ShellSort.Sort(values.Slice(0, bufferSize), in p);
            MergeForwardsLazyLargeStruct(ref values.Head, bufferSize, values.Length - bufferSize, in p);

            static void InsertSort(ref NativeSpan<T?> values, in TProxy p) => InsertionSort.Sort(values, in p);
        }
    }
}
