using System.Buffers;
using System.Runtime.CompilerServices;

using ModernMemory.Buffers;

namespace ModernMemory
{
    public readonly partial struct ReadOnlyNativeMemory<T>
    {
        internal readonly struct ReadOnlyNativeSpanFactory : IReadOnlyNativeSpanFactory<T>
        {
            private readonly IReadOnlyNativeSpanFactory<T>? nativeSpanFactory;
            private readonly ReadOnlyMemory<T> memory;

            public ReadOnlyNativeSpanFactory(ReadOnlyMemory<T> memory)
            {
                this.memory = memory;
                nativeSpanFactory = default;
            }

            [SkipLocalsInit]
            public ReadOnlyNativeSpanFactory(IReadOnlyNativeSpanFactory<T> readOnlyNativeSpanFactory)
            {
                ArgumentNullException.ThrowIfNull(readOnlyNativeSpanFactory);
                if (readOnlyNativeSpanFactory is ReadOnlyNativeSpanFactory factory)
                {
                    this = factory;
                }
                else if(readOnlyNativeSpanFactory is NativeMemory<T>.NativeSpanFactory factory1)
                {
                    this = new(factory1);
                }
                else
                {
                    nativeSpanFactory = readOnlyNativeSpanFactory;
                    memory = default;
                }
            }

            public ReadOnlyNativeSpanFactory(NativeMemory<T>.NativeSpanFactory nativeSpanFactory)
            {
                this.nativeSpanFactory = nativeSpanFactory.nativeSpanFactory;
                memory = nativeSpanFactory.memory;
            }

            public bool IsEmpty => nativeSpanFactory is null && memory.IsEmpty;

            public nuint Length => nativeSpanFactory is { } factory ? factory.Length : (nuint)memory.Length;

            public ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length)
            {
                if (nativeSpanFactory is { } factory)
                {
                    return factory.CreateReadOnlyNativeSpan(start, length);
                }
                else if (!memory.IsEmpty)
                {
                    return new ReadOnlyNativeSpan<T>(memory.Span).Slice(start, length);
                }
                return default;
            }

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

            public ReadOnlyNativeSpan<T> GetReadOnlyNativeSpan()
            {
                if (nativeSpanFactory is { } factory)
                {
                    return factory.GetReadOnlyNativeSpan();
                }
                else if (!memory.IsEmpty)
                {
                    return new(memory.Span);
                }
                return default;
            }
        }
    }
}
