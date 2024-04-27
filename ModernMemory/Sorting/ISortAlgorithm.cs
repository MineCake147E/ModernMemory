using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Utils;

namespace ModernMemory.Sorting
{
    public interface ISortAlgorithm
    {
        static virtual bool IsStableByDefault => false;
        static abstract void SortByStaticComparer<T, TStaticComparer>(NativeSpan<T?> values) where TStaticComparer : unmanaged, IStaticComparer<T>;
        static abstract void Sort<T, TLightweightComparer>(NativeSpan<T?> values, in TLightweightComparer comparer) where TLightweightComparer : struct, ILightweightComparer<T, TLightweightComparer>;
    }
}
