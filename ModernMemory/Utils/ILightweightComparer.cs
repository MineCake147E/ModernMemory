using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Utils
{
    public interface ILightweightComparer<in T, TSelf> where TSelf : struct, ILightweightComparer<T, TSelf>
    {
        /// <summary>
        /// Negative: x is less than y<br/>
        /// Zero: x is equal to y<br/>
        /// Positive: x is greater than y
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        static abstract nint Compare(T? x, T? y, TSelf self);

        static virtual TSelf Load(ref readonly TSelf source) => source;

        static virtual ref readonly TSelf PassReference(ref readonly TSelf source) => ref source;

        static virtual TSelf Pass(TSelf source) => source;
    }

    public readonly struct ComparerWrapper<T, TComparer>(TComparer comparer) : ILightweightComparer<T, ComparerWrapper<T, TComparer>> where TComparer : IComparer<T>
    {
        public TComparer Comparer { get; } = comparer;
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nint Compare(T? x, T? y, ComparerWrapper<T, TComparer> self) => self.Comparer.Compare(x, y);

        public static implicit operator ComparerWrapper<T, TComparer>(TComparer comparer) => new(comparer);
    }
}
