using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Collections;

namespace ModernMemory
{
    [StructLayout(LayoutKind.Auto)]
    public readonly partial struct ReadOnlyNativeMemory<T> : ISpanEnumerable<T>, IMemoryEnumerable<T>, IEquatable<ReadOnlyNativeMemory<T>>
    {
        internal readonly object? underlyingObject;
        private readonly nuint start;
#pragma warning disable IDE0032 // Use auto property
        private readonly nuint length;
#pragma warning restore IDE0032 // Use auto property
        internal readonly MemoryType type;

        /// <summary>
        /// Gets the number of items in the current instance.
        /// </summary>
        /// <internalValue>The number of items in the current instance.</internalValue>
        public nuint Length => length;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyNativeMemory(MemoryType type, object? underlyingObject, nuint start, nuint length) : this()
        {
            this.type = type;
            this.underlyingObject = underlyingObject;
            this.start = start;
            this.length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ReadOnlyNativeMemory(NativeMemoryManager<T>? nativeSpanFactory, nuint start, nuint length)
        {
            Unsafe.SkipInit(out this);
            var newType = MemoryType.NativeMemoryManager;
            var newUnderlyingObject = nativeSpanFactory;
            var newStart = start;
            var newLength = length;
            if (newLength == 0 || nativeSpanFactory is null)
            {
                newUnderlyingObject = null;
                newStart = 0;
                newLength = 0;
            }
            this = new(newType, newUnderlyingObject, newStart, newLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyNativeMemory(ReadOnlyMemory<T> memory)
        {
            Unsafe.SkipInit(out this);
            var newType = MemoryType.MemoryManager;
            object? newUnderlyingObject = null;
            nuint newStart = 0;
            nuint newLength = 0;
            if (!memory.IsEmpty)
            {
                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() == RuntimeHelpers.IsReferenceOrContainsReferences<char>() && typeof(T) == typeof(char)
                    && MemoryMarshal.TryGetString(Unsafe.As<ReadOnlyMemory<T>, ReadOnlyMemory<char>>(ref memory), out var nst, out var sStart, out var sLength))
                {
                    newUnderlyingObject = nst;
                    newType = MemoryType.String;
                    newStart = (uint)sStart;
                    newLength = (uint)sLength;
                }
                else if (MemoryMarshal.TryGetMemoryManager<T, MemoryManager<T>>(memory, out var manager, out var intStart, out var intLength))
                {
                    newUnderlyingObject = manager;
                    newType = manager is NativeMemoryManager<T> ? MemoryType.NativeMemoryManager : newType;
                    newStart = (uint)intStart;
                    newLength = (uint)intLength;
                }
                else if (MemoryMarshal.TryGetArray(memory, out var segment))
                {
                    newUnderlyingObject = segment.Array;
                    newType = MemoryType.Array;
                    newStart = (uint)segment.Offset;
                    newLength = (uint)segment.Count;
                }
            }
            this = new(newType, newUnderlyingObject, newStart, newLength);
        }

        /// <summary>
        /// Returns an empty <see cref="ReadOnlyNativeMemory{T}"/> object.
        /// </summary>
        public static ReadOnlyNativeMemory<T> Empty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#pragma warning disable S1168 // Empty arrays and collections should be returned instead of null
            get => default;
#pragma warning restore S1168 // Empty arrays and collections should be returned instead of null
        }

        /// <returns><see langword="true"/> if the read-only rom region is empty (that is, its <see cref="Length"/> is 0); otherwise, <see langword="false"/>.</returns>
        /// <inheritdoc cref="ReadOnlyMemory{T}.IsEmpty"/>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => length == 0;
        }

        /// <summary>
        /// Gets a span from the rom region.
        /// </summary>
        public ReadOnlyNativeSpan<T> Span
        {
            [SkipLocalsInit]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var t = type;
                ref var head = ref Unsafe.NullRef<T>();
                var newLength = length;
                var medium = underlyingObject;
                var localStart = start;
                var lengthToValidate = nuint.MaxValue;
                newLength = medium is null ? 0 : newLength;
                if (medium is not null)
                {
                    switch (t)
                    {
                        case MemoryType.String when RuntimeHelpers.IsReferenceOrContainsReferences<T>() == RuntimeHelpers.IsReferenceOrContainsReferences<char>() && typeof(T) == typeof(char):
                            Debug.Assert(medium is string);
                            var ust = Unsafe.As<string>(medium);
                            head = ref Unsafe.As<char, T>(ref Unsafe.Add(ref Unsafe.AsRef(in ust.GetPinnableReference()), checked((int)localStart)))!;
                            lengthToValidate = (uint)ust.Length;
                            break;
                        case MemoryType.Array:
                            Debug.Assert(medium is T[]);
                            var array = Unsafe.As<T[]>(medium);
                            head = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), localStart)!;
                            lengthToValidate = (uint)array.Length;
                            break;
                        case MemoryType.NativeMemoryManager:
                            Debug.Assert(medium is NativeMemoryManager<T>);
                            var nativeMemoryManager = Unsafe.As<NativeMemoryManager<T>>(medium);
                            var nativeSpan = nativeMemoryManager.CreateNativeSpan(localStart, newLength);
                            head = ref nativeSpan.Head!;
                            break;
                        default:
                            Debug.Assert(medium is MemoryManager<T>);
                            var manager = Unsafe.As<MemoryManager<T>>(medium);
                            var span = manager.GetSpan();
                            head = ref span[checked((int)localStart)]!;
                            lengthToValidate = (uint)span.Length;
                            break;
                    }
                }
                if (!MathUtils.IsRangeInRange(lengthToValidate, localStart, newLength))
                {
                    NativeMemoryCore.ThrowSliceExceptions(localStart, newLength, lengthToValidate);
                }
                return new ReadOnlyNativeSpan<T>(ref head, newLength);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe MemoryHandle Pin() => NativeMemoryCore.Pin<T>(type, underlyingObject, start, length);

