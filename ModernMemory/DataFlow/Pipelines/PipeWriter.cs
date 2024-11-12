using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.DataFlow.Pipelines
{
    public abstract partial class PipeWriter<T> : INativeBufferWriter<T>
    {
        #region INativeBufferWriter
        public abstract void Advance(nuint count);
        void IBufferWriter<T>.Advance(int count) => Advance((nuint)count);
        public abstract Memory<T> GetMemory(int sizeHint = 0);
        public abstract NativeMemory<T> GetNativeMemory(nuint sizeHint = 0U);
        public abstract NativeSpan<T> GetNativeSpan(nuint sizeHint = 0U);
        public virtual Span<T> GetSpan(int sizeHint = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sizeHint);
            return GetNativeSpan((nuint)sizeHint).GetHeadSpan();
        }
        public abstract bool TryGetMaxBufferSize(out nuint space);
        public abstract NativeMemory<T> TryGetNativeMemory(nuint sizeHint = 0U);
        public abstract NativeSpan<T> TryGetNativeSpan(nuint sizeHint = 0U);
        #endregion

        public abstract ValueTask<FlushResult> FlushAsync(CancellationToken token = default);
        public abstract void CancelPendingFlush();
        public abstract void Complete(Exception? exception = default);

        public virtual bool TryGetUnflushedItems(out nuint unflushedItems)
        {
            unflushedItems = 0;
            return false;
        }
    }
}
