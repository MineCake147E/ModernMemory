using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers.Pooling;

namespace ModernMemory.Collections.Storage
{
    public struct MemoryOwnerContainerStorage<T> : ICollectionStorage<T>
    {
        private MemoryOwnerContainer<T> owner;
#pragma warning disable IDE0032 // Use auto property
        private NativeMemory<T> memory;
#pragma warning restore IDE0032 // Use auto property

        public MemoryOwnerContainerStorage(MemoryOwnerContainer<T> owner) : this()
        {
            this.owner = owner;
            memory = owner.NativeMemory;
        }

        public readonly NativeSpan<T> Span => memory.Span;

        public readonly NativeMemory<T> Memory => memory;

        public void Dispose()
        {
            memory = default;
            owner.Dispose();
        }
    }
}
