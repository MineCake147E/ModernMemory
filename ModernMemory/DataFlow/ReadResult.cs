using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.DataFlow
{
    public readonly record struct ReadResult<T>(ReadOnlySequence<T> Buffer, AvailableElementsResult Result);

    public static class ReadResultUtils
    {
        public static ReadResult<byte> AsReadResult(this ReadResult readResult)
        {
            if (readResult.IsCanceled) return new(readResult.Buffer, AvailableElementsResult.Canceled);
            if (readResult.IsCompleted && readResult.Buffer.IsEmpty)
            {
                // Completion shouldn't be propagated as EmptyReason
                return new(readResult.Buffer, AvailableElementsResult.StreamComplete);
            }
            return new(readResult.Buffer, AvailableElementsResult.Value);
        }
    }
}
