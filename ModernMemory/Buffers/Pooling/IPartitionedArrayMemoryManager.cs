namespace ModernMemory.Buffers.Pooling
{
    internal interface IPartitionedArrayMemoryManager<T>
    {
        nuint Length { get; }
        NativeSpan<T> GetNativeSpanForSegment(nuint segmentIndex);
        NativeMemory<T> GetNativeMemoryForSegment(nuint segmentIndex);
        Memory<T> GetMemoryForSegment(nuint segmentIndex);
        void Return(nuint segmentIndex);
        bool TryAllocate(out nuint allocatedSegmentId, nuint retryCount = 0U);
    }
}