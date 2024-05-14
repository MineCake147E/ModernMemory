using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers.Pooling
{
    public struct MemoryOwnerContainer<T> : INativeMemoryOwner<T>
    {
        private INativeMemoryOwner<T>? owner;

        public MemoryOwnerContainer(INativeMemoryOwner<T>? owner)
        {
            this.owner = owner;
        }

        public readonly bool IsOwnerNull => owner is null;

        public readonly bool HasOwner => owner is not null;

        public readonly NativeMemory<T> NativeMemory => owner?.NativeMemory ?? default;
        public readonly Memory<T> Memory => owner?.Memory ?? default;

        public readonly NativeSpan<T> Span => owner is not null ? owner.Span : default;

        void IDisposable.Dispose() => DisposeInternal();

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal void DisposeInternal()
        {
            var oldOwner = Interlocked.Exchange(ref owner, null);
            oldOwner?.Dispose();
        }
    }

    public static class MemoryOwnerContainer
    {
#pragma warning disable S2953 // Methods named "Dispose" should implement "IDisposable.Dispose"
        public static void Dispose<T>(this ref MemoryOwnerContainer<T> container) => container.DisposeInternal();
#pragma warning restore S2953 // Methods named "Dispose" should implement "IDisposable.Dispose"
    }
}
