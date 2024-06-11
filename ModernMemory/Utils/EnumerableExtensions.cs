using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.DataFlow;

namespace ModernMemory.Utils
{
    public static class EnumerableExtensions
    {
        public static nuint WriteTo<T>(this IEnumerable<T> values, scoped NativeSpan<T> destination)
        {
            nuint count = 0;
            using var enumerator = values.GetEnumerator();
            while (enumerator.MoveNext() && count < destination.Length)
            {
                destination[count++] = enumerator.Current;
            }
            return count;
        }
    }
}
