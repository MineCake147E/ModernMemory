using System.Buffers;

using ModernMemory.Buffers;

namespace ModernMemory
{
    public readonly partial struct NativeMemory<T>
    {
        internal readonly struct NativeSpanFactory : INativeSpanFactory<T>, IEquatable<NativeSpanFactory>
        {
            internal readonly INativeSpanFactory<T>? nativeSpanFactory;
            internal readonly Memory<T> memory;

            public NativeSpanFactory(Memory<T> memory)
            {
                this.memory = memory;
            }

            public NativeSpanFactory(INativeSpanFactory<T> nativeSpanFactory)
            {
                ArgumentNullException.ThrowIfNull(nativeSpanFactory);
                if (nativeSpanFactory is NativeSpanFactory factory)
                {
                    this = factory;
                }
                else
                {
                    this.nativeSpanFactory = nativeSpanFactory;
                    memory = default;
                }
            }

            public bool IsEmpty => nativeSpanFactory is null && memory.IsEmpty;

            public nuint Length => nativeSpanFactory is { } factory ? factory.Length : (nuint)memory.Length;

            public ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length)
                => nativeSpanFactory is { } factory ? factory.CreateReadOnlyNativeSpan(start, length) : new ReadOnlyNativeSpan<T>(memory.Span).Slice(start, length);
            public ReadOnlyMemory<T> GetReadOnlyMemorySegment(nuint start)
            {
                if (nativeSpanFactory is { } factory)
                {
                    return factory.GetReadOnlyMemorySegment(start);
                }
                ArgumentOutOfRangeException.ThrowIfGreaterThan(start, (nuint)memory.Length);
                return memory.Slice((int)start);
            }
            public MemoryHandle Pin(nuint elementIndex)
            {
                if (nativeSpanFactory is { } factory)
                {
                    return factory.Pin(elementIndex);
                }
                ArgumentOutOfRangeException.ThrowIfGreaterThan(elementIndex, (nuint)int.MaxValue);
                return Pin((int)elementIndex);
            }
            public MemoryHandle Pin(int elementIndex)
            {
                if (nativeSpanFactory is { } factory)
                {
                    return factory.Pin(elementIndex);
                }
                ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);
                return elementIndex == 0 ? memory.Pin() : memory.Slice(elementIndex).Pin();
            }
            public void Unpin() => nativeSpanFactory?.Unpin();

            public ReadOnlyNativeSpan<T> GetReadOnlyNativeSpan() => nativeSpanFactory is { } factory ? factory.GetReadOnlyNativeSpan() : new(memory.Span);

            public NativeSpan<T> CreateNativeSpan(nuint start, nuint length)
                => nativeSpanFactory is { } factory ? factory.CreateNativeSpan(start, length) : new NativeSpan<T>(memory.Span).Slice(start, length);
            public NativeSpan<T> GetNativeSpan() => nativeSpanFactory is { } factory ? factory.GetNativeSpan() : new(memory.Span);
            public override bool Equals(object? obj) => obj is NativeSpanFactory factory && Equals(factory);
            public bool Equals(NativeSpanFactory other) => nativeSpanFactory == other.nativeSpanFactory && memory.Equals(other.memory);
            public override int GetHashCode() => HashCode.Combine(nativeSpanFactory, memory);

            public static bool operator ==(NativeSpanFactory left, NativeSpanFactory right) => left.Equals(right);
            public static bool operator !=(NativeSpanFactory left, NativeSpanFactory right) => !(left == right);
        }
    }
}