        public ReadOnlyMemory<T> GetHeadMemory()
        {
            ReadOnlyMemory<T> result = default;
            var t = type;
            var newLength = (int)nuint.Min(int.MaxValue, length);
            var medium = underlyingObject;
            var newStart = checked((int)start);
            newLength = medium is null ? 0 : newLength;
            if (medium is not null)
            {
                switch (t)
                {
                    case MemoryType.String when RuntimeHelpers.IsReferenceOrContainsReferences<T>() == RuntimeHelpers.IsReferenceOrContainsReferences<char>() && typeof(T) == typeof(char):
                        Debug.Assert(medium is string);
                        var ust = Unsafe.As<string>(medium);
                        var cm = ust.AsMemory().Slice(newStart, newLength);
                        result = Unsafe.As<ReadOnlyMemory<char>, ReadOnlyMemory<T>>(ref cm);
                        break;
                    case MemoryType.Array:
                        Debug.Assert(medium is T[]);
                        var array = Unsafe.As<T[]>(medium);
                        result = array.AsMemory(newStart, newLength);
                        break;
                    default:
                        Debug.Assert(medium is MemoryManager<T>);
                        var manager = Unsafe.As<MemoryManager<T>>(medium);
                        result = manager.Memory.Slice(newStart, newLength);
                        break;
                }
            }
            return result;
        }

