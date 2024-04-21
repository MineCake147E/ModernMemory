using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    public static class MathUtils
    {
        /// <summary>
        /// Divides a number consists of two <see cref="nuint"/> number <paramref name="hi"/> and <paramref name="lo"/> by a constant <paramref name="divisor"/>.
        /// </summary>
        /// <param name="hi">The higher part of numerator.</param>
        /// <param name="lo">The lower part of numerator.</param>
        /// <param name="divisor">The divisor. Assumed to be a constant number.</param>
        /// <returns>The lower <see cref="nuint"/> quotient.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint BigDivConstant(nuint hi, nuint lo, nuint divisor)
        {
            var hiq = ReciprocalUIntPtr(divisor, out var hir);
            lo += hir * hi;
            hi = hiq * hi;
            hi += lo / divisor;
            return hi;
        }

        /// <summary>
        /// Calculates the (<see cref="nuint.MaxValue"/> + 1) / <paramref name="value"/> and its remainder.
        /// </summary>
        /// <param name="value">The internalValue to divide (<see cref="nuint.MaxValue"/> + 1) by.</param>
        /// <param name="remainder">The remainder (<see cref="nuint.MaxValue"/> + 1) % <paramref name="value"/>.</param>
        /// <returns>The quotient (<see cref="nuint.MaxValue"/> + 1) / <paramref name="value"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint ReciprocalUIntPtr(nuint value, out nuint remainder)
        {
            (var q, var r) = nuint.DivRem(nuint.MaxValue, value);
            var kk = Unsafe.BitCast<bool, byte>(++r < value) - (nuint)1;
            q -= kk;
            r = ~kk & r;
            remainder = r;
            return q;
        }

        #region Unrolling Helpers

        /// <summary>
        /// Returns 0 if <paramref name="right"/> is greater than <paramref name="left"/>, otherwise subtracts <paramref name="right"/> from <paramref name="left"/>.
        /// </summary>
        /// <param name="left">The original internalValue.</param>
        /// <param name="right">The internalValue to subtract from <paramref name="left"/>.</param>
        /// <returns>0 if <paramref name="right"/> is greater than <paramref name="left"/>, otherwise, <paramref name="left"/> - <paramref name="right"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint SubtractSaturate(nuint left, nuint right)
        {
            nuint res = 0;
            var y = left - right;
            if (left >= right) res = y;
            return res;
        }

        /// <summary>
        /// Returns 0 if <paramref name="right"/> is greater than <paramref name="left"/> or <paramref name="right"/> is negative, otherwise subtracts <paramref name="right"/> from <paramref name="left"/>.<br/>
        /// In Debug mode, it'll throw an <see cref="ArgumentOutOfRangeException"/> if <paramref name="right"/> were negative.
        /// </summary>
        /// <param name="left">The original internalValue.</param>
        /// <param name="right">The internalValue to subtract from <paramref name="left"/>.</param>
        /// <returns>0 if <paramref name="right"/> is greater than <paramref name="left"/> or <paramref name="right"/> is negative, otherwise, <paramref name="left"/> - <paramref name="right"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="right"/> was negative in Debug mode.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint SubtractSaturate(nuint left, nint right)
        {
            if (ThrowHelper.IsDebugDefined) ArgumentOutOfRangeException.ThrowIfNegative(right);
            var r = (nuint)right;
            var res = (nuint)0;
            var y = left - r;
            if (right < 0) y = res;
            if (left >= r) res = y;
            return res;
        }

        /// <summary>
        /// Calculates the appropriate offset <paramref name="length"/> internalValue for specified <paramref name="unroll"/> factor.
        /// </summary>
        /// <param name="length">The original length.</param>
        /// <param name="unroll">The amount of unrolling.</param>
        /// <returns>0 if <paramref name="length"/> is less than <paramref name="unroll"/>, otherwise, <paramref name="length"/> - <paramref name="unroll"/> + 1.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="right"/> was 0 in Debug mode.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint CalculateUnrollingOffsetLength(nuint length, nuint unroll)
        {
            if (ThrowHelper.IsDebugDefined) ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unroll);
            return SubtractSaturate(length, unroll - 1);
        }

        /// <summary>
        /// Calculates the appropriate offset <paramref name="length"/> internalValue for specified <paramref name="unroll"/> factor.
        /// </summary>
        /// <param name="length">The original length.</param>
        /// <param name="unroll">The amount of unrolling.</param>
        /// <returns>0 if <paramref name="length"/> is less than <paramref name="unroll"/>, otherwise, <paramref name="length"/> - <paramref name="unroll"/> + 1.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="right"/> was not positive in Debug mode.
        /// </exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint CalculateUnrollingOffsetLength(nuint length, nint unroll) => SubtractSaturate(length, unroll - 1);

        public static bool IsInUnrolledRange(nuint i, nuint length, nuint unroll, out nuint offsetLength)
        {
            var olen = length - unroll + 1;
            bool r0 = olen < length;
            bool r1 = i < olen;
            offsetLength = olen;
            return r0 && r1;
        }

        #endregion

        /// <summary>
        /// Determines whether the <paramref name="value"/> is in specified <paramref name="range"/>.
        /// </summary>
        /// <typeparam name="T">The type of <paramref name="value"/>.</typeparam>
        /// <param name="range">The range to test.</param>
        /// <param name="value">The address to test.</param>
        /// <returns>The internalValue which indicates whether the <paramref name="value"/> is in specified <paramref name="range"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool IsAddressInRange<T>(NativeSpan<T> range, ref T value)
        {
            unsafe
            {
                var b = (nuint)Unsafe.ByteOffset(ref range.Head, ref value);
                return b < range.Length * (nuint)Unsafe.SizeOf<T>();
            }
        }
        /// <summary>
        /// Determines whether the <paramref name="value"/> is in specified region starts from <paramref name="start"/> with specified <paramref name="length"/>.
        /// </summary>
        /// <typeparam name="T">The type of <paramref name="value"/>.</typeparam>
        /// <param name="start"></param>
        /// <param name="length"></param>
        /// <param name="value">The address to test.</param>
        /// <returns>The internalValue which indicates whether the <paramref name="value"/> is in specified region.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool IsAddressInRange<T>(ref T start, nuint length, ref T value)
        {
            unsafe
            {
                var b = (nuint)Unsafe.ByteOffset(ref start, ref value);
                return b < length * (nuint)Unsafe.SizeOf<T>();
            }
        }

        #region Range Check Helpers

        /// <summary>
        /// Checks if the sliced range consists of <paramref name="start"/> and <paramref name="length"/> is in between 0 and <paramref name="range"/>.
        /// </summary>
        /// <param name="range">The exclusive upper limit of the range.</param>
        /// <param name="start">The starting point of the sliced range.</param>
        /// <param name="length">The length of the sliced range.</param>
        /// <returns><see langword="true"/> if the sliced range consists of <paramref name="start"/> and <paramref name="length"/> is in between 0 and <paramref name="range"/>, <see langword="false"/> otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool IsRangeInRange(nuint range, nuint start, nuint length)
        {
            var olen = range - length;
            var y = olen <= range;
#pragma warning disable S2178 // Short-circuit logic should be used in boolean contexts (it's not always faster)
            return start <= olen & y;
#pragma warning restore S2178 // Short-circuit logic should be used in boolean contexts
        }
        #endregion

        #region Sign and CompareTo
        public static nint Sign(nint value)
        {
            var d = -value;
            var sh = 8 * Unsafe.SizeOf<nint>() - 1;
            return (d >>> sh) | (value >> sh);
        }

        /// <inheritdoc cref="IComparable{T}.CompareTo(T?)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static int CompareToByComparisonOperators<T>(this T self, T other) where T : IComparisonOperators<T, T, bool>
        {
            var p = self > other;
            var n = self < other;
            return (p ? 1 : 0) - (n ? 1 : 0);
        }
        #endregion



        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint AbsDiff(nuint left, nuint right)
        {
            var d = left - right;
            var res = right - left;
            res = right < left ? d : res;
            return res;
        }

        public static ReadOnlySpan<ulong> Factorials => [1, 1, 2, 6, 24, 120, 720, 5040, 40320, 362880, 3628800, 39916800, 479001600, 6227020800, 87178291200, 1307674368000, 20922789888000, 355687428096000, 6402373705728000, 121645100408832000, 2432902008176640000];
    }
}
