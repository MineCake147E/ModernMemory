using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers.Pooling
{
    internal abstract partial class SharedNativeMemoryPoolBase<T, TSelf> : NativeMemoryPool<T>
        where TSelf : SharedNativeMemoryPoolBase<T, TSelf>
    {
        [ThreadStatic]
        private static PerThreadStates states;


        public sealed override MemoryOwnerContainer<T> RentWithDefaultSize() => throw new NotImplementedException();
        public sealed override MemoryOwnerContainer<T> Rent(nuint minBufferSize) => throw new NotImplementedException();

        internal abstract INativeMemoryOwner<T> CreateWithDefaultSizeInternal();

        internal abstract INativeMemoryOwner<T> CreateInternal(nuint minBufferSize);

        protected struct PerThreadStates
        {

        }
        protected override void Dispose(bool disposing) { }
    }
}
