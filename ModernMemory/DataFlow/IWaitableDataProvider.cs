using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.DataFlow
{
    public interface IWaitableDataProvider<T> : IDataProvider<T>
    {
        /// <summary>
        /// Waits until the number of elements available overall becomes greater than or equal to <paramref name="length"/>.
        /// </summary>
        /// <param name="length">The number of elements to wait until.</param>
        /// <returns></returns>
        ValueTask WaitAsync(nuint length = 1);
    }
}
