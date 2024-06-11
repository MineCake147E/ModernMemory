using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory
{
    internal static class NativeMemoryCore
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe MemoryHandle Pin<T>(MemoryType type, object? medium, nuint start)
        {
            MemoryHandle result = default;
            if (medium is not null)
            {
                nuint maxIndex = int.MaxValue;
                var intIndex = (int)start;
                switch (type)
                {
                    case MemoryType.String when RuntimeHelpers.IsReferenceOrContainsReferences<T>() == RuntimeHelpers.IsReferenceOrContainsReferences<char>() && typeof(T) == typeof(char):
                        Debug.Assert(medium is string);
                        var ust = Unsafe.As<string>(medium);
                        Debug.Assert(start < (uint)ust.Length);
                        var strHandle = GCHandle.Alloc(ust, GCHandleType.Pinned);
                        result = new(Unsafe.AsPointer(ref Unsafe.Add(ref Unsafe.AsRef(in ust.GetPinnableReference()), intIndex)), strHandle);
                        break;
                    case MemoryType.Array:
                        Debug.Assert(medium is T[]);
                        var array = Unsafe.As<T[]>(medium);
                        Debug.Assert(start < (uint)array.Length);
                        var handle = GCHandle.Alloc(array, GCHandleType.Pinned);
                        var arrayPointer = Unsafe.AsPointer(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), intIndex));
                        result = new(arrayPointer, handle);
                        break;
                    case MemoryType.NativeMemoryManager:
                        maxIndex = nuint.MaxValue;
                        Debug.Assert(medium is NativeMemoryManager<T>);
                        var nativeMemoryManager = Unsafe.As<NativeMemoryManager<T>>(medium);
                        result = nativeMemoryManager.Pin(start);
                        break;
                    default:
                        Debug.Assert(medium is MemoryManager<T>);
                        var manager = Unsafe.As<MemoryManager<T>>(medium);
                        Debug.Assert(start < (uint)manager.Memory.Length);
                        result = manager.Pin(intIndex);
                        break;
                }
                ArgumentOutOfRangeException.ThrowIfGreaterThan(start, maxIndex);
            }
            return result;
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        public static void ThrowSliceExceptions(nuint start, nuint length, nuint sourceLength)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, sourceLength);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, sourceLength - start);
            throw new InvalidOperationException("Something went wrong!");
        }
    }
}
