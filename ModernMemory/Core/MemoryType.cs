using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    public enum MemoryType : sbyte
    {
        String = -2,
        Array = -1,
        MemoryManager,
        NativeMemoryManager,
    }
}
