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
        static abstract void SortByStaticProxy<T, TStaticComparisonProxy>(NativeSpan<T?> values) where TStaticComparisonProxy : IStaticComparisonProxy<T>;
    }
}
