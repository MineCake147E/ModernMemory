namespace ModernMemory.Tests
{
    public readonly struct SliceData
    {
        public SliceData(nuint start)
        {
            Start = start;
            SliceByLength = false;
        }

        public SliceData(nuint start, nuint length)
        {
            Start = start;
            Length = length;
            SliceByLength = true;
        }

        public nuint Start { get; }
        public nuint Length { get; }
        public bool SliceByLength { get; }

        public override string ToString() => SliceByLength ? $"<Start: {Start}, Length: {Length}>" : $"<Start: {Start}>";
    }
}
