using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    public static class Utils
    {
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Transfer<T>(this ref T item) where T : struct
        {
            var k = item;
            item = default;
            return k;
        }
    }
}
