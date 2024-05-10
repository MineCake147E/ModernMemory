using System.Buffers;

using ModernMemory.Buffers;

namespace ModernMemory
{
    public readonly partial struct NativeMemory<T>
    {
        internal readonly struct NativeSpanFactory : INativeSpanFactory<T>, IEquatable<NativeSpanFactory>
        {
            internal readonly NativeMemoryManager<T>? manager;
            internal readonly Memory<T> memory;

            public NativeSpanFactory(Memory<T> memory)
            {
                this.memory = memory;
            }

            public NativeSpanFactory(NativeMemoryManager<T> manager)
            {
                ArgumentNullException.ThrowIfNull(manager);
                this.manager = manager;
                memory = default;
            }

            public bool IsEmpty => manager is null && memory.IsEmpty;

            public nuint Length => manager is { } factory ? factory.Length : (nuint)memory.Length;

            public ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length)
                => manager is { } factory ? factory.CreateReadOnlyNativeSpan(start, length) : new ReadOnlyNativeSpan<T>(memory.Span).Slice(start, length);
            public ReadOnlyMemory<T> GetReadOnlyMemorySegment(nuint start)
            {
                if (manager is { } factory)
                {
                    return factory.GetReadOnlyMemorySegment(start);
                }
                ArgumentOutOfRangeException.ThrowIfGreaterThan(start, (nuint)memory.Length);
                return memory.Slice((int)start);
            }
            public MemoryHandle Pin(nuint elementIndex)
            {
                if (manager is { } factory)
                {
                    return factory.Pin(elementIndex);
                }
                ArgumentOutOfRangeException.ThrowIfGreaterThan(elementIndex, (nuint)int.MaxValue);
                return Pin((int)elementIndex);
            }
            public MemoryHandle Pin(int elementIndex)
            {
                if (manager is { } factory)
                {
                    return factory.Pin(elementIndex);
                }
                ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);
                return elementIndex == 0 ? memory.Pin() : memory.Slice(elementIndex).Pin();
            }
            public void Unpin() => manager?.Unpin();

            public ReadOnlyNativeSpan<T> GetReadOnlyNativeSpan() => manager is { } factory ? factory.GetReadOnlyNativeSpan() : new(memory.Span);

            public Memory<T> GetHeadMemory() => manager is { } factory ? factory.Memory : memory;

            public NativeSpan<T> CreateNativeSpan(nuint start, nuint length)
                => manager is { } factory ? factory.CreateNativeSpan(start, length) : new NativeSpan<T>(memory.Span).Slice(start, length);
            public NativeSpan<T> GetNativeSpan() => manager is { } factory ? factory.GetNativeSpan() : new(memory.Span);
            public override bool Equals(object? obj) => obj is NativeSpanFactory factory && Equals(factory);
            public bool Equals(NativeSpanFactory other) => manager == other.manager && memory.Equals(other.memory);
            public override int GetHashCode() => HashCode.Combine(manager, memory);

            public static bool operator ==(NativeSpanFactory left, NativeSpanFactory right) => left.Equals(right);
            public static bool operator !=(NativeSpanFactory left, NativeSpanFactory right) => !(left == right);
        }
    }
}
