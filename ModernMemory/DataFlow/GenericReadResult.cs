using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.DataFlow
{
    public readonly record struct GenericReadResult<T>(ReadOnlySequence<T> Buffer, AvailableElementsResult Result);

    public readonly record struct MemorySequenceReadResult<T>(ReadOnlyMemorySequence<T> Buffer, AvailableElementsResult Result);
    public static class ReadResultUtils
    {
        public static GenericReadResult<byte> AsGenericReadResult(this ReadResult readResult)
        {
            var buffer = readResult.Buffer;
            var result = AvailableElementsResult.Value;
            if (readResult.IsCompleted && buffer.IsEmpty)
            {
                // Completion shouldn't be propagated as EmptyReason
                result = AvailableElementsResult.StreamComplete;
            }
            if (readResult.IsCanceled) result = AvailableElementsResult.Canceled;
            return new(buffer, result);
        }
    }
}
