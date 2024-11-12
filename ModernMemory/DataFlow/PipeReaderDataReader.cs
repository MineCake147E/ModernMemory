using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.DataFlow
{
    public readonly struct PipeReaderDataReader(PipeReader pipeReader) : ISequenceDataReader<byte>
    {
        private PipeReader PipeReader { get; } = pipeReader ?? throw new ArgumentNullException(nameof(pipeReader));

        public void AdvanceTo(SequencePosition consumed, SequencePosition examined) => PipeReader.AdvanceTo(consumed, examined);

        public void CancelPendingRead() => PipeReader.CancelPendingRead();

        public void Complete(Exception? exception = null) => PipeReader.Complete();

        public async ValueTask<GenericReadResult<byte>> ReadAsync(CancellationToken cancellationToken = default) => (await PipeReader.ReadAsync(cancellationToken)).AsGenericReadResult();

        public async ValueTask<GenericReadResult<byte>> ReadAtLeastAsync(nuint minimumSize, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(minimumSize, (nuint)int.MaxValue);
            return (await PipeReader.ReadAtLeastAsync((int)minimumSize, cancellationToken)).AsGenericReadResult();
        }

        public bool TryRead(out GenericReadResult<byte> result)
        {
            var res = PipeReader.TryRead(out var r);
            result = r.AsGenericReadResult();
            return res;
        }
    }
}