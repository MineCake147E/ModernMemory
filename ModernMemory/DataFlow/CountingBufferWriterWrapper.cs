using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.DataFlow
{
    public struct CountingBufferWriterWrapper<T, TBufferWriter> : INativeBufferWriter<T> where TBufferWriter : INativeBufferWriter<T>
    {
        TBufferWriter? bufferWriter;

        public CountingBufferWriterWrapper(TBufferWriter bufferWriter) : this()
        {
            ArgumentNullException.ThrowIfNull(bufferWriter);
            this.bufferWriter = bufferWriter;
        }

        public nuint Count { get; private set; }

        public TBufferWriter? Dismount()
        {
            var res = bufferWriter;
            bufferWriter = default;
            return res;
        }

        public void Advance(nuint count)
        {
            ObjectDisposedException.ThrowIf(bufferWriter is null, this);
            bufferWriter.Advance(count);
            Count += count;
        }
        public void Advance(int count)
        {
            ObjectDisposedException.ThrowIf(bufferWriter is null, this);
            bufferWriter.Advance(count);
            Count += (nuint)count;
        }
        public Memory<T> GetMemory(int sizeHint = 0)
        {
            ObjectDisposedException.ThrowIf(bufferWriter is null, this);
            return bufferWriter.GetMemory(sizeHint);
        }

        public NativeMemory<T> GetNativeMemory(nuint sizeHint = 0U)
        {
            ObjectDisposedException.ThrowIf(bufferWriter is null, this);
            return bufferWriter.GetNativeMemory(sizeHint);
        }

        public NativeSpan<T> GetNativeSpan(nuint sizeHint = 0U)
        {
            ObjectDisposedException.ThrowIf(bufferWriter is null, this);
            return bufferWriter.GetNativeSpan(sizeHint);
        }

        public Span<T> GetSpan(int sizeHint = 0)
        {
            ObjectDisposedException.ThrowIf(bufferWriter is null, this);
            return bufferWriter.GetSpan(sizeHint);
        }

        public bool TryGetMaxBufferSize(out nuint space)
        {
            ObjectDisposedException.ThrowIf(bufferWriter is null, this);
            return bufferWriter.TryGetMaxBufferSize(out space);
        }

        public NativeMemory<T> TryGetNativeMemory(nuint sizeHint = 0U)
        {
            ObjectDisposedException.ThrowIf(bufferWriter is null, this);
            return bufferWriter.TryGetNativeMemory(sizeHint);
        }

        public NativeSpan<T> TryGetNativeSpan(nuint sizeHint = 0U)
        {
            ObjectDisposedException.ThrowIf(bufferWriter is null, this);
            return bufferWriter.TryGetNativeSpan(sizeHint);
        }
    }
}
