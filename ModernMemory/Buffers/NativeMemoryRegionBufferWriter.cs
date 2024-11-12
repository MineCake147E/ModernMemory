using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Collections.Storage;
using ModernMemory.DataFlow;

namespace ModernMemory.Buffers
{
    public partial class NativeMemoryRegionBufferWriter<T> : INativeBufferWriter<T>, IDisposable
    {
        private NativeMemoryRegionStorage<T> storage;
        private NativeMemory<T> memory;
        private uint disposedValue;
        private nuint writtenCount = 0;

        public NativeMemoryRegionBufferWriter(nuint initialCapacity = 0)
        {
            storage = new(initialCapacity, clear: true);
            memory = storage.Memory;
        }

        public nuint WrittenCount => writtenCount;

        public ReadOnlyNativeSpan<T> WrittenSpan => storage.Span.Slice(0, writtenCount);

        public ReadOnlyNativeMemory<T> WrittenMemory => storage.Memory.Slice(0, writtenCount);
        public void Clear()
        {
            var wc = writtenCount;
            writtenCount = 0;
            storage.Span.Slice(0, wc).ClearIfReferenceOrContainsReferences();
        }
        public void EnsureCapacity(nuint itemsToAdd) => Resize(checked(writtenCount + itemsToAdd));
        private void Resize(nuint minSize)
        {
            ObjectDisposedException.ThrowIf(disposedValue > 0, this);
            if (storage.Length >= minSize) return;
            var newSize = minSize > unchecked((nuint)nint.MinValue) ? minSize : BitOperations.RoundUpToPowerOf2(minSize);
            storage.Resize(newSize);
            memory = storage.Memory;
        }
        public void Advance(nuint count)
        {
            ObjectDisposedException.ThrowIf(disposedValue > 0, this);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, storage.Length - writtenCount);
            writtenCount += count;
        }
        public Memory<T> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(checked((nuint)sizeHint));
            return memory.Slice(writtenCount).GetHeadMemory();
        }
        public NativeMemory<T> GetNativeMemory(nuint sizeHint = 0U)
        {
            EnsureCapacity(sizeHint);
            return memory.Slice(writtenCount);
        }
        public NativeSpan<T> GetNativeSpan(nuint sizeHint = 0U)
        {
            EnsureCapacity(sizeHint);
            return storage.Span.Slice(writtenCount);
        }
        public Span<T> GetSpan(int sizeHint = 0) => GetNativeSpan(checked((nuint)sizeHint)).GetHeadSpan();
        public bool TryGetMaxBufferSize(out nuint space)
        {
            space = nuint.MaxValue;
            return disposedValue == 0;
        }

        public bool TryGetSuitableBufferSize(out nuint space)
        {
            space = storage.Length - writtenCount;
            return disposedValue == 0;
        }
        public NativeMemory<T> TryGetNativeMemory(nuint sizeHint = 0U) => GetNativeMemory(sizeHint);
        public NativeSpan<T> TryGetNativeSpan(nuint sizeHint = 0U) => GetNativeSpan(sizeHint);
    }

    public partial class NativeMemoryRegionBufferWriter<T>
    {
        protected virtual void Dispose(bool disposing)
        {
            if (!AtomicUtils.Exchange(ref disposedValue, true))
            {
                Volatile.Write(ref writtenCount, 0);
                memory = default;
                if (disposing)
                {
                    storage.Dispose();
                }
                storage = default;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
