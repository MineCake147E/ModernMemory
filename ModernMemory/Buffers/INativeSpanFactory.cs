using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    public interface IReadOnlyNativeSpanFactory<T> : INativePinnable
    {
        nuint Length { get; }
        ReadOnlyNativeSpan<T> GetReadOnlyNativeSpan();
        ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length);
        ReadOnlyMemory<T> GetReadOnlyMemorySegment(nuint start);
    }

    public interface INativeSpanFactory<T> : IReadOnlyNativeSpanFactory<T>
    {
        NativeSpan<T> GetNativeSpan();
        NativeSpan<T> CreateNativeSpan(nuint start, nuint length);
    }

    internal static class NativeSpanFactoryUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool IsInRange<T, TFactory>(this TFactory factory, nuint start, nuint length) where TFactory : IReadOnlyNativeSpanFactory<T>
            => MathUtils.IsRangeInRange(factory.Length, start, length);
    }
}
