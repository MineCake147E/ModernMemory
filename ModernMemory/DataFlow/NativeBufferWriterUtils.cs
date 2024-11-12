using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.DataFlow
{
    public static partial class NativeBufferWriterUtils
    {
        public static bool DefaultMaxBufferSize(out nuint space)
        {
            space = nuint.MaxValue;
            return false;
        }
    }
}
