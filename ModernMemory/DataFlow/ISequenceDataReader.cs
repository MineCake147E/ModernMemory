namespace ModernMemory.DataFlow
{
    public interface ISequenceDataReader<T>
    {
        void AdvanceTo(SequencePosition consumed, SequencePosition examined);
        void AdvanceTo(SequencePosition consumed) => AdvanceTo(consumed, consumed);
        ValueTask<GenericReadResult<T>> ReadAsync(CancellationToken cancellationToken = default);
        ValueTask<GenericReadResult<T>> ReadAtLeastAsync(nuint minimumSize, CancellationToken cancellationToken = default);
        bool TryRead(out GenericReadResult<T> result);
        void CancelPendingRead();
        void Complete(Exception? exception = null);
        ValueTask CompleteAsync(Exception? exception = null)
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