using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    public interface INativeMemoryOwner<T> : IMemoryOwner<T>
    {
        /// <inheritdoc cref="IMemoryOwner{T}.Memory"/>
        NativeMemory<T> NativeMemory { get; }

        NativeSpan<T> Span => NativeMemory.Span;
    }

    internal sealed class MemoryOwnerWrapper<T>(IMemoryOwner<T> owner) : INativeMemoryOwner<T>
    {
        private readonly IMemoryOwner<T> owner = owner ?? throw new ArgumentNullException(nameof(owner));

        public NativeMemory<T> NativeMemory => owner.Memory;
        public Memory<T> Memory => owner.Memory;

        public NativeSpan<T> Span => owner.Memory.Span;

        public void Dispose() => owner.Dispose();
    }
}
