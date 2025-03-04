﻿using System;
using System.Buffers;
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
    /// <summary>
    /// Represents a contiguous region of memory.
    /// </summary>
    /// <typeparam name="T">The type of items in the <see cref="NativeMemory{T}"/>.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    [SuppressMessage("Major Code Smell", "S1168:Empty arrays and collections should be returned instead of null", Justification = "[] for Memory is too slow")]
    public readonly partial struct NativeMemory<T> : INativeMemory<T, NativeMemory<T>>, IEquatable<NativeMemory<T>>, IMemoryEnumerable<T>
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
        #region Constructors

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NativeMemory(MemoryType type, object? underlyingObject, nuint start, nuint length) : this()
        {
            this.type = type;
            this.underlyingObject = underlyingObject;
            this.start = start;
            this.length = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NativeMemory(NativeMemoryManager<T>? nativeSpanFactory, nuint start, nuint length)
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
        public NativeMemory(Memory<T> memory)
        {
            Unsafe.SkipInit(out this);
            var newType = MemoryType.MemoryManager;
            object? newUnderlyingObject = null;
            nuint newStart = 0;
            nuint newLength = 0;
            if (!memory.IsEmpty)
            {
                if (MemoryMarshal.TryGetMemoryManager<T, MemoryManager<T>>(memory, out var manager, out var intStart, out var intLength))
                {
                    newUnderlyingObject = manager;
                    newType = manager is NativeMemoryManager<T> ? MemoryType.NativeMemoryManager : newType;
                    newStart = (uint)intStart;
                    newLength = (uint)intLength;
                }
                else if (MemoryMarshal.TryGetArray<T>(memory, out var segment))
                {
                    newUnderlyingObject = segment.Array;
                    newType = MemoryType.Array;
                    newStart = (uint)segment.Offset;
                    newLength = (uint)segment.Count;
                }
            }

            this = new(newType, newUnderlyingObject, newStart, newLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory(ArraySegment<T> segment)
        {
            this = new(MemoryType.Array, segment.Array, (uint)segment.Offset, (uint)segment.Count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory(T[]? array, int start, int length)
        {
            nuint newLength = 0;
            nuint newStart = 0;
            if (array is not null)
            {
                newLength = (uint)array.Length;
                newStart = (uint)start;
                //range checks throws automatically
                _ = array[newStart];
                _ = array[checked(newStart + newLength)];
                newLength -= newStart;
            }
            this = new(MemoryType.Array, array, newStart, newLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory(T[]? array, int start)
        {
            nuint newLength = 0;
            nuint newStart = 0;
            if (array is not null)
            {
                _ = array[start];
                newLength = (uint)array.Length;
                newStart = (uint)start;
            }
            this = new(MemoryType.Array, array, newStart, newLength - newStart);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory(T[]? array)
        {
            nuint newLength = 0;
            if (array is not null)
            {
                newLength = (uint)array.Length;
            }
            this = new(MemoryType.Array, array, 0, newLength);
        }

        #endregion

        /// <summary>
        /// Returns an empty <see cref="NativeMemory{T}"/> object.
        /// </summary>
        public static NativeMemory<T> Empty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => default;
        }

        /// <inheritdoc cref="Memory{T}.IsEmpty"/>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => length == 0;
        }

        /// <summary>
        /// Returns a <see cref="NativeSpan{T}"/> from the current instance.
        /// </summary>
        /// <internalValue>A span created from the current <see cref="NativeSpan{T}"/> object.</internalValue>
        public NativeSpan<T> Span
        {
            [SkipLocalsInit]
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                var medium = underlyingObject;
                var t = type;
                var localLength = length;
                nuint newLength = 0;
                newLength = medium is not null ? localLength : newLength;
                var localStart = start;
                ref var head = ref Unsafe.NullRef<T>();
                if (medium is not null)
                {
                    nuint lengthToValidate;
                    if ((sbyte)t >= 0)
                    {
                        if (t == MemoryType.NativeMemoryManager)
                        {
                            Debug.Assert(medium is NativeMemoryManager<T>);
                            var nativeMemoryManager = Unsafe.As<NativeMemoryManager<T>>(medium);
                            head = ref nativeMemoryManager.GetReferenceAt(localStart)!;
#pragma warning disable S907 // "goto" statement should not be used
                            goto FastForward;
#pragma warning restore S907 // "goto" statement should not be used
                        }
                        else
                        {
                            Debug.Assert(medium is MemoryManager<T>);
                            var manager = Unsafe.As<MemoryManager<T>>(medium);
                            var span = manager.GetSpan();
                            lengthToValidate = (uint)span.Length;
                            head = ref Unsafe.Add(ref MemoryMarshal.GetReference(span), localStart)!;
                        }
                    }
                    else
                    {
                        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() == RuntimeHelpers.IsReferenceOrContainsReferences<char>() && typeof(T) == typeof(char) && t == MemoryType.String)
                        {
                            Debug.Assert(medium is string);
                            var ust = Unsafe.As<string>(medium);
                            lengthToValidate = (uint)ust.Length;
                            head = ref Unsafe.Add(ref Unsafe.As<char, T>(ref Unsafe.AsRef(in ust.GetPinnableReference())), localStart)!;
                        }
                        else
                        {
                            Debug.Assert(medium is T[]);
                            var array = Unsafe.As<T[]>(medium);
                            lengthToValidate = (uint)array.Length;
                            head = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), localStart)!;
                        }
                    }
                    Debug.Assert(MathUtils.IsRangeInRange(lengthToValidate, localStart, newLength));
                }
                FastForward:
                return new NativeSpan<T>(ref head, newLength);
            }
        }

        /// <summary>
        /// Creates a handle for the <see cref="NativeMemory{T}"/> object.
        /// </summary>
        /// <returns>A handle for the <see cref="NativeMemory{T}"/> object.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe MemoryHandle Pin() => NativeMemoryCore.Pin<T>(type, underlyingObject, start);

        public Memory<T> GetHeadMemory()
        {
            Memory<T> result = default;
            var t = type;
            var newLength = (int)nuint.Min(int.MaxValue, length);
            var medium = underlyingObject;
            var localStart = start;
            newLength = medium is null ? 0 : newLength;
            if (medium is not null)
            {
                if (nint.MaxValue > int.MaxValue && localStart + (nuint)newLength > int.MaxValue && t == MemoryType.NativeMemoryManager)
                {
                    Debug.Assert(medium is NativeMemoryManager<T>);
                    var manager = Unsafe.As<NativeMemoryManager<T>>(medium);
                    result = manager.CreateSlicedMemoryManager(localStart).Memory.Slice(0, newLength);
                }
                else
                {
                    var newStart = checked((int)localStart);
                    switch (t)
                    {
                        case MemoryType.String when RuntimeHelpers.IsReferenceOrContainsReferences<T>() == RuntimeHelpers.IsReferenceOrContainsReferences<char>() && typeof(T) == typeof(char):
                            Debug.Assert(medium is string);
                            var ust = Unsafe.As<string>(medium);
                            var cm = MemoryMarshal.AsMemory(ust.AsMemory()).Slice(newStart, newLength);
                            result = Unsafe.As<Memory<char>, Memory<T>>(ref cm);
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
            }
            return result;
        }

        #region Slice
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory<T> Slice(nuint start)
        {
            var currentLength = Length;
            var ol = currentLength - start;
            NativeMemory<T> result = new(type, underlyingObject, this.start + start, ol);
            return ol <= currentLength ? result : ThrowSliceExceptions(start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory<T> Slice(nuint start, nuint length)
        {
            var result = new NativeMemory<T>(type, underlyingObject, this.start + start, length);
            return MathUtils.IsRangeInRange(Length, start, length)
                ? result
                : ThrowSliceExceptions(start, length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private NativeMemory<T> ThrowSliceExceptions(nuint start)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
            throw new InvalidOperationException("Something went wrong!");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private NativeMemory<T> ThrowSliceExceptions(nuint start, nuint length)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, Length - start);
            throw new InvalidOperationException("Something went wrong!");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeMemory<T> SliceByRange(nuint startInclusive, nuint endExclusive)
        {
            var newLength = checked(endExclusive - startInclusive);
            return Slice(startInclusive, newLength);
        }
        #endregion

        #region Copy
        /// <summary>
        /// Attempts to copy the current <see cref="NativeMemory{T}"/> to a destination <see cref="NativeSpan{T}"/> and returns a internalValue that indicates whether the copy operation succeeded.
        /// </summary>
        /// <inheritdoc cref="Span{T}.TryCopyTo(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCopyTo(NativeSpan<T> destination) => Span.TryCopyTo(destination);

        /// <summary>
        /// Attempts to copy the current <see cref="NativeMemory{T}"/> to a destination <see cref="NativeMemory{T}"/> and returns a internalValue that indicates whether the copy operation succeeded.
        /// </summary>
        /// <inheritdoc cref="Span{T}.TryCopyTo(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryCopyTo(NativeMemory<T> destination) => Span.TryCopyTo(destination.Span);

        /// <summary>
        /// Copies the content of this <see cref="NativeMemory{T}"/> into a <paramref name="destination"/> <see cref="NativeSpan{T}"/>.
        /// </summary>
        /// <param name="destination">The destination <see cref="NativeSpan{T}"/> object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(NativeSpan<T> destination) => Span.CopyTo(destination);

        /// <summary>
        /// Copies the content of this <see cref="NativeMemory{T}"/> into a <paramref name="destination"/> <see cref="NativeMemory{T}"/>.
        /// </summary>
        /// <param name="destination">The destination <see cref="NativeSpan{T}"/> object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void CopyTo(NativeMemory<T> destination) => Span.CopyTo(destination.Span);

        #endregion
        #region Equality

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(NativeMemory<T> other)
            => type == other.type
                && underlyingObject == other.underlyingObject
                && start == other.start
                && length == other.length;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj) => obj is NativeMemory<T> memory && Equals(memory);
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => HashCode.Combine(type, underlyingObject, start, length);
        public ReadOnlyNativeSpan<T>.Enumerator GetEnumerator() => new(Span);

        public ReadOnlyNativeMemory<T>.Enumerator GetMemoryEnumerator() => new(this);
        ReadOnlyNativeMemory<T>.Enumerator ITypedEnumerable<T, ReadOnlyNativeMemory<T>.Enumerator>.GetEnumerator() => GetMemoryEnumerator();
        NativeMemory<T> INativeMemoryBase<T>.AsNativeMemory() => this;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(NativeMemory<T> left, NativeMemory<T> right) => left.Equals(right);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(NativeMemory<T> left, NativeMemory<T> right) => !(left == right);
        #endregion

        #region Conversions
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator NativeMemory<T>(Memory<T> memory) => new(memory);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ReadOnlyNativeMemory<T>(NativeMemory<T> nativeMemory)
            => new(nativeMemory.type, nativeMemory.underlyingObject, nativeMemory.start, nativeMemory.Length);
        #endregion
    }
}
