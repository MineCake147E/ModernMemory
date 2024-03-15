using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Collections;

namespace ModernMemory.Buffers.DataFlow
{
    public interface IDataProviderTypedEnumerable<T, TEnumerator> : IDataProvider<T>, ITypedEnumerable<T, TEnumerator> where TEnumerator : IEnumerator<T>
    {
    }

}
