using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using ModernMemory.Buffers;
using ModernMemory.Utils;

namespace ModernMemory.Sorting
{
    public readonly partial struct AdaptiveOptimizedGrailSort
    {
        internal struct SortBuffer<T, TArray>(TArray array) where TArray : struct, IFixedGenericInlineArray<T, TArray>
        {
            private TArray array = array;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool UseBuffer() => (RuntimeHelpers.IsReferenceOrContainsReferences<TArray>() || default(TArray) is not DummyFixedArray<T>) && TArray.Count >= 1;

            public static void Rotate(ref T head, nuint leftLength, nuint rightLength, ref SortBuffer<T, TArray> sortBuffer)
            {
                if (UseBuffer())
                {
                    ref var array = ref sortBuffer.array;
                    if (!Unsafe.IsNullRef(in array))
                    {
                        NativeMemoryUtils.RotateBuffered(ref head, leftLength, rightLength, ref array);
                        return;
                    }
                }
                NativeMemoryUtils.Rotate(ref head, leftLength, rightLength);
            }

            public static void Swap(ref T x, ref T y, nuint length, ref SortBuffer<T, TArray> sortBuffer)
            {
                if (UseBuffer())
                {
                    if (length < 2)
                    {
                        if (length == 1)
                        {
                            (x, y) = (y, x);
                        }
                        return;
                    }
                    ref var array = ref sortBuffer.array;
                    if (!Unsafe.IsNullRef(in array))
                    {
                        NativeMemoryUtils.SwapBuffered(ref x, ref y, length, ref array);
                        return;
                    }
                }
                NativeMemoryUtils.SwapValues(ref x, ref y, length);
            }
        }

        internal static SortBuffer<T, DummyFixedArray<T>> CreateDummySortBuffer<T>() => default;

        [StructLayout(LayoutKind.Sequential)]
        [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
        internal struct DummyFixedArray<T> : IFixedGenericInlineArray<T, DummyFixedArray<T>>
        {
            public readonly int Length => 0;

            public static int Count => 0;

            public static Span<T> AsSpan(ref DummyFixedArray<T> self) => default;
            private readonly string GetDebuggerDisplay()
            {
                ReadOnlySpan<T> a = default;
                return $"{nameof(DummyFixedArray<T>)}<{typeof(T).Name}>[{Length}] {{ {string.Join(", ", a.ToArray())} }}";
            }
            public override string? ToString() => GetDebuggerDisplay();
        }
    }
}
