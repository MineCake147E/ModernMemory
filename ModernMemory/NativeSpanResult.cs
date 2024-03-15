using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly ref struct NativeSpanResult<T, TResultSummary>
    {
        public NativeSpan<T> NativeSpan { get; }
        public TResultSummary ResultSummary { get; }

        public NativeSpanResult(NativeSpan<T> nativeSpan, TResultSummary result)
        {
            NativeSpan = nativeSpan;
            ResultSummary = result;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct NativeMemoryResult<T, TResultSummary>
    {
        public NativeMemory<T> NativeMemory { get; }
        public TResultSummary ResultSummary { get; }

        public NativeMemoryResult(NativeMemory<T> nativeMemory, TResultSummary result)
        {
            NativeMemory = nativeMemory;
            ResultSummary = result;
        }
    }
}
