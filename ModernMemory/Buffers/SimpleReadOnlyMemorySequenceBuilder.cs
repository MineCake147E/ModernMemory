using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Threading;

namespace ModernMemory.Buffers
{
    public sealed class SimpleReadOnlyMemorySequenceBuilder<T> : IReadOnlyMemorySequenceBuilder<T>
    {
        private uint disposedValue = AtomicUtils.GetValue(false);

        public nuint CurrentLength { get; }

        public void AdvanceTo(SlimSequencePosition consumed) => throw new NotImplementedException();
        public nuint AdvanceTo(SlimSequencePosition consumed, SlimSequencePosition examined) => throw new NotImplementedException();
        public nuint Append(ReadOnlyNativeMemory<T> memory) => throw new NotImplementedException();
        public nuint Append(ReadOnlyNativeSpan<ReadOnlyNativeMemory<T>> memories) => throw new NotImplementedException();
        public ReadOnlyMemorySequence<T> Build() => throw new NotImplementedException();
        public ReadOnlySequenceSlim<T> BuildSlim() => throw new NotImplementedException();
        public void Clear() => throw new NotImplementedException();

        private void Dispose(bool disposing)
        {
            if (!AtomicUtils.Exchange(ref disposedValue, true))
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
