using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    public static partial class NativeMemoryUtils
    {
        public static void Rotate<T>(this scoped Span<T> span, int position) => span.AsNativeSpan().Rotate((nuint)position);
    }
}
