namespace ModernMemory.Buffers.Pooling
{
    internal sealed partial class SharedNativeMemoryPool<T>
    {
        private sealed class ThreadLocalSizedArrayPool
        {
            public nuint BufferSize { get; }

            public nuint SizeClass { get; }
            

            public MemoryOwnerContainer<T> Rent() => throw new NotImplementedException();
        }

    }
}
