using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
#pragma warning disable S2326 // Unused type parameters should be removed
    public interface IClearable<T>
#pragma warning restore S2326 // Unused type parameters should be removed
    {
        void Clear();
    }
}
