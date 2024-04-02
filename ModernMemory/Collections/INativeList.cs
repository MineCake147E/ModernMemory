using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
    public interface INativeList<T> : INativeCollection<T>, IReadOnlyNativeList<T>, INativeIndexable<T>
    {
    }
}
