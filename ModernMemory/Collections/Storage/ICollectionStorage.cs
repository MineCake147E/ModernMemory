using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections.Storage
{
    public interface ICollectionStorage<T> : IDisposable
    {
        NativeSpan<T> Span { get; }

        NativeMemory<T> Memory { get; }
        static virtual nuint MaxSize => nuint.MaxValue / (nuint)Unsafe.SizeOf<T>();
    }

    public interface ICollectionStorageFactory<T, TStorage>
        where TStorage : ICollectionStorage<T>
    {
        TStorage Create(nuint size);
    }

    public interface IStaticCollectionStorageFactory<T, TStorage>
        where TStorage : ICollectionStorage<T>
    {
        static abstract TStorage Create(nuint size);

        static abstract TStorage Create(ReadOnlyNativeSpan<T> values);
    }
}
