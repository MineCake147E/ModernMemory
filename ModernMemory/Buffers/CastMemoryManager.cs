using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    internal class CastMemoryManager<T> : NativeMemoryManager<T>
    {
        private readonly NativeMemoryManager<byte> memoryManager;

        public CastMemoryManager(NativeMemoryManager<byte> memoryManager)
        {
            ArgumentNullException.ThrowIfNull(memoryManager);
            this.memoryManager = memoryManager;
        }

        public override NativeSpan<T> CreateNativeSpan(nuint start, nuint length)
            => NativeMemoryUtils.CastUnsafe<byte, T>(memoryManager.CreateNativeSpan(start * (nuint)Unsafe.SizeOf<T>(), length * (nuint)Unsafe.SizeOf<T>()));
        public override ReadOnlyNativeSpan<T> CreateReadOnlyNativeSpan(nuint start, nuint length)
            => NativeMemoryUtils.CastUnsafe<byte, T>(memoryManager.CreateReadOnlyNativeSpan(start * (nuint)Unsafe.SizeOf<T>(), length * (nuint)Unsafe.SizeOf<T>()));
        public override NativeSpan<T> GetNativeSpan() => NativeMemoryUtils.CastUnsafe<byte, T>(memoryManager.GetNativeSpan());
        public override ReadOnlyMemory<T> GetReadOnlyMemorySegment(nuint start) => throw new NotSupportedException();
        public override MemoryHandle Pin(nuint elementIndex) => memoryManager.Pin(elementIndex * (nuint)Unsafe.SizeOf<T>());
        public override void Unpin() => memoryManager.Unpin();
        protected override void Dispose(bool disposing) => (memoryManager as IDisposable).Dispose();
    }
}
