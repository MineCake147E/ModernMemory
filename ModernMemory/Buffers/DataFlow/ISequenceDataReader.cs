
namespace ModernMemory.Buffers.DataFlow
{
    public interface ISequenceDataReader<T>
    {
        void AdvanceTo(SequencePosition consumed, SequencePosition examined);
        void AdvanceTo(SequencePosition consumed) => AdvanceTo(consumed, consumed);
        ValueTask<ReadResult<T>> ReadAsync(CancellationToken cancellationToken = default);
        ValueTask<ReadResult<T>> ReadAtLeastAsync(nuint minimumSize, CancellationToken cancellationToken = default);
        bool TryRead(out ReadResult<T> result);
        void CancelPendingRead();
        void Complete(Exception? exception = null);
        ValueTask CompleteAsync(Exception? exception = null)
        {
            try
            {
                Complete(exception);
                return default;
            }
            catch (Exception e)
            {
                return new(Task.FromException(e));
            }
        }
    }
}