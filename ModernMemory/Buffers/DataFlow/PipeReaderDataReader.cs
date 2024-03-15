using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers.DataFlow
{
    public readonly struct PipeReaderDataReader(PipeReader pipeReader) : ISequenceDataReader<byte>
    {
        private PipeReader PipeReader { get; } = pipeReader ?? throw new ArgumentNullException(nameof(pipeReader));

        public void AdvanceTo(SequencePosition consumed, SequencePosition examined) => PipeReader.AdvanceTo(consumed, examined);

        public void CancelPendingRead() => PipeReader.CancelPendingRead();

        public void Complete(Exception? exception = null) => PipeReader.Complete();

        public async ValueTask<ReadResult<byte>> ReadAsync(CancellationToken cancellationToken = default) => (await PipeReader.ReadAsync(cancellationToken)).AsReadResult();

        public async ValueTask<ReadResult<byte>> ReadAtLeastAsync(nuint minimumSize, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(minimumSize, (nuint)int.MaxValue);
            return (await PipeReader.ReadAtLeastAsync((int)minimumSize, cancellationToken)).AsReadResult();
        }

        public bool TryRead(out ReadResult<byte> result)
        {
            var res = PipeReader.TryRead(out var r);
            result = r.AsReadResult();
            return res;
        }
    }
}