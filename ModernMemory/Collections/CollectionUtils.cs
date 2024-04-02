using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
    public static class CollectionUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearIfReferenceOrContainsReferences<T, TClearable>(this ref TClearable clearable) where TClearable : struct, IClearable<T>
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                clearable.Clear();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ClearIfReferenceOrContainsReferences<T, TClearable>(this TClearable clearable) where TClearable : class, IClearable<T>
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                clearable.Clear();
            }
        }
    }
}
