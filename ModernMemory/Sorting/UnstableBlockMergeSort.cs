﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Utils;

namespace ModernMemory.Sorting
{
    // Fast unstable block merge sort inspired from Holy GrailSort

    public readonly struct UnstableBlockMergeSort : ISortAlgorithm
    {
        public static void Sort<T, TLightweightComparer>(NativeSpan<T?> values, in TLightweightComparer comparer) where TLightweightComparer : struct, ILightweightComparer<T, TLightweightComparer> => throw new NotImplementedException();

        public static void SortByStaticComparer<T, TStaticComparisonProxy>(NativeSpan<T?> values) where TStaticComparisonProxy : unmanaged, IStaticComparer<T>
        {

        }
    }
}
