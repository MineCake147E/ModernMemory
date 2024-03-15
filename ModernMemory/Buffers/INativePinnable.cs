using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    /// <inheritdoc cref="IPinnable"/>
    public interface INativePinnable : IPinnable
    {
        /// <inheritdoc cref="MemoryManager{T}.Pin(int)"/>
        MemoryHandle Pin(nuint elementIndex);
    }
}
