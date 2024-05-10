using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.DataFlow
{
    public readonly struct ConstantDataProvider<T>(T value) : IDataProvider<T>
    {
        public T Value { get; } = value;
        public void AdvanceRead(nuint count) { }
        public nuint PeekAtMost(NativeSpan<T> destination, nuint offset = 0U)
        {
            destination.Fill(Value);
            return destination.Length;
        }
        public void PeekAtMostTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count = 0U, nuint offset = 0U) where TBufferWriter : INativeBufferWriter<T>
        {
            var span = bufferWriter.GetNativeSpan(count).Slice(0, count);
            span.Fill(Value);
            bufferWriter.Advance(span.Length);
        }
        public void PeekExact(NativeSpan<T> destination, nuint offset = 0U) => destination.Fill(Value);
        public void PeekExactTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count, nuint offset = 0U) where TBufferWriter : INativeBufferWriter<T>
        {
            var span = bufferWriter.GetNativeSpan(count).Slice(0, count);
            span.Fill(Value);
            bufferWriter.Advance(span.Length);
        }
        public AvailableElementsResult TryGetAvailableElements(out nuint count)
        {
            count = nuint.MaxValue;
            return AvailableElementsResult.Infinite;
        }
        public bool TryPeekExact(NativeSpan<T> destination, nuint offset = 0U)
        {
            destination.Fill(Value);
            return true;
        }
        public void WriteAtMostTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count = 0U) where TBufferWriter : INativeBufferWriter<T>
            => PeekExactTo(bufferWriter, count);
        public void ReadExact(NativeSpan<T> destination) => PeekExact(destination);
        public nuint ReadAtMost(NativeSpan<T> destination) => PeekAtMost(destination);
        public bool TryReadExact(NativeSpan<T> destination) => TryPeekExact(destination);

        public void WriteExactTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count) where TBufferWriter : INativeBufferWriter<T>
            => PeekExactTo(bufferWriter, count);
        public void Prefetch(nuint count) { }
    }
}
