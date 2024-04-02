using ModernMemory.Utils;

namespace ModernMemory.Tests.Sorting
{
    internal readonly struct BitShiftTransform : ITransform<ulong, uint>
    {
        public static uint Transform(ulong value) => (uint)(value >> 32);
    }
}
