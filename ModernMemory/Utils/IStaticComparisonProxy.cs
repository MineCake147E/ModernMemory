using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Utils;

namespace ModernMemory.Utils
{
    public interface IStaticComparisonProxy<T>
    {
        /// <summary>
        /// Negative: x &lt; y<br/>
        /// Zero: x == y<br/>
        /// Positive: x &gt; y
        /// </summary>
        static abstract nint Compare(T? x, T? y);
    }

    public readonly struct StaticComparisonProxy<T, TProxy> : IComparisonProxy<T, StaticComparisonProxy<T, TProxy>>
        where TProxy : IStaticComparisonProxy<T>
    {
        public static nint Compare(StaticComparisonProxy<T, TProxy> self, T? x, T? y) => TProxy.Compare(x, y);
    }

    public readonly struct TransformedStaticComparisonProxy<TItem, TIntermediate, TProxy, TTransform> : IStaticComparisonProxy<TItem>
        where TProxy : IStaticComparisonProxy<TIntermediate>
        where TTransform : ITransform<TItem, TIntermediate>
    {
        public static nint Compare(TItem? x, TItem? y)
            => TProxy.Compare(TTransform.Transform(x), TTransform.Transform(y));
    }

    public readonly struct ReverseStaticComparisonProxy<T, TProxy> : IStaticComparisonProxy<T> where TProxy : IStaticComparisonProxy<T>
    {
        public static nint Compare(T? x, T? y)
            => -TProxy.Compare(x, y);
    }

    public readonly struct ComparisonOperatorsStaticComparisonProxy<T> : IStaticComparisonProxy<T> where T : IComparisonOperators<T, T, bool>
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

    public readonly struct ComparableStaticComparisonProxy<T> : IStaticComparisonProxy<T> where T : IComparable<T>
    {
        public static nint Compare(T? x, T? y)
            => x.CompareTo(y);
    }
}
