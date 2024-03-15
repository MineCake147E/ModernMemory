using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    internal static class ReadOnlyNativeSpanUtils
    {
        internal static ReadOnlyNativeSpan<T> Create<T>(ReadOnlySpan<T> span) => new(span);
    }
}
