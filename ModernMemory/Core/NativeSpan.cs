using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;
using ModernMemory.Sorting;

namespace ModernMemory
{
    /// <typeparam name="T">The type of items in the <see cref="NativeSpan{T}"/>.</typeparam>
    /// <inheritdoc cref="Span{T}"/>
    [DebuggerTypeProxy(typeof(NativeSpanDebugView<>))]
    [CollectionBuilder(typeof(NativeSpanUtils), nameof(NativeSpanUtils.Create))]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public readonly ref struct NativeSpan<T>
    {
        private readonly ref T head;
        private readonly nuint length;

        /// <summary>
        /// The length of this <see cref="NativeSpan{T}"/>.
        /// </summary>
        public nuint Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => length;
        }

        internal ref T Head => ref head;
        internal ref T Tail => ref Unsafe.Add(ref head, Length - 1);

        /// <summary>
        /// Returns a internalValue that indicates whether the current <see cref="NativeSpan{T}"/> is empty.
        /// </summary>
        /// <inheritdoc cref="Span{T}.IsEmpty"/>
        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => Length <= 0;
        }

        /// <summary>
        /// Creates a new <see cref="Span{T}"/> object that represents the current <see cref="NativeSpan{T}"/>.
        /// </summary>
        public Span<T> GetHeadSpan() => MemoryMarshal.CreateSpan(ref head, (int)Math.Min(int.MaxValue, Length));

        /// <summary>
        /// Returns a internalValue that indicates whether the current <see cref="NativeSpan{T}"/>'s contents can fit into <see cref="Span{T}"/>.
        /// </summary>
        public bool FitsInSpan => Length <= int.MaxValue;

        /// <summary>
        /// Returns an empty <see cref="NativeSpan{T}"/> object.
        /// </summary>
        /// <internalValue>An empty <see cref="NativeSpan{T}"/> object.</internalValue>
        public static NativeSpan<T> Empty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get => default;
        }

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeSpan{T}"/> struct.
        /// </summary>
        /// <param name="headPointer">The head pointer.</param>
        /// <param name="length">The length.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public unsafe NativeSpan(ref T headPointer, nuint length = 1)
        {
            head = ref headPointer;
            this.length = length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NativeSpan{T}"/> struct.
        /// </summary>
        /// <param name="headPointer">The head pointer.</param>
        /// <param name="length">The number of elements.</param>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public unsafe NativeSpan(void* headPointer, nuint length)
        {
            if (headPointer is null || length < 1)
            {
                this = default;
                return;
            }
            var span = new Span<T>(headPointer, 1);                     // This will throw if TRange was reference types anyway.
            this = new(ref MemoryMarshal.GetReference(span), length);   // And this is even good at being inlined.
        }

        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> object over the entirety of a specified <paramref name="array"/>.
        /// </summary>
        /// <param name="array">The memory from which to create the <see cref="NativeSpan{T}"/> object.</param>
        /// <exception cref="ArrayTypeMismatchException"><typeparamref name="T"/> is a reference type, and <paramref name="array"/> is not an memory of type <typeparamref name="T"/>.</exception>
        /// <remarks>If <paramref name="array"/> is null, this constructor returns a <see langword="null"/> <see cref="NativeSpan{T}"/>.</remarks>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public NativeSpan(T[]? array)
        {
            if (array is null)
            {
                this = default;
                return;
            }
            CheckArrayTypeMismatch(array);
            this = new(ref MemoryMarshal.GetArrayDataReference(array), checked((nuint)array.LongLength));
        }

        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> object over the entirety of a specified <paramref name="array"/>.
        /// </summary>
        /// <param name="array">The memory from which to create the <see cref="NativeSpan{T}"/> object.</param>
        /// <exception cref="ArrayTypeMismatchException"><typeparamref name="T"/> is a reference type, and <paramref name="array"/> is not an memory of type <typeparamref name="T"/>.</exception>
        /// <remarks>If <paramref name="array"/> is null, this constructor returns a <see langword="null"/> <see cref="NativeSpan{T}"/>.</remarks>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public NativeSpan(ArraySegment<T> array) : this(array.AsSpan())
        {
        }

        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> object that includes a specified number of elements of an memory starting at a specified index.
        /// </summary>
        /// <param name="array">The source memory.</param>
        /// <param name="start">The index of the first element to include in the new <see cref="NativeSpan{T}"/>.</param>
        /// <exception cref="ArrayTypeMismatchException"><typeparamref name="T"/> is a reference type, and <paramref name="array"/> is not an memory of type <typeparamref name="T"/>.</exception>
        /// <remarks>If <paramref name="array"/> is null, this constructor returns a <see langword="null"/> <see cref="NativeSpan{T}"/>.</remarks>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public NativeSpan(T[]? array, nuint start)
        {
            if (array is null)
            {
                this = default;
                return;
            }
            CheckArrayTypeMismatch(array);
            //range checks throws automatically
            _ = array[start];
            this = new(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), start), (nuint)array.Length - start);
        }

        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> object that includes a specified number of elements of an memory starting at a specified index.
        /// </summary>
        /// <param name="array">The source memory.</param>
        /// <param name="start">The index of the first element to include in the new <see cref="NativeSpan{T}"/>.</param>
        /// <param name="length">The number of elements to include in the new <see cref="NativeSpan{T}"/>.</param>
        /// <exception cref="ArrayTypeMismatchException"><typeparamref name="T"/> is a reference type, and <paramref name="array"/> is not an memory of type <typeparamref name="T"/>.</exception>
        /// <remarks>If <paramref name="array"/> is null, this constructor returns a <see langword="null"/> <see cref="NativeSpan{T}"/>.</remarks>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public NativeSpan(T[]? array, nuint start, nuint length)
        {
            if (array is null)
            {
                this = default;
                return;
            }
            CheckArrayTypeMismatch(array);
            //range checks throws automatically
            _ = array[start];
            _ = array[start + length];
            this = new(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), start), length);
        }

        /// <summary>
        /// Creates a new <see cref="NativeSpan{T}"/> object over the entirety of a specified <paramref name="span"/>.
        /// </summary>
        /// <param name="span">The memory region from which to create the <see cref="NativeSpan{T}"/> object.</param>
        /// <remarks>If <paramref name="span"/> is null, this constructor returns a <see langword="null"/> <see cref="NativeSpan{T}"/>.</remarks>
        [SkipLocalsInit]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public NativeSpan(Span<T> span)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(span.Length);
            this = new(ref MemoryMarshal.GetReference(span), (nuint)span.Length);
        }

        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static void CheckArrayTypeMismatch(T[]? array)
        {
            if (array is null || typeof(T).IsValueType || array.GetType() == typeof(T[]))
            {
                return;
            }
            throw new ArrayTypeMismatchException();
        }

        /// <inheritdoc cref="Span{T}.this[int]"/>
        public ref T this[nuint index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
            get
            {
                if (index >= length)
                {
                    ThrowHelper.ThrowIndexOutOfRangeException();
                }
                return ref Unsafe.Add(ref head, index);
            }
        }

        /// <inheritdoc cref="Span{T}.this[int]"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ref T ElementAtUnchecked(nuint index)
        {
            Debug.Assert(index < Length);
            return ref Unsafe.Add(ref head, index);
        }

        /// <inheritdoc cref="Span{T}.GetPinnableReference()"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Browsable(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public ref T GetPinnableReference()
        {
#pragma warning disable IDE0007 // Use implicit type (false positive)
            ref T ret = ref Unsafe.NullRef<T>();
#pragma warning restore IDE0007 // Use implicit type
            if (Length > 0) ret = ref head;
            return ref ret;
        }

        #region Slice
        /// <inheritdoc cref="Span{T}.Slice(int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public NativeSpan<T> Slice(nuint start) => TrySlice(out var span, start) ? span : ThrowSliceExceptions(start);

        /// <inheritdoc cref="Span{T}.Slice(int, int)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public NativeSpan<T> Slice(nuint start, nuint length) => TrySlice(out var span, start, length) ? span : ThrowSliceExceptions(start, length);

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private NativeSpan<T> ThrowSliceExceptions(nuint start)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, Length);
            throw new InvalidOperationException("Something went wrong!");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        private NativeSpan<T> ThrowSliceExceptions(nuint start, nuint length)
        {
            ThrowSliceExceptions(start, length, Length);
            throw new InvalidOperationException("Something went wrong!");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        [DoesNotReturn]
        internal static void ThrowSliceExceptions(nuint start, nuint length, nuint sourceLength)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(start, sourceLength);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(length, sourceLength - start);
            throw new InvalidOperationException("Something went wrong!");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TrySlice(out NativeSpan<T> nativeSpan, nuint start)
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
        public bool TrySlice(out NativeSpan<T> nativeSpan, nuint start, nuint length)
        {
            var currentLength = Length;
            ref var newHead = ref Unsafe.NullRef<T>();
            ref var newHeadCandidate = ref Unsafe.Add(ref head, start);
            nuint newLength = 0;
            var res = MathUtils.IsRangeInRange(currentLength, start, length);
            if (res)
            {
                newHead = ref newHeadCandidate;
                newLength = length;
            }
            nativeSpan = new(ref newHead, newLength);
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TrySliceWhile(out NativeSpan<T> nativeSpan, nuint length) => TrySlice(out nativeSpan, 0, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public NativeSpan<T> SliceWhile(nuint length) => Slice(0, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public NativeSpan<T> SliceWhileIfLongerThan(nuint length)
            => TrySliceWhile(out var span, length) ? span : this;

        #endregion
        #region Fill and Clear

        /// <summary>
        /// Fills the elements of this span with a specified <paramref name="value"/>.
        /// </summary>
        /// <inheritdoc cref="Span{T}.Fill(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Fill(T value)
        {
            if (IsEmpty) return;
            NativeMemoryUtils.Fill(value, ref Head, Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private void FillShort(T value)
        {
            nuint i = 0, length = Length;
            ref var rsi = ref head;
            var olen = MathUtils.SubtractSaturate(length, 7);
            for (; i < olen; i += 8)
            {
                Unsafe.Add(ref rsi, i + 0) = value;
                Unsafe.Add(ref rsi, i + 1) = value;
                Unsafe.Add(ref rsi, i + 2) = value;
                Unsafe.Add(ref rsi, i + 3) = value;
                Unsafe.Add(ref rsi, i + 4) = value;
                Unsafe.Add(ref rsi, i + 5) = value;
                Unsafe.Add(ref rsi, i + 6) = value;
                Unsafe.Add(ref rsi, i + 7) = value;
            }

            for (; i < length; i++)
            {
                Unsafe.Add(ref rsi, i) = value;
            }
        }

        /// <summary>
        /// Clears the contents of this <see cref="NativeSpan{T}"/> object.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void Clear() => NativeMemoryUtils.Clear(ref Head, Length);
        #endregion

        /// <summary>
        /// Copies the content of this <see cref="NativeSpan{T}"/> into a <paramref name="destination"/> <see cref="NativeSpan{T}"/>.
        /// </summary>
        /// <param name="destination">The destination <see cref="NativeSpan{T}"/> object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void CopyTo(NativeSpan<T> destination)
        {
            if (!TryCopyTo(destination)) throw new ArgumentException($"The {nameof(destination)} must be at least as long as this span!", nameof(destination));
        }

        /// <summary>
        /// Copies the content of this <see cref="NativeSpan{T}"/> into a <paramref name="destination"/> <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="destination">The destination <see cref="Span{T}"/> object.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public void CopyTo(Span<T> destination)
        {
            if (!TryCopyTo(destination)) throw new ArgumentException($"The {nameof(destination)} must be at least as long as this span!", nameof(destination));
        }

        /// <summary>
        /// Attempts to copy the current <see cref="NativeSpan{T}"/> to a destination <see cref="NativeSpan{T}"/> and returns a internalValue that indicates whether the copy operation succeeded.
        /// </summary>
        /// <inheritdoc cref="Span{T}.TryCopyTo(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryCopyTo(NativeSpan<T> destination) => ReadOnlyNativeSpan<T>.TryCopyTo(this, destination);

        /// <summary>
        /// Attempts to copy the current <see cref="NativeSpan{T}"/> to a destination <see cref="Span{T}"/> and returns a internalValue that indicates whether the copy operation succeeded.
        /// </summary>
        /// <inheritdoc cref="Span{T}.TryCopyTo(Span{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public bool TryCopyTo(Span<T> destination) => ReadOnlyNativeSpan<T>.TryCopyTo(this, destination);

        public readonly void Rotate(nuint position)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(position, Length);
            NativeMemoryUtils.Rotate(ref head, position, Length - position);
        }

        /// <summary>
        /// Determines whether the specified <paramref name="left"/> and <paramref name="right"/> shares the same region.
        /// </summary>
        /// <param name="left">The first checking <see cref="NativeSpan{T}"/>.</param>
        /// <param name="right">The second checking <see cref="NativeSpan{T}"/>.</param>
        /// <returns>The internalValue which indicates whether the specified <paramref name="left"/> and <paramref name="right"/> shares the same region.</returns>
        public static bool IsOverlapped(NativeSpan<T> left, NativeSpan<T> right)
        {
            if (left.IsEmpty || right.IsEmpty)
            {
                return false;
            }
            var bo = Unsafe.ByteOffset(ref left.head, ref right.head);
            return (nuint)bo < left.Length * (nuint)Unsafe.SizeOf<T>() || (nuint)bo > (nuint)(-(nint)right.Length) * (nuint)Unsafe.SizeOf<T>();
        }

        /// <summary>
        /// Indicates whether the owner of two specified <see cref="NativeSpan{T}"/> objects are equal.
        /// </summary>
        /// <param name="left">The first <see cref="NativeSpan{T}"/> to compare.</param>
        /// <param name="right">The second <see cref="NativeSpan{T}"/> to compare.</param>
        /// <returns>
        ///   <c>true</c> if the left is the same as the right; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool operator ==(NativeSpan<T> left, NativeSpan<T> right) => left.Length == right.Length && Unsafe.AreSame(ref left.head, ref right.head);

        /// <summary>
        /// Indicates whether the owner of two specified <see cref="NativeSpan{T}"/> objects are not equal.
        /// </summary>
        /// <param name="left">The first <see cref="NativeSpan{T}"/> to compare.</param>
        /// <param name="right">The second  <see cref="NativeSpan{T}"/> to compare.</param>
        /// <returns>
        ///   <c>true</c> if left and right are not equal; otherwise, <c>false</c>.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool operator !=(NativeSpan<T> left, NativeSpan<T> right) => !(left == right);

        #region Equals and GetHashCode

#pragma warning disable S1133 // Deprecated code should be removed
        /// <inheritdoc cref="Span{T}.Equals"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Equals() on NativeSpan will always throw an exception. Use the equality operator instead.", true)]
#pragma warning disable CS0809 // Obsolete member overrides non-obsolete member.
        public override bool Equals(object? obj) => Span<T>.Empty.Equals(default);

        /// <inheritdoc cref="Span{T}.GetHashCode"/>
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("GetHashCode() on NativeSpan will always throw an exception.", true)]
        public override unsafe int GetHashCode() => Span<T>.Empty.GetHashCode();
#pragma warning restore CS0809 // Obsolete member overrides non-obsolete member.
#pragma warning restore S1133 // Deprecated code should be removed

        #endregion

        #region Conversions
        /// <summary>
        /// Performs an implicit conversion from <see cref="Span{T}"/> to <see cref="NativeSpan{T}"/>.
        /// </summary>
        /// <param name="value">The internalValue to convert.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator NativeSpan<T>(Span<T> value) => new(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="Array"/> to <see cref="NativeSpan{T}"/>.
        /// </summary>
        /// <param name="value">The internalValue to convert.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator NativeSpan<T>(T[] value) => new(value);

        /// <summary>
        /// Performs an implicit conversion from <see cref="ArraySegment{T}"/> to <see cref="NativeSpan{T}"/>.
        /// </summary>
        /// <param name="value">The internalValue to convert.</param>
        /// <returns>
        /// The result of the conversion.
        /// </returns>
        public static implicit operator NativeSpan<T>(ArraySegment<T> value) => new(value.AsSpan());

        public readonly T[] ToArray() => FitsInSpan ? GetHeadSpan().ToArray()
                : throw new InvalidOperationException("The NativeSpan is larger than the limit that Array can hold!");

        public override readonly string ToString()
            => !RuntimeHelpers.IsReferenceOrContainsReferences<T>() && default(T) is char && FitsInSpan ? GetHeadSpan().ToString()
            : $"ModernMemory.NativeSpan<{typeof(T).Name}>[{Length}]";
        #endregion

        public ReadOnlyNativeSpan<T>.Enumerator GetEnumerator() => new(this);

        private string GetDebuggerDisplay() => ToString();
    }

    internal sealed class NativeSpanDebugView<T>
    {
        public NativeSpanDebugView(ReadOnlyNativeSpan<T> values)
        {
            Items = values.ToArray();
        }
        public NativeSpanDebugView(NativeSpan<T> values)
        {
            Items = values.ToArray();
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items { get; }
    }
}
