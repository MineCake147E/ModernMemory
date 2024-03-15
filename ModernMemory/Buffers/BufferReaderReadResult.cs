using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    public readonly struct BufferReaderReadResult<T, TArgs>(ReadOnlySequence<T> buffer, TArgs args) where TArgs : unmanaged
    {
        public ReadOnlySequence<T> Buffer { get; } = buffer;
        public TArgs Args { get; } = args;

        public bool IsCanceled => Buffer.IsEmpty;
    }
}
