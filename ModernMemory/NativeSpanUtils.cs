using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    internal static class NativeSpanUtils
    {
        internal static NativeSpan<T> Create<T>(ReadOnlySpan<T> span)
        {
            if (span.IsEmpty) return default;
            // We are allocating a new array, but the Span<TRange> does the same thing.
            var a = new T[span.Length];
            span.CopyTo(a);
            return new(a);
        }
    }
}
