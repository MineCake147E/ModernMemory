using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections.Storage
{
    public struct MemoryResizerCollectionStorage<T> : IResizableCollectionStorage<T>, IStaticCollectionStorageFactory<T, MemoryResizerCollectionStorage<T>>
    {
        internal MemoryResizer<T> resizer;

        public MemoryResizerCollectionStorage()
        {
            resizer = new();
        }

        public MemoryResizerCollectionStorage(MemoryResizer<T> resizer)
        {
            this.resizer = resizer;
        }

        public readonly NativeSpan<T> Span => resizer.Span;
        public readonly NativeMemory<T> Memory => resizer.Memory;

        public static MemoryResizerCollectionStorage<T> Create(nuint size) => new(new(size));
        public static MemoryResizerCollectionStorage<T> Create(ReadOnlyNativeSpan<T> values) => new(new(values));
        public void Dispose()
        {
            resizer.Dispose();
            resizer = default;
        }
        public void Resize(nuint newSize) => resizer.Resize(newSize);
        public void Resize(nuint newSize, ReadOnlyNativeSpan<T> values) => resizer.Resize(newSize, values);
    }
}
