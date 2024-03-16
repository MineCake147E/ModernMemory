using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers.DataFlow.WrappedDataProviders
{
    public sealed class SequenceToBaseDataProvider<T, TSequenceDataReader> : IDataProvider<T>
        where TSequenceDataReader : ISequenceDataReader<T>
    {
#pragma warning disable S2933 // Fields that are only assigned in the constructor should be "readonly" (TSequenceDataReader may be struct)
        private TSequenceDataReader sequenceDataProvider;
#pragma warning restore S2933 // Fields that are only assigned in the constructor should be "readonly"
        private ReadOnlySequence<T> sequence;
        private AvailableElementsResult resultCache;

        public SequenceToBaseDataProvider(TSequenceDataReader sequenceDataProvider)
        {
            ArgumentNullException.ThrowIfNull(sequenceDataProvider);
            this.sequenceDataProvider = sequenceDataProvider;
        }

        public void AdvanceRead(nuint count)
        {
            if (count == 0) return;
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, (ulong)long.MaxValue);
            var s = sequence;
            var pos = s.GetPosition((long)count);
            sequence = s = s.Slice(pos);
            if (s.IsEmpty) Refill(pos, pos);
        }

        private void Refill(SequencePosition read, SequencePosition evaluated)
        {
            sequenceDataProvider.AdvanceTo(read, evaluated);
            if (!sequenceDataProvider.TryRead(out var readResult))
            {
                Task.Run(async () =>
                {
                    var rr = await sequenceDataProvider.ReadAsync().ConfigureAwait(false);
                    sequence = rr.Buffer;
                    resultCache = rr.Result;
                }).Wait();
            }
            else
            {
                sequence = readResult.Buffer;
                resultCache = readResult.Result;
            }
        }

        private void RefillAtLeast(SequencePosition read, SequencePosition evaluated, nuint count)
        {
            sequenceDataProvider.AdvanceTo(read, evaluated);
            Task.Run(async () =>
            {
                var rr = await sequenceDataProvider.ReadAtLeastAsync(count).ConfigureAwait(false);
                sequence = rr.Buffer;
                resultCache = rr.Result;
            }).Wait();
        }

        public nuint PeekAtMost(NativeSpan<T> destination, nuint offset = 0U)
        {
            var seq = sequence;
            if (seq.IsEmpty) return 0;
            if (seq.IsSingleSegment)
            {
                var s = (ReadOnlyNativeSpan<T>)seq.FirstSpan;
                return s.CopyAtMostTo(destination, offset);
            }
            return offset > long.MaxValue ? 0 : PeekAtMostMultiSegments(destination, (long)offset, seq);
        }

        private static nuint PeekAtMostMultiSegments(NativeSpan<T> destination, long offset, ReadOnlySequence<T> seq)
        {
            var dst = destination;
            var q = offset > 0 ? seq.GetPosition(offset) : seq.Start;
            while (!dst.IsEmpty && seq.TryGet(ref q, out var m))
            {
                var hs = dst;
                var s = m.Span;
                var res = s.CopyAtMostTo(hs);
                dst = dst.Slice(res);
            }
            return destination.Length - dst.Length;
        }

        public void PeekAtMostTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count = 0U, nuint offset = 0U) where TBufferWriter : INativeBufferWriter<T>
        {
            var s = sequence;
            if (s.IsEmpty) return;
            if (offset > 0)
            {
                if (offset >= (nuint)s.Length) return;
                s = s.Slice((long)offset);
            }
            if (count == 0)
            {
                var span = bufferWriter.GetSpan();
                var res = s.FirstSpan.CopyAtMostTo(span);
                bufferWriter.Advance(res);
                return;
            }
            if (count < (nuint)s.Length)
            {
                s = s.Slice(0, (long)count);
            }
            s.WriteTo(ref bufferWriter);
        }
        public void PeekExact(NativeSpan<T> destination, nuint offset = 0U)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(destination.Length, (ulong)long.MaxValue);
            Prefetch(offset + destination.Length);
            var res = PeekAtMost(destination, offset);
            if (res != destination.Length)
            {
                throw new InvalidOperationException($"The {nameof(sequenceDataProvider)} is not reading sufficient data!");
            }
        }
        public void PeekExactTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count, nuint offset = 0U) where TBufferWriter : INativeBufferWriter<T>
        {
            if (count == 0) return;
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, (ulong)long.MaxValue);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, (ulong)long.MaxValue);
            Prefetch(offset + count);
            sequence.Slice((long)offset, (long)count).WriteTo(ref bufferWriter);
        }
        public void Prefetch(nuint count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, (ulong)long.MaxValue);
            var s = sequence;
            if (s.IsEmpty || (nuint)s.Length < count)
            {
                RefillAtLeast(s.Start, s.End, count);
            }
        }
        public AvailableElementsResult TryGetAvailableElements(out nuint count)
        {
            if (resultCache.HasElements)
            {
                count = (nuint)sequence.Length;
                return AvailableElementsResult.Value;
            }
            count = 0;
            return resultCache;
        }
        public bool TryPeekExact(NativeSpan<T> destination, nuint offset = 0U)
        {
            if (destination.IsEmpty) return true;
            var s = sequence;
            if (offset > 0)
            {
                if (offset >= (nuint)s.Length) return destination.IsEmpty;
                s = s.Slice((long)offset);
            }
            if ((nuint)s.Length < destination.Length) return false;
            var res = PeekAtMostMultiSegments(destination, 0, s);
            return res == destination.Length;
        }

        public nuint ReadAtMost(NativeSpan<T> destination)
        {
            if (destination.IsEmpty) return 0;
            var seq = sequence;
            var writer = DataWriter.CreateFrom(destination);
            writer.WriteAtMost(seq);
            return writer.GetElementsWritten();
        }
        public void ReadExact(NativeSpan<T> destination)
        {

        }
        public void WriteAtMostTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count = 0U) where TBufferWriter : INativeBufferWriter<T>
        {
            var s = sequence;
            if (count == 0)
            {
                if (s.IsEmpty) return;
                var span = bufferWriter.GetSpan();
                var res = s.FirstSpan.CopyAtMostTo(span);
                bufferWriter.Advance(res);
                return;
            }
            using var w = new DataWriter<T, TBufferWriter>(ref bufferWriter, count);
            var end = w.WriteAtMost(s);
            if (s.End.Equals(end))
            {
                Refill(end, end);
            }
            else
            {
                sequence = s.Slice(end);
            }
        }
    }
}
