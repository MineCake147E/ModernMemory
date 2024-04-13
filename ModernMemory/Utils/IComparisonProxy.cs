using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Utils
{
    public interface IComparisonProxy<in T, TSelf> where TSelf : struct, IComparisonProxy<T, TSelf>
    {
        /// <summary>
        /// Negative: x is less than y<br/>
        /// Zero: x is equal to y<br/>
        /// Positive: x is greater than y
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        static abstract nint Compare(TSelf self, T? x, T? y);
    }

    public readonly struct ComparerComparisonProxy<T, TComparer>(TComparer comparer) : IComparisonProxy<T, ComparerComparisonProxy<T, TComparer>> where TComparer : IComparer<T>
    {
        public TComparer Comparer { get; } = comparer;
        public static nint Compare(ComparerComparisonProxy<T, TComparer> self, T? x, T? y) => self.Comparer.Compare(x, y);
    }
}
