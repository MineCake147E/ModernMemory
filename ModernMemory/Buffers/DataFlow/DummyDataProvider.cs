using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers.DataFlow
{
    public readonly struct DummyDataProvider<T> : IDataProvider<T>
    {
        public void AdvanceRead(nuint count) { }
        public nuint PeekAtMost(NativeSpan<T> destination, nuint offset = 0U) => destination.Length;
        public void PeekAtMostTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count = 0U, nuint offset = 0U) where TBufferWriter : INativeBufferWriter<T>
            => bufferWriter.Advance(count);
        public void PeekExact(NativeSpan<T> destination, nuint offset = 0U) { }
        public void PeekExactTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count, nuint offset = 0U) where TBufferWriter : INativeBufferWriter<T>
            => bufferWriter.Advance(count);
        public void Prefetch(nuint count) { }

        public AvailableElementsResult TryGetAvailableElements(out nuint count)
        {
            count = nuint.MaxValue;
            return AvailableElementsResult.Uncountable;
        }
        public bool TryPeekExact(NativeSpan<T> destination, nuint offset = 0U) => true;
        public void WriteAtMostTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count = 0U) where TBufferWriter : INativeBufferWriter<T>
            => bufferWriter.Advance(count);
        public void WriteExactTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count) where TBufferWriter : INativeBufferWriter<T>
            => bufferWriter.Advance(count);
    }
}
