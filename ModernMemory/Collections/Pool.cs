using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
    public sealed class Pool<T> where T : class, IPooled
    {

    }

    public interface IPooled
    {
        bool IsTrimmable { get; }
    }
}
