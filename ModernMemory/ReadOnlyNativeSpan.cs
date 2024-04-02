using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    /// <typeparam name="T">The type of items in the <see cref="ReadOnlyNativeSpan{T}"/>.</typeparam>
    /// <inheritdoc cref="ReadOnlySpan{T}"/>
    [DebuggerTypeProxy(typeof(NativeSpanDebugView<>))]
    [CollectionBuilder(typeof(ReadOnlyNativeSpanUtils), nameof(ReadOnlyNativeSpanUtils.Create))]
    [StructLayout(LayoutKind.Sequential)]
    [DebuggerDisplay($"{{{nameof(ToString)}(),nq}}")]
    public readonly ref struct ReadOnlyNativeSpan<T>
    {
        private readonly ref T head;
        private readonly nuint length;

        /// <summary>
        /// The copyLength of this <see cref="ReadOnlyNativeSpan{T}"/>.
        /// </summary>
        public nuint Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => length;
        }

        internal ref readonly T Head => ref head;
        internal ref readonly T Tail => ref NativeMemoryUtils.Add(in head, Length - 1);

        /// <summary>
        /// Returns a internalValue that indicates whether the current <see cref="ReadOnlyNativeSpan{T}"/> is empty.
        /// </summary>
        /// <inheritdoc cref="ReadOnlySpan{T}.IsEmpty"/>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => 0 >= Length;
        }

        /// <summary>
        /// Creates a new <see cref="ReadOnlySpan{T}"/> object that represents the current <see cref="ReadOnlyNativeSpan{T}"/>.
        /// </summary>
        public ReadOnlySpan<T> GetHeadReadOnlySpan() => MemoryMarshal.CreateReadOnlySpan(in head, (int)nuint.Min(int.MaxValue, Length));

        /// <summary>
        /// Returns a internalValue that indicates whether the current <see cref="ReadOnlyNativeSpan{T}"/>'s contents can fit into <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        public bool FitsInReadOnlySpan => Length <= int.MaxValue;

        /// <summary>
        /// Returns a internalValue that indicates whether the current <see cref="ReadOnlyNativeSpan{T}"/>'s contents can fit into <see cref="Array"/>.
        /// </summary>
        public bool FitsInArray => Length <= (nuint)Array.MaxLength;

        /// <summary>
        /// Returns an empty <see cref="ReadOnlyNativeSpan{T}"/> object.
        /// </summary>
        /// <internalValue>An empty <see cref="ReadOnlyNativeSpan{T}"/> object.</internalValue>
        public static ReadOnlyNativeSpan<T> Empty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => default;
        }

        #region Constructors
        /// <summary>
        /// Creates a new <see cref="ReadOnlyNativeSpan{T}"/> object over the entirety of a specified <paramref name="span"/>.
        /// </summary>
        /// <param name="span">The memory region from which to create the <see cref="ReadOnlyNativeSpan{T}"/> object.</param>
        /// <remarks>If <paramref name="span"/> is null, this constructor returns a <see langword="null"/> <see cref="ReadOnlyNativeSpan{T}"/>.</remarks>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ReadOnlyNativeSpan(NativeSpan<T> span)
        {
            if (span.IsEmpty)
            {
                this = default;
                return;
            }
            this = new(ref NativeMemoryUtils.GetReference(span), span.Length);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlyNativeSpan{T}"/> struct.
        /// </summary>
        /// <param name="headPointer">The head pointer.</param>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public unsafe ReadOnlyNativeSpan(ref readonly T headPointer)
        {
            head = ref Unsafe.AsRef(in headPointer);
            length = 1;
        }



        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlyNativeSpan{T}"/> struct.
        /// </summary>
        /// <param name="headPointer">The head pointer.</param>
        /// <param name="length">The copyLength.</param>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public unsafe ReadOnlyNativeSpan(ref readonly T headPointer, nuint length)
        {
            head = ref Unsafe.AsRef(in headPointer);
            this.length = length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlyNativeSpan{T}"/> struct.
        /// </summary>
        /// <param name="headPointer">The head pointer.</param>
        /// <param name="length">The copyLength.</param>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public unsafe ReadOnlyNativeSpan(void* headPointer, nuint length)
        {
            var span = new ReadOnlySpan<T>(headPointer, 0);
            this = new(ref MemoryMarshal.GetReference(span), length);
        }

        /// <summary>
        /// Creates a new <see cref="ReadOnlyNativeSpan{T}"/> object over the entirety of a specified <paramref name="array"/>.
        /// </summary>
        /// <param name="array">The memory from which to create the <see cref="ReadOnlyNativeSpan{T}"/> object.</param>
        /// <exception cref="ArrayTypeMismatchException"><typeparamref name="T"/> is a reference type, and <paramref name="array"/> is not an memory of type <typeparamref name="T"/>.</exception>
        /// <remarks>If <paramref name="array"/> is null, this constructor returns a <see langword="null"/> <see cref="ReadOnlyNativeSpan{T}"/>.</remarks>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ReadOnlyNativeSpan(T[]? array)
        {
            if (array is null)
            {
                this = default;
                return;
            }
            this = new(ref MemoryMarshal.GetArrayDataReference(array), unchecked((nuint)array.LongLength));
        }

        /// <summary>
        /// Creates a new <see cref="ReadOnlyNativeSpan{T}"/> object over the entirety of a specified <paramref name="array"/>.
        /// </summary>
        /// <param name="array">The memory from which to create the <see cref="ReadOnlyNativeSpan{T}"/> object.</param>
        /// <exception cref="ArrayTypeMismatchException"><typeparamref name="T"/> is a reference type, and <paramref name="array"/> is not an memory of type <typeparamref name="T"/>.</exception>
        /// <remarks>If <paramref name="array"/> is null, this constructor returns a <see langword="null"/> <see cref="ReadOnlyNativeSpan{T}"/>.</remarks>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ReadOnlyNativeSpan(ArraySegment<T> array) : this(array.AsSpan())
        {
        }

        /// <summary>
        /// Creates a new <see cref="ReadOnlyNativeSpan{T}"/> object that includes a specified number of elements of an memory starting at a specified index.
        /// </summary>
        /// <param name="array">The source memory.</param>
        /// <param name="start">The index of the first element to include in the new <see cref="ReadOnlyNativeSpan{T}"/>.</param>
        /// <exception cref="ArrayTypeMismatchException"><typeparamref name="T"/> is a reference type, and <paramref name="array"/> is not an memory of type <typeparamref name="T"/>.</exception>
        /// <remarks>If <paramref name="array"/> is null, this constructor returns a <see langword="null"/> <see cref="ReadOnlyNativeSpan{T}"/>.</remarks>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ReadOnlyNativeSpan(T[]? array, nuint start)
        {
            if (array is null)
            {
                this = default;
                return;
            }
            _ = array[start];
            this = new(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), start), (nuint)array.LongLength - start);
        }

        /// <summary>
        /// Creates a new <see cref="ReadOnlyNativeSpan{T}"/> object that includes a specified number of elements of an memory starting at a specified index.
        /// </summary>
        /// <param name="array">The source memory.</param>
        /// <param name="start">The index of the first element to include in the new <see cref="ReadOnlyNativeSpan{T}"/>.</param>
        /// <param name="length">The number of elements to include in the new <see cref="ReadOnlyNativeSpan{T}"/>.</param>
        /// <exception cref="ArrayTypeMismatchException"><typeparamref name="T"/> is a reference type, and <paramref name="array"/> is not an memory of type <typeparamref name="T"/>.</exception>
        /// <remarks>If <paramref name="array"/> is null, this constructor returns a <see langword="null"/> <see cref="ReadOnlyNativeSpan{T}"/>.</remarks>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ReadOnlyNativeSpan(T[]? array, nuint start, nuint length)
        {
            if (array is null)
            {
                this = default;
                return;
            }
            //range checks throws automatically
            _ = array[start];
            _ = array[start + length];
            this = new(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), start), length);
        }

        /// <summary>
        /// Creates a new <see cref="ReadOnlyNativeSpan{T}"/> object over the entirety of a specified <paramref name="span"/>.
        /// </summary>
        /// <param name="span">The memory region from which to create the <see cref="ReadOnlyNativeSpan{T}"/> object.</param>
        /// <remarks>If <paramref name="span"/> is null, this constructor returns a <see langword="null"/> <see cref="ReadOnlyNativeSpan{T}"/>.</remarks>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ReadOnlyNativeSpan(ReadOnlySpan<T> span)
        {
            if (span.IsEmpty)
            {
                this = default;
                return;
            }
            this = new(ref MemoryMarshal.GetReference(span), (nuint)span.Length);
        }

        /// <summary>
        /// Creates a new <see cref="ReadOnlyNativeSpan{T}"/> object over the entirety of a specified <paramref name="span"/>.
        /// </summary>
        /// <param name="span">The memory region from which to create the <see cref="ReadOnlyNativeSpan{T}"/> object.</param>
        /// <remarks>If <paramref name="span"/> is null, this constructor returns a <see langword="null"/> <see cref="ReadOnlyNativeSpan{T}"/>.</remarks>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ReadOnlyNativeSpan(Span<T> span)
        {
            if (span.IsEmpty)
            {
                this = default;
                return;
            }
            this = new(ref MemoryMarshal.GetReference(span), (nuint)span.Length);
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static void CheckArrayTypeMismatch(T[]? array)
        {
            if (array is not null && !(typeof(T).IsValueType || array.GetType() == typeof(T[])))
            {
                ThrowHelper.Throw(new ArrayTypeMismatchException());
            }
        }

        /// <inheritdoc cref="ReadOnlySpan{T}.this[int]"/>
        public ref readonly T this[nuint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get
            {
                if (index >= Length)
                {
                    ThrowHelper.ThrowIndexOutOfRangeException();
                }
                return ref NativeMemoryUtils.Add(in head, index);
            }
        }

        /// <inheritdoc cref="ReadOnlySpan{T}.this[int]"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ref readonly T ElementAtUnchecked(nuint index)
        {
            Debug.Assert(index < Length);
            return ref NativeMemoryUtils.Add(in head, index);
        }

        /// <inheritdoc cref="ReadOnlySpan{T}.GetPinnableReference()"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ref readonly T GetPinnableReference() => ref head;

        #region Slice
        /// <inheritdoc cref="ReadOnlySpan{T}.Slice(int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ReadOnlyNativeSpan<T> Slice(nuint start)
        {
            if (TrySlice(out var span, start))
            {
                return span;
            }
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
            throw new InvalidOperationException("Something went wrong!");
        }

        /// <inheritdoc cref="ReadOnlySpan{T}.Slice(int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ReadOnlyNativeSpan<T> Slice(nuint start, nuint length)
        {
            if (TrySlice(out var span, start, length))
            {
                return span;
            }
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, Length - start);
            throw new InvalidOperationException("Something went wrong!");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TrySlice(out ReadOnlyNativeSpan<T> nativeSpan, nuint start)
        {
            var currentLength = Length;
            var olen = currentLength - start;
            if (olen <= currentLength)
            {
                nativeSpan = new(ref Unsafe.Add(ref head, start), olen);
                return true;
            }
            nativeSpan = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TrySlice(out ReadOnlyNativeSpan<T> nativeSpan, nuint start, nuint length)
        {
            var currentLength = Length;
            if (MathUtils.IsRangeInRange(currentLength, start, length))
            {
                nativeSpan = new(ref Unsafe.Add(ref head, start), length);
                return true;
            }
            nativeSpan = default;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TrySliceWhile(out ReadOnlyNativeSpan<T> nativeSpan, nuint length) => TrySlice(out nativeSpan, 0, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ReadOnlyNativeSpan<T> SliceWhile(nuint length) => Slice(0, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ReadOnlyNativeSpan<T> SliceWhileIfLongerThan(nuint length)
            => TrySliceWhile(out var span, length) ? span : this;

        #endregion

        /// <summary>
        /// Copies the content of this <see cref="ReadOnlyNativeSpan{T}"/> into a <paramref name="destination"/> <see cref="NativeSpan{T}"/>.
        /// </summary>
        /// <param name="destination">The destination <see cref="NativeSpan{T}"/> object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void CopyTo(NativeSpan<T> destination)
        {
            if (!TryCopyTo(this, destination)) throw new ArgumentException($"The {nameof(destination)} must be at least as long as this span!", nameof(destination));
        }

        /// <summary>
        /// Copies the content of this <see cref="ReadOnlyNativeSpan{T}"/> into a <paramref name="destination"/> <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="destination">The destination <see cref="ReadOnlySpan{T}"/> object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void CopyTo(Span<T> destination)
        {
            if (!TryCopyTo(this, destination)) throw new ArgumentException($"The {nameof(destination)} must be at least as long as this span!", nameof(destination));
        }

        public nuint CopyAtMostTo(NativeSpan<T> destination, nuint offset = 0)
        {
            var len = Length;
            if (offset >= len || destination.IsEmpty) return 0;
            ref readonly var srcHead = ref ElementAtUnchecked(offset);
            var copyLength = nuint.Min(len - offset, destination.Length);
            NativeMemoryUtils.MoveMemory(ref destination.Head, in srcHead, copyLength);
            return copyLength;
        }

        public int CopyAtMostTo(Span<T> destination, int offset = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(offset);
            return (int)CopyAtMostTo(new NativeSpan<T>(destination), (nuint)offset);
        }

        /// <inheritdoc cref="ReadOnlySpan{T}.TryCopyTo(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryCopyTo(NativeSpan<T> destination) => TryCopyTo(this, destination);

        /// <inheritdoc cref="ReadOnlySpan{T}.TryCopyTo(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryCopyTo(Span<T> destination) => TryCopyTo(this, destination);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static bool TryCopyTo(ReadOnlyNativeSpan<T> source, NativeSpan<T> destination)
        {
            if (destination.IsEmpty || source.IsEmpty)
            {
                return source.IsEmpty;
            }
            if (destination.Length < source.Length)
            {
                return false;
            }
            NativeMemoryUtils.MoveMemory(ref destination.Head, ref source.head, source.Length);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal static bool TryCopyTo(ReadOnlyNativeSpan<T> source, Span<T> destination) => destination.IsEmpty || source.IsEmpty
                ? source.IsEmpty
                : (nuint)destination.Length >= source.Length
                && (Unsafe.AreSame(ref source.head, ref MemoryMarshal.GetReference(destination)) || source.FitsInReadOnlySpan && source.GetHeadReadOnlySpan().TryCopyTo(destination));

        /// <summary>
        /// Determines whether the specified <paramref name="left"/> and <paramref name="right"/> shares the same region.
        /// </summary>
        /// <param name="left">The first checking <see cref="ReadOnlyNativeSpan{T}"/>.</param>
        /// <param name="right">The second checking <see cref="ReadOnlyNativeSpan{T}"/>.</param>
        /// <returns>The internalValue which indicates whether the specified <paramref name="left"/> and <paramref name="right"/> shares the same region.</returns>
        public static bool IsOverlapped(ReadOnlyNativeSpan<T> left, ReadOnlyNativeSpan<T> right)
        {
            if (left.IsEmpty || right.IsEmpty)
            {
                return false;
            }
            var bo = Unsafe.ByteOffset(ref left.head, ref right.head);
            return (nuint)bo < left.Length * (nuint)Unsafe.SizeOf<T>() || (nuint)bo > (nuint)(-(nint)right.Length) * (nuint)Unsafe.SizeOf<T>();
        }

        /// <summary>
        /// Indicates whether the values of two specified <see cref="ReadOnlyNativeSpan{T}"/> objects are equal.
        /// </summary>
        /// <param name="left">The first <see cref="ReadOnlyNativeSpan{T}"/> to compare.</param>
        /// <param name="right">The second <see cref="ReadOnlyNativeSpan{T}"/> to compare.</param>
        /// <returns>
        ///   <c>true</c> if the left is the same as the right; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool operator ==(ReadOnlyNativeSpan<T> left, ReadOnlyNativeSpan<T> right) => left.Length == right.Length && Unsafe.AreSame(ref left.head, ref right.head);

        /// <summary>
        /// Indicates whether the values of two specified <see cref="ReadOnlyNativeSpan{T}"/> objects are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="ReadOnlyNativeSpan{T}"/> to compare.</param>
        /// <param name="right">The second  <see cref="ReadOnlyNativeSpan{T}"/> to compare.</param>
        /// <returns>
        ///   <c>true</c> if left and right are not equal; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool operator !=(ReadOnlyNativeSpan<T> left, ReadOnlyNativeSpan<T> right) => !(left == right);

        #region Equals and GetHashCode
#pragma warning disable S1133 // Deprecated code should be removed
        /// <inheritdoc cref="ReadOnlySpan{T}.Equals"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Equals() on ReadOnlyNativeSpan will always throw an exception. Use the equality operator instead.", true)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member.
        public override bool Equals(object? obj) => ReadOnlySpan<T>.Empty.Equals(default);

        /// <inheritdoc cref="ReadOnlySpan{T}.GetHashCode"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("GetHashCode() on ReadOnlyNativeSpan will always throw an exception.", true)]
        public override unsafe int GetHashCode() => ReadOnlySpan<T>.Empty.GetHashCode();
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member.
#pragma warning restore S1133 // Deprecated code should be removed
        #endregion

        #region Conversions
        /// <summary>
        /// Performs an implicit conversion from <see cref="ReadOnlySpan{T}"/> to <see cref="ReadOnlyNativeSpan{T}"/>.
        /// </summary>
        /// <param name="value">The internalValue to convert.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator ReadOnlyNativeSpan<T>(ReadOnlySpan<T> value) => new(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="Span{T}"/> to <see cref="ReadOnlyNativeSpan{T}"/>.
        /// </summary>
        /// <param name="value">The internalValue to convert.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator ReadOnlyNativeSpan<T>(Span<T> value) => new(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="NativeSpan{T}"/> to <see cref="ReadOnlySpan{T}"/>.
        /// </summary>
        /// <param name="value">The internalValue to convert.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator ReadOnlyNativeSpan<T>(NativeSpan<T> value) => new(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="Array"/> to <see cref="ReadOnlyNativeSpan{T}"/>.
        /// </summary>
        /// <param name="value">The internalValue to convert.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator ReadOnlyNativeSpan<T>(T[] value) => new(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="ArraySegment{T}"/> to <see cref="ReadOnlyNativeSpan{T}"/>.
        /// </summary>
        /// <param name="value">The internalValue to convert.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator ReadOnlyNativeSpan<T>(ArraySegment<T> value) => new(value);

        public readonly T[] ToArray() => FitsInArray ? GetHeadReadOnlySpan().ToArray()
                : throw new InvalidOperationException("The NativeSpan is larger than the limit that Array can hold!");

        public override readonly string ToString()
            => !RuntimeHelpers.IsReferenceOrContainsReferences<T>() && default(T) is char && FitsInReadOnlySpan ? GetHeadReadOnlySpan().ToString()
            : $"ModernMemory.ReadOnlyNativeSpan<{typeof(T).Name}>[{Length}]";

        #endregion

        public Enumerator GetEnumerator() => new(this);

        public ref struct Enumerator
        {
            private readonly ReadOnlyNativeSpan<T> span;
            private nuint index;

            internal Enumerator(ReadOnlyNativeSpan<T> span)
            {
                this.span = span;
                index = ~(nuint)0;
            }
            public readonly T Current => span[index];

            public bool MoveNext() => ++index < span.Length;
            public void Reset() => index = 0;
        }
    }

}
