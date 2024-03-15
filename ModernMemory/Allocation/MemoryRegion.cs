using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Allocation
{
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type

    public readonly unsafe struct MemoryRegion<T, TMemoryAllocator> : IDisposable where TMemoryAllocator : IMemoryAllocator<TMemoryAllocator>
    {
        private readonly T* head;
        private readonly nuint length;

        public void Dispose() => throw new NotImplementedException();
    }
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
}
