using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.DataFlow.Pipelines
{
    public abstract class PipeReader<T>
    {
        public abstract void AdvanceTo(SlimSequencePosition consumed, SlimSequencePosition examined);
        public abstract void CancelPendingRead();
        public abstract void Complete(Exception? exception = null);
        public abstract ValueTask<MemorySequenceReadResult<T>> ReadAsync(CancellationToken cancellationToken = default);
        public abstract ValueTask<MemorySequenceReadResult<T>> ReadAtLeastAsync(nuint minimumSize, CancellationToken cancellationToken = default);
        public abstract bool TryRead(out MemorySequenceReadResult<T> result);
        public virtual ValueTask CompleteAsync(Exception? exception = null)
        {
            try
            {
                Complete(exception);
                return ValueTask.CompletedTask;
            }
            catch (Exception e)
            {
                return ValueTask.FromException(e);
            }
        }
    }
}
