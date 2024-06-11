using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections.Storage
{
    public struct ArrayStorage<T> : IResizableCollectionStorage<T>, IStaticCollectionStorageFactory<T, ArrayStorage<T>>
    {
        T[]? array;

        public ArrayStorage(T[] array)
        {
            this.array = array ?? throw new ArgumentNullException(nameof(array));
        }

        public ArrayStorage(int size)
        {
            array = new T[size];
        }

        public readonly NativeSpan<T> Span => array.AsNativeSpan();

        public static nuint MaxSize => (nuint)Array.MaxLength;

        public readonly NativeMemory<T> Memory => array.AsNativeMemory();

        public static ArrayStorage<T> Create(nuint size)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(size, MaxSize);
            return new(new T[(int)size]);
        }

        public static ArrayStorage<T> Create(ReadOnlyNativeSpan<T> values)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(values.Length, MaxSize);
            return new(values.ToArray());
        }

        public void Resize(nuint newSize)
        {
            var localArray = array;
            if (localArray is not null && (uint)localArray.Length <= newSize) return;
            var newArray = new T[(int)newSize];
            array = newArray;
            localArray.AsNativeSpan().CopyAtMostTo(newArray.AsNativeSpan());
        }

        public void Resize(nuint newSize, ReadOnlyNativeSpan<T> values)
        {
            var localArray = array;
            if (localArray is null || (uint)localArray.Length > newSize)
            {
                localArray = new T[(int)newSize];
                array = localArray;
            }
            values.CopyTo(localArray);
        }

        public void Dispose() => array = null;
    }
}
