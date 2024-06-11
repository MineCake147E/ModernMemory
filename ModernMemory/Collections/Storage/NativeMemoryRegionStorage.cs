using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Allocation;

namespace ModernMemory.Collections.Storage
{
    public struct NativeMemoryRegionStorage<T> : IResizableCollectionStorage<T>, IStaticCollectionStorageFactory<T, NativeMemoryRegionStorage<T>>
    {
        private MemoryRegion<T> regionCache;
        private NativeMemoryRegionOwner<T>? owner;

        public NativeMemoryRegionStorage(NativeMemoryRegionOwner<T> owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            this.owner = owner;
            regionCache = owner.Region;
        }

        public NativeMemoryRegionStorage(nuint size, byte alignmentExponent = 0, bool clear = false) : this(new(size, alignmentExponent, clear))
        {
        }

        public readonly NativeSpan<T> Span => regionCache.NativeSpan;
        public readonly NativeMemory<T> Memory => owner?.NativeMemory ?? default;

        public static NativeMemoryRegionStorage<T> Create(nuint size) => new(new(size));

        public static NativeMemoryRegionStorage<T> Create(ReadOnlyNativeSpan<T> values)
        {
            var newStorage = new NativeMemoryRegionStorage<T>(new(values.Length));
            values.CopyTo(newStorage.Span);
            return newStorage;
        }

        public void Resize(nuint newSize)
        {
            var span = Span;
            if (span.Length >= newSize) return;
            Resize(newSize, span);
        }

        public void Resize(nuint newSize, ReadOnlyNativeSpan<T> values)
        {
            var oldOwner = owner;
            var newOwner = oldOwner;
            NativeSpan<T> span = default;
            if (oldOwner is not null)
            {
                span = oldOwner.Span;
            }
            if (oldOwner is null || span.Length < newSize)
            {
                newOwner = new NativeMemoryRegionOwner<T>(newSize);
                span = newOwner.Span;
                owner = newOwner;
                regionCache = newOwner.Region;
            }
            values.CopyTo(span);
            if (oldOwner != newOwner)
            {
                oldOwner?.Dispose();
            }
        }

        public void Dispose()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                Span.Clear();
            }
            regionCache = default;
            owner?.Dispose();
            owner = null;
        }
    }
}
