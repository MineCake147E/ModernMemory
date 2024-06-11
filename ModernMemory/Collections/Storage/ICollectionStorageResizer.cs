using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections.Storage
{
    public interface ICollectionStorageResizer<T, TStorage> : ICollectionStorageFactory<T, TStorage>
        where TStorage : ICollectionStorage<T>
    {
        static abstract void Resize(ref TStorage destination, nuint newSize);
    }

    public interface IResizableCollectionStorage<T> : ICollectionStorage<T>
    {
        void Resize(nuint newSize);

        void Resize(nuint newSize, ReadOnlyNativeSpan<T> values);
    }
}
