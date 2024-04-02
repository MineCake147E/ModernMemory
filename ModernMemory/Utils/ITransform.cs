using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Utils
{
    public interface ITransform<in TIn, out TOut>
    {
        static abstract TOut Transform(TIn value);
    }
}
