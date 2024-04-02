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
        /// Negative: x is less than y<br/>
        /// Zero: x is equal to y<br/>
        /// Positive: x is greater than y
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        static abstract int Compare(in T x, in T y);
    }

    public readonly struct TransformedStaticComparisonProxy<TItem, TIntermediate, TProxy, TTransform> : IStaticComparisonProxy<TItem>
        where TProxy : IStaticComparisonProxy<TIntermediate>
        where TTransform : ITransform<TItem, TIntermediate>
    {
        public static int Compare(in TItem x, in TItem y)
            => TProxy.Compare(TTransform.Transform(x), TTransform.Transform(y));
    }

    public readonly struct ReverseStaticComparisonProxy<T, TProxy> : IStaticComparisonProxy<T> where TProxy : IStaticComparisonProxy<T>
    {
        public static int Compare(in T x, in T y)
            => -TProxy.Compare(x, y);
    }

    public readonly struct ComparisonOperatorsStaticComparisonProxy<T> : IStaticComparisonProxy<T> where T : IComparisonOperators<T, T, bool>
    {
        public static int Compare(in T x, in T y)
            => x.CompareToByComparisonOperators(y);
    }

    public readonly struct ComparableStaticComparisonProxy<T> : IStaticComparisonProxy<T> where T : IComparable<T>
    {
        public static int Compare(in T x, in T y)
            => x.CompareTo(y);
    }
}
