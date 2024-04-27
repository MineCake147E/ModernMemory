using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Utils;

namespace ModernMemory.Utils
{
    public interface IStaticComparer<T>
    {
        /// <summary>
        /// Negative: x &lt; y<br/>
        /// Zero: x == y<br/>
        /// Positive: x &gt; y
        /// </summary>
        static abstract nint Compare(T? x, T? y);
    }

    public readonly struct StaticComparer<T, TProxy> : ILightweightComparer<T, StaticComparer<T, TProxy>>
        where TProxy : unmanaged, IStaticComparer<T>
    {
        public static nint Compare(T? x, T? y, StaticComparer<T, TProxy> self) => TProxy.Compare(x, y);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static StaticComparer<T, TProxy> ILightweightComparer<T, StaticComparer<T, TProxy>>.Load(ref readonly StaticComparer<T, TProxy> source) => default;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ref readonly StaticComparer<T, TProxy> ILightweightComparer<T, StaticComparer<T, TProxy>>.PassReference(ref readonly StaticComparer<T, TProxy> source) => ref Unsafe.NullRef<StaticComparer<T, TProxy>>();

        static StaticComparer<T, TProxy> ILightweightComparer<T, StaticComparer<T, TProxy>>.Pass(StaticComparer<T, TProxy> source) => default;
    }

    public static class StaticComparer
    {
        public static StaticComparer<T, TProxy> Create<T, TProxy>() where TProxy : unmanaged, IStaticComparer<T>
            => new();
    }

    public readonly struct TransformedStaticComparer<TItem, TIntermediate, TProxy, TTransform> : IStaticComparer<TItem>
        where TProxy : unmanaged, IStaticComparer<TIntermediate>
        where TTransform : ITransform<TItem, TIntermediate>
    {
        public static nint Compare(TItem? x, TItem? y)
            => TProxy.Compare(TTransform.Transform(x), TTransform.Transform(y));
    }

    public readonly struct ReverseStaticComparer<T, TProxy> : IStaticComparer<T>
        where TProxy : unmanaged, IStaticComparer<T>
    {
        public static nint Compare(T? x, T? y)
            => -TProxy.Compare(x, y);
    }

    public readonly struct ComparisonOperatorsStaticComparer<T> : IStaticComparer<T> where T : IComparisonOperators<T, T, bool>
    {
        public static nint Compare(T? x, T? y)
        {
            bool p;
            bool n;
            if (x is null || y is null)
            {
                p = y is null;
                n = x is null;
            }
            else
            {
                p = x > y;
                n = x < y;
            }
            return (nint)((p ? (nuint)1 : 0) - (n ? (nuint)1 : 0));
        }
    }

    public readonly struct ComparableStaticComparer<T> : IStaticComparer<T> where T : IComparable<T>
    {
        public static nint Compare(T? x, T? y)
            => x.CompareTo(y);
    }
}
