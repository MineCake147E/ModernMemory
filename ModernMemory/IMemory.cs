using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using ModernMemory.Collections;
using ModernMemory.Utils;

namespace ModernMemory
{
    public interface IReadOnlyNativeMemoryBase<T> : INativeSpanCopyable<T>, ISpanEnumerable<T>
    {
        nuint Length { get; }
        bool IsEmpty => Length == 0;
        ReadOnlyNativeSpan<T> Span { get; }
        MemoryHandle Pin();
        bool TryCopyTo(NativeMemory<T> destination) => TryCopyTo(destination.Span);
        void CopyTo(NativeMemory<T> destination) => CopyTo(destination.Span);

        ReadOnlyNativeMemory<T> AsNativeMemory();
    }

    public interface INativeMemoryBase<T> : IReadOnlyNativeMemoryBase<T>
    {
        new NativeSpan<T> Span { get; }
        ReadOnlyNativeSpan<T> IReadOnlyNativeMemoryBase<T>.Span => Span;

        new NativeMemory<T> AsNativeMemory();
        ReadOnlyNativeMemory<T> IReadOnlyNativeMemoryBase<T>.AsNativeMemory() => AsNativeMemory();
    }

    public interface IReadOnlyNativeMemory<T, TSelf> : IReadOnlyNativeMemoryBase<T>, ISliceable<TSelf, nuint>
        where TSelf : IReadOnlyNativeMemory<T, TSelf>
    {
        static virtual TSelf? Empty => default;

        static virtual implicit operator ReadOnlyNativeMemory<T>(TSelf value) => value.AsNativeMemory();
    }

    public interface INativeMemory<T, TSelf> : INativeMemoryBase<T>, IReadOnlyNativeMemory<T, TSelf>, ICopyable<T, TSelf>
        where TSelf : INativeMemory<T, TSelf>
    {
        static new virtual TSelf? Empty => default;
        static TSelf? IReadOnlyNativeMemory<T, TSelf>.Empty => default;
        bool ICopyable<T, TSelf>.TryCopyTo(TSelf destination) => TryCopyTo(destination.Span);
        void ICopyable<T, TSelf>.CopyTo(TSelf destination) => TryCopyTo(destination.Span);

        static virtual implicit operator NativeMemory<T>(TSelf value) => value.AsNativeMemory();
    }
}