        #region Slice

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyNativeMemory<T> Slice(nuint start)
        {
            var currentLength = Length;
            var ol = currentLength - start;
            ReadOnlyNativeMemory<T> result = new(type, underlyingObject, this.start + start, ol);
            return ol <= currentLength ? result : ThrowSliceExceptions(start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlyNativeMemory<T> Slice(nuint start, nuint length)
        {
            var result = new ReadOnlyNativeMemory<T>(type, underlyingObject, this.start + start, length);
            return MathUtils.IsRangeInRange(Length, start, length)
                ? result
                : ThrowSliceExceptions(start, length);
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private ReadOnlyNativeMemory<T> ThrowSliceExceptions(nuint start)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
            throw new InvalidOperationException("Something went wrong!");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private ReadOnlyNativeMemory<T> ThrowSliceExceptions(nuint start, nuint length)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, Length - start);
            throw new InvalidOperationException("Something went wrong!");
        }
        #endregion

        #region Copy
        /// <summary>
        /// Attempts to copy the current <see cref="ReadOnlyNativeMemory{T}"/> to a destination <see cref="NativeSpan{T}"/> and returns a internalValue that indicates whether the copy operation succeeded.
        /// </summary>
        /// <inheritdoc cref="Span{T}.TryCopyTo(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCopyTo(NativeSpan<T> destination) => Span.TryCopyTo(destination);

        /// <summary>
        /// Attempts to copy the current <see cref="ReadOnlyNativeMemory{T}"/> to a destination <see cref="NativeMemory{T}"/> and returns a internalValue that indicates whether the copy operation succeeded.
        /// </summary>
        /// <inheritdoc cref="Span{T}.TryCopyTo(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCopyTo(NativeMemory<T> destination) => Span.TryCopyTo(destination.Span);

        /// <summary>
        /// Copies the content of this <see cref="ReadOnlyNativeMemory{T}"/> into a <paramref name="destination"/> <see cref="NativeSpan{T}"/>.
        /// </summary>
        /// <param name="destination">The destination <see cref="NativeSpan{T}"/> object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(NativeSpan<T> destination) => Span.CopyTo(destination);

        /// <summary>
        /// Copies the content of this <see cref="ReadOnlyNativeMemory{T}"/> into a <paramref name="destination"/> <see cref="NativeMemory{T}"/>.
        /// </summary>
        /// <param name="destination">The destination <see cref="NativeSpan{T}"/> object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(NativeMemory<T> destination) => Span.CopyTo(destination.Span);
        #endregion

        #region Conversions
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyNativeMemory<T>(ReadOnlyMemory<T> memory) => new(memory);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyNativeMemory<T>(Memory<T> memory) => new(memory);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyNativeMemory<T>(T[] memory) => new(memory);

        public static bool operator ==(ReadOnlyNativeMemory<T> left, ReadOnlyNativeMemory<T> right) => left.Equals(right);
        public static bool operator !=(ReadOnlyNativeMemory<T> left, ReadOnlyNativeMemory<T> right) => !(left == right);

        #endregion

        Enumerator ITypedEnumerable<T, Enumerator>.GetEnumerator() => new(this);
        public ReadOnlyNativeSpan<T>.Enumerator GetEnumerator() => new(Span);
        public override bool Equals(object? obj) => obj is ReadOnlyNativeMemory<T> memory && Equals(memory);
        public bool Equals(ReadOnlyNativeMemory<T> other) => EqualityComparer<object?>.Default.Equals(underlyingObject, other.underlyingObject) && start.Equals(other.start) && length.Equals(other.length) && type == other.type;
        public override int GetHashCode() => HashCode.Combine(underlyingObject, start, length, type);

        public struct Enumerator : IEnumerator<T>
        {
            private ReadOnlyNativeMemory<T> memory;
            private nuint index;
            internal Enumerator(ReadOnlyNativeMemory<T> memory)
            {
                index = ~(nuint)0;
                this.memory = memory;
            }

            public readonly T Current => memory is { } && index < memory.Span.Length ? memory.Span.ElementAtUnchecked(index) : default!;

            readonly object? IEnumerator.Current => Current;

            public void Dispose() => memory = default;
            [MemberNotNullWhen(true, nameof(Current))]
            public bool MoveNext() => ++index < memory.Length;
            public void Reset() => index = ~(nuint)0;
        }
    }
}
