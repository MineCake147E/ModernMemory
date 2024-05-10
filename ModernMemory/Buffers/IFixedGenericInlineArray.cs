using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    internal interface IFixedGenericInlineArray<T, TSelf> where TSelf : struct, IFixedGenericInlineArray<T, TSelf>
    {
        int Length => TSelf.Count;
#pragma warning disable S2743 // Static fields should not be used in generic types (false positive)
        static abstract int Count { get; }
#pragma warning restore S2743 // Static fields should not be used in generic types
        static abstract Span<T> AsSpan(ref TSelf self);

        static NativeSpan<T> AsNativeSpan(ref TSelf self) => new(TSelf.AsSpan(ref self));
    }
}
