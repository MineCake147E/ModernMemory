using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Collections
{
    public sealed class DoubleBinBuffer<T>
    {
        private NativeMemory<T> buffer0;
        private NativeMemory<T> buffer1;


        private struct Bin : IDisposable
        {
            private nuint readHead;
            private nuint writeHead;
            private NativeMemory<T> memory;
            public NativeMemory<T> WrittenMemory => memory.Slice(0, writeHead);
            INativeMemoryOwner<T> Owner { get; }

            public void Dispose()
            {
                memory = default;
                Owner.Dispose();
            }
        }
    }
}
