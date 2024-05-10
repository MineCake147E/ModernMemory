namespace ModernMemory.DataFlow
{
    [Flags]
    public enum ReadingMethods : uint
    {
        None = 0u,
        PeekSpan = 1u,
        PeekBufferWriter = 2u,
        Enumerator = 4u,
        Buffered = 8u,
        Sequence = 16u,

        Pull = PeekSpan | PeekBufferWriter
    }
}
