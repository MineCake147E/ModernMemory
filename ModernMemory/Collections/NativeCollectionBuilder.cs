using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
    internal static class NativeCollectionBuilder
    {
        internal static NativeQueueCore<T> CreateCore<T>(ReadOnlySpan<T> span) => new(span);

        internal static NativeQueue<T> CreateNativeQueue<T>(ReadOnlySpan<T> span) => new(CreateCore(span));
        internal static OverwritableNativeQueue<T> CreateOverwritableNativeQueue<T>(ReadOnlySpan<T> span) => new(CreateCore(span));

        internal static NativePile<T> CreateNativePile<T>(ReadOnlySpan<T> span) => new(span);
    }
}
