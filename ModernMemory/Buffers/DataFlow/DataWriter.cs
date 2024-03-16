using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers.DataFlow
{
    public ref struct DataWriter<T, TBufferWriter> where TBufferWriter : IBufferWriter<T>
    {
        private ref TBufferWriter writer;
        private NativeSpan<T> buffer;
        private nuint elementsInBuffer;
        private nuint elementsToWrite;
        private readonly byte flags;

        private readonly bool HasBufferWriter
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => !(default(TBufferWriter) is DataWriter.DummyBufferWriter<T> || Unsafe.IsNullRef(ref writer));
        }

        public readonly bool IsLengthConstrained => flags == 0;

        public readonly bool IsCompleted => elementsToWrite == 0;

        public DataWriter(ref TBufferWriter bufferWriter)
        {
            ArgumentNullException.ThrowIfNull(bufferWriter);
            writer = ref bufferWriter;
            flags = 1;
            elementsToWrite = nuint.MaxValue;
        }

        public DataWriter(ref TBufferWriter bufferWriter, nuint elementsToWrite)
        {
            ArgumentNullException.ThrowIfNull(bufferWriter);
            writer = ref bufferWriter;
            flags = 0;
            this.elementsToWrite = elementsToWrite;
        }

        internal DataWriter(NativeSpan<T> span)
        {
            Debug.Assert(default(TBufferWriter) is DataWriter.DummyBufferWriter<T>);
            writer = ref Unsafe.NullRef<TBufferWriter>();
            buffer = span;
            elementsToWrite = span.Length;
        }

        internal readonly bool TryGetElementsWritten(out nuint elements)
        {
            var hasBufferWriter = HasBufferWriter;
            elements = hasBufferWriter ? 0 : elementsInBuffer;
            return !hasBufferWriter;
        }

        public readonly bool TryGetMaxBufferSize(out nuint space)
        {
            if (HasBufferWriter)
            {
                var sp = elementsToWrite;
                var v = buffer.Length < sp;
                sp = v ? 0 : sp;
                space = sp;
                return v;
            }
            space = buffer.Length;
            return true;
        }

        /// <summary>
        /// Returns a <see cref="NativeSpan{T}"/> to write to that is at least the requested size (specified by <paramref name="sizeHint"/>), or the maximum size that this <see cref="INativeBufferWriter{T}"/> can offer, whichever smaller.<br/>
        /// </summary>
        /// <param name="sizeHint">The desired length of the returned <see cref="NativeSpan{T}"/>.</param>
        /// <remarks>
        /// This can return an empty <see cref="NativeSpan{T}"/> but it can not throw
        /// if no buffer is available.<br/>
        /// There is no guarantee that successive calls will return the same buffer or the same-sized buffer.<br/>
        /// You must request a new buffer after calling <see cref="Advance(nuint)"/> to continue writing more data and cannot write to a previously acquired buffer.
        /// </remarks>
        /// <returns>The <see cref="NativeSpan{T}"/> for the buffer.</returns>
        public NativeSpan<T> TryGetNativeSpan(nuint sizeHint = 0)
        {
            var res = buffer;
            if (HasBufferWriter)
            {
                var isLengthConstrained = IsLengthConstrained;
                var maxSize = elementsToWrite;
                if (isLengthConstrained)
                {
                    sizeHint = nuint.Min(sizeHint, maxSize);
                }
                if (res.IsEmpty || sizeHint > res.Length)
                {
                    if (elementsInBuffer > 0) FlushCore();
                    res = writer.GetSpan((int)nuint.Min(int.MaxValue, sizeHint));
                    if (isLengthConstrained && res.Length > maxSize)
                    {
                        res = res.Slice(0, maxSize);
                    }
                    buffer = res;
                }
            }
            return res;
        }

        public void Flush()
        {
            if (HasBufferWriter)
            {
                FlushCore();
                buffer = default;
            }
        }

        private void FlushCore()
        {
            var e = elementsInBuffer;
            if (!Unsafe.IsNullRef(ref writer) && e > 0)
            {
                writer.Advance((int)e);
                elementsInBuffer = 0;
            }
        }

        /// <summary>
        /// Notifies <see cref="DataWriter{T, TBufferWriter}"/> that <paramref name="count"/> amount of data was written to the output <see cref="NativeSpan{T}"/>/<see cref="NativeMemory{T}"/>
        /// </summary>
        /// <param name="count">The number of data items written to the <see cref="NativeSpan{T}"/> or <see cref="NativeMemory{T}"/>.</param>
        /// <remarks>You must request a new buffer after calling <see cref="Advance(nuint)"/> to continue writing more data and cannot write to a previously acquired buffer.</remarks>
        public void Advance(nuint count)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length);
            elementsInBuffer += count;
            buffer = buffer.Slice(count);
            if (IsLengthConstrained)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(count, elementsToWrite);
                elementsToWrite -= count;
            }
        }

        public int WriteAtMost(ReadOnlySpan<T> values) => (int)WriteAtMost(new ReadOnlyNativeSpan<T>(values));
        public nuint WriteAtMost(ReadOnlyNativeSpan<T> values)
        {
            if (values.IsEmpty || IsCompleted) return 0;
            var span = TryGetNativeSpan();
            if (values.TryCopyTo(span))
            {
                Advance(values.Length);
                return values.Length;
            }
            var vs = values;
            while (!vs.IsEmpty && !IsCompleted)
            {
                span = TryGetNativeSpan(vs.Length);
                Debug.Assert(!span.IsEmpty);
                var k = vs.CopyAtMostTo(span);
                Advance(k);
                vs = vs.Slice(k);
            }
            return values.Length - vs.Length;
        }

        public SlimSequencePosition WriteAtMost(ReadOnlySequenceSlim<T> sequence, nuint offset = 0)
        {
            var pos = sequence.Start;
            if (offset > 0)
            {
                pos = sequence.GetPosition(offset);
            }
            if (IsCompleted) return pos;
            var cp = pos;
            while (sequence.TryGet(pos, out var m, out pos))
            {
                var s = m.Span;
                var w = WriteAtMost(s);
                if (IsCompleted)
                {
                    return sequence.GetPosition(w, cp);
                }
                Debug.Assert(s.Length == w);
                cp = pos;
            }
            return sequence.End;
        }

        public SequencePosition WriteAtMost(ReadOnlySequence<T> sequence, long offset = 0)
        {
            var pos = sequence.Start;
            if (offset > 0)
            {
                pos = sequence.GetPosition(offset);
            }
            if (IsCompleted) return pos;
            var cp = pos;
            while (sequence.TryGet(ref pos, out var m))
            {
                var s = m.Span;
                var w = WriteAtMost(s);
                if (IsCompleted)
                {
                    return sequence.GetPosition(w, cp);
                }
                Debug.Assert(s.Length == w);
                cp = pos;
            }
            return sequence.End;
        }

        public void Dispose()
        {
            Flush();
            writer = ref Unsafe.NullRef<TBufferWriter>();
            elementsToWrite = 0;
        }
    }

    public static class DataWriter
    {
        public static DataWriter<T, DummyBufferWriter<T>> CreateFrom<T>(NativeSpan<T> span) => new(span);

        public static DataWriter<T, DummyBufferWriter<T>> CreateFrom<T>(Span<T> span) => new(span);

        public static DataWriter<T, ArrayBufferWriter<T>> CreateFrom<T>(ref ArrayBufferWriter<T> writer)
            => new(ref writer);
        public static DataWriter<T, ArrayBufferWriter<T>> CreateFrom<T>(ref ArrayBufferWriter<T> writer, nuint elementsToWrite)
            => new(ref writer, elementsToWrite);

        public static DataWriter<byte, PipeWriter> CreateFrom(ref PipeWriter writer)
            => new(ref writer);

        public static DataWriter<byte, PipeWriter> CreateFrom(ref PipeWriter writer, nuint elementsToWrite)
            => new(ref writer, elementsToWrite);

        public static DataWriter<T, TBufferWriter> AsDataWriter<T, TBufferWriter>(this ref TBufferWriter writer) where TBufferWriter : struct, IBufferWriter<T>
            => new(ref writer);

        public static DataWriter<T, TBufferWriter> AsDataWriter<T, TBufferWriter>(this ref TBufferWriter writer, nuint elementsToWrite) where TBufferWriter : struct, IBufferWriter<T>
            => new(ref writer, elementsToWrite);

        public static nuint GetElementsWritten<T>(this in DataWriter<T, DummyBufferWriter<T>> dataWriter)
        {
            var res = dataWriter.TryGetElementsWritten(out var elements);
            Debug.Assert(res);
            return elements;
        }

        public readonly struct DummyBufferWriter<T> : INativeBufferWriter<T>
        {
#pragma warning disable S1133 // Deprecated code should be removed
            [Obsolete("DO NOT CALL IT!", true)]
#pragma warning restore S1133 // Deprecated code should be removed
            public DummyBufferWriter()
            {
            }
            void INativeBufferWriter<T>.Advance(nuint count) { }
            void IBufferWriter<T>.Advance(int count) { }
            Memory<T> IBufferWriter<T>.GetMemory(int sizeHint) => default;
            NativeMemory<T> INativeBufferWriter<T>.GetNativeMemory(nuint sizeHint) => default;
            NativeSpan<T> INativeBufferWriter<T>.GetNativeSpan(nuint sizeHint) => default;
            Span<T> IBufferWriter<T>.GetSpan(int sizeHint) => default;
            bool INativeBufferWriter<T>.TryGetMaxBufferSize(out nuint space)
            {
                Unsafe.SkipInit(out space);
                return false;
            }
            NativeMemory<T> INativeBufferWriter<T>.TryGetNativeMemory(nuint sizeHint) => default;
            NativeSpan<T> INativeBufferWriter<T>.TryGetNativeSpan(nuint sizeHint) => default;
        }
    }

    public static class DataWriter<T>
    {
        public static DataWriter<T, TBufferWriter> CreateFrom<TBufferWriter>(ref TBufferWriter writer) where TBufferWriter : IBufferWriter<T>
            => new(ref writer);

        public static DataWriter<T, TBufferWriter> CreateFrom<TBufferWriter>(ref TBufferWriter writer, nuint elementsToWrite) where TBufferWriter : IBufferWriter<T>
            => new(ref writer, elementsToWrite);
    }
}
