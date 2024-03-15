using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
    public interface ITypedEnumerable<out T, TEnumerator> : IEnumerable<T> where TEnumerator : IEnumerator<T>
    {
        new TEnumerator GetEnumerator();
    }
}
