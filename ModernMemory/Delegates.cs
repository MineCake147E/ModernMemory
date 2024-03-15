using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    /// <inheritdoc cref="SpanAction{T, TArg}"/>
    public delegate void NativeSpanAction<T, in TArg>(NativeSpan<T> span, TArg arg);

    /// <inheritdoc cref="ReadOnlySpanAction{T, TArg}"/>
    public delegate void ReadOnlyNativeSpanAction<T, in TArg>(ReadOnlyNativeSpan<T> span, TArg arg);
}
