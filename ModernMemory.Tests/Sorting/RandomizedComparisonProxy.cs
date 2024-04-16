using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using ModernMemory.Utils;

namespace ModernMemory.Tests.Sorting
{
    internal readonly struct RandomizedComparisonProxy<T> : IStaticComparisonProxy<T>
    {
        public static nint Compare(T? x, T? y)
        {
            Unsafe.SkipInit<nint>(out var v);
            Random.Shared.NextBytes(MemoryMarshal.AsBytes(new Span<nint>(ref v)));
            return v;
        }
    }

    internal readonly struct PositiveBiasedRandomizedComparisonProxy<T> : IStaticComparisonProxy<T>
    {
        public static nint Compare(T? x, T? y)
            => Random.Shared.Next(-1, 3);
    }
}
