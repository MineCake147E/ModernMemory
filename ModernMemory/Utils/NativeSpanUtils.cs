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
            // We are allocating a new memory, but the Span<TRange> does the same thing.
            return new(span.ToArray());
        }
    }
}
