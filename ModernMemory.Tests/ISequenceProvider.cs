using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Tests
{
    public interface ISequenceProvider<T>
    {
        static abstract void GenerateSequence(NativeSpan<T> destination);
    }
}
