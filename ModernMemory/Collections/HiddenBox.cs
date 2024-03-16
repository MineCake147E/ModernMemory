using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
    [InlineArray(1)]
    public struct HiddenBox<T>(T value)
    {
        internal T value = value;
        public readonly int Length => 1;
    }

    public static class HiddenBox
    {
        public static ReadOnlyNativeSpan<T> AsBoxSpan<T>(this ref HiddenBox<T> box) => (ReadOnlyNativeSpan<T>)(ReadOnlySpan<T>)box;
    }
}
