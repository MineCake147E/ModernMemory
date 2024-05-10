using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    internal static class ReadOnlyMemorySequenceUtils
    {
        internal static ReadOnlyMemorySequence<T> Create<T>(ReadOnlySpan<T> values) => values.IsEmpty ? new() : new(values.ToArray());
    }
}
