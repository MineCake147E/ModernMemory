using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory
{
    public static partial class NativeMemoryUtils
    {
        public static void Rotate<T>(this scoped Span<T> span, int position) => span.AsNativeSpan().Rotate((nuint)position);

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
                        SwapValues(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, mid), headLength);
                    }
                    return;
                }
                if (minLen <= 1) break;
                if (headLength <= tailLength)
                {
                    var tail = end - headLength;
                    while (tail <= end && tail >= mid)
                    {
                        SwapValues(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, tail), headLength);
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
                        SwapValues(ref Unsafe.Add(ref head, mid), ref Unsafe.Add(ref head, end), ml);
                        tailLength = ml;
                        headLength -= ml;
                    }
                }
                else
                {
                    var tail = start + tailLength;
                    while (tail >= start && tail <= mid)
                    {
                        SwapValues(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, mid), tailLength);
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
                        SwapValues(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, mid), ml);
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

        internal static void RotateBuffered<T, TArray>(ref T head, nuint leftLength, nuint rightLength, ref TArray buf)
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
                        SwapBuffered(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, mid), headLength, ref buf);
                    }
                    return;
                }
                if (minLen <= sizeThreshold) break;
                if (headLength <= tailLength)
                {
                    var tail = end - headLength;
                    while (tail <= end && tail >= mid)
                    {
                        SwapBuffered(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, tail), headLength, ref buf);
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
                        SwapBuffered(ref Unsafe.Add(ref head, mid), ref Unsafe.Add(ref head, end), ml, ref buf);
                        tailLength = ml;
                        headLength -= ml;
                    }
                }
                else
                {
                    var tail = start + tailLength;
                    while (tail >= start && tail <= mid)
                    {
                        SwapBuffered(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, mid), tailLength, ref buf);
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
                        SwapBuffered(ref Unsafe.Add(ref head, start), ref Unsafe.Add(ref head, mid), ml, ref buf);
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


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="head"></param>
        /// <param name="count">The number of elements to be shifted.</param>
        internal static void InsertForwards<T>(ref T head, nuint count)
        {
            var item = head;
            MoveMemory(ref head, ref Unsafe.Add(ref head, 1), count);
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
            MoveMemory(ref MemoryMarshal.GetReference(bfs), ref head, headLength);
            MoveMemory(ref head, ref Unsafe.Add(ref head, headLength), count);
            MoveMemory(ref Unsafe.Add(ref head, count), ref MemoryMarshal.GetReference(bfs), headLength);
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
            MoveMemory(ref Unsafe.Add(ref head, 1), ref head, count);
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
            MoveMemory(ref MemoryMarshal.GetReference(bfs), ref Unsafe.Add(ref head, count), tailLength);
            MoveMemory(ref Unsafe.Add(ref head, tailLength), ref head, count);
            MoveMemory(ref head, ref MemoryMarshal.GetReference(bfs), tailLength);
        }
    }
}
