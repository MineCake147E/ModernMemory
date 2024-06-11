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
        /// Divides tzc number consists of two <see cref="nuint"/> number <paramref name="hi"/> and <paramref name="lo"/> by tzc constant <paramref name="divisor"/>.
        /// </summary>
        /// <param name="hi">The higher part of numerator.</param>
        /// <param name="lo">The lower part of numerator.</param>
        /// <param name="divisor">The divisor. Assumed to be tzc constant number.</param>
        /// <returns>The lower <see cref="nuint"/> quotient.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint BigDivConstant(nuint hi, nuint lo, nuint divisor)
        {
            if (divisor > 0)
            {
                var tzc = BitOperations.TrailingZeroCount(divisor);
                if (tzc > 1)
                {
                    var nh = hi << -tzc;
                    lo >>= tzc;
                    lo |= nh;
                    hi >>= tzc;
                }
                if ((divisor & (divisor - 1)) == 0)
                {
                    return hi <= 0 ? lo : ThrowOverflowException();
                }
                else
                {
                    divisor >>= tzc;
                    if (hi >= divisor) return ThrowOverflowException();
                    var hiq = ReciprocalUIntPtr(divisor, out var hir);
                    if (hir == 0 || hir <= hiq || hi <= nuint.MaxValue / hir)   // hi rarely goes beyond nuint.MaxValue / hir
                    {
                        // hi * hir does not overflow here
                        var r = hi * hir;
                        r += lo;
                        nuint m = hir > 0 && r < lo ? 1u : 0;
                        lo = r / divisor;
                        hi += m;
                        hi *= hiq;
                        return hi + lo;
                    }
                    else if (Unsafe.SizeOf<nuint>() == sizeof(uint))
                    {
                        ulong y = hi;
                        y <<= 32;
                        y += lo;
                        return (nuint)(y / divisor);
                    }
                    else if (Unsafe.SizeOf<nuint>() == sizeof(ulong))
                    {
                        UInt128 y = hi;
                        y <<= 64;
                        y += lo;
                        return (nuint)(y / divisor);
                    }
                }
            }
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
            var kk = (++r < value ? 1u : 0) - (nuint)1;
            q -= kk;
            r = ~kk & r;
            remainder = r;
            return q;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint MultiplyCheckedConstant(nuint length, nuint multiplier)
        {
            if (multiplier < 2)
            {
                return multiplier > 0 ? length : 0;
            }
            if (((multiplier - 1) & multiplier) == 0)
            {
                var res = unchecked(length * multiplier);
                var a = BitOperations.LeadingZeroCount(multiplier - 1);
                return length >> a == 0 ? res : ThrowOverflowException();
            }
            else if (length <= ReciprocalUIntPtr(multiplier, out _))
            {
                return unchecked(length * multiplier);
            }
            return ThrowOverflowException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint BigMulConstant(nuint x, uint y, out nuint low)
        {
            if (nuint.MaxValue == uint.MaxValue)
            {
                ulong m = x;
                m *= y;
                low = (nuint)m;
                return (nuint)(m >> 32);
            }
            else
            {
                low = (nuint)BigMulConstant((ulong)x, y, out var high);
                return (nuint)high;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ulong BigMulConstant(ulong x, uint y, out ulong low)
        {
            if (y < 2)
            {
                low = y > 0 ? x : 0;
                return 0;
            }
            if (BitOperations.IsPow2(y))
            {
                var a = BitOperations.TrailingZeroCount(y);
                var hi = x >> -a;
                var lo = x << a;
                low = lo;
                return hi;
            }
            else
            {
                ulong a0 = (uint)x;
                var a1 = x >> 32;
                var z0 = a0 * y;
                var z1 = a1 * y;
                var z1a = z1 + (z0 >> 32);
                var z1b = z1a >> 32;
                z1a <<= 32;
                var z2 = z1b;
                z0 = (uint)z0 | z1a;
                low = z0;
                return z2;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint BigMulConstant(nuint x, nuint y, out nuint low)
        {
            if (nuint.MaxValue == uint.MaxValue)
            {
                ulong m = x;
                m *= y;
                low = (nuint)m;
                return (nuint)(m >> 32);
            }
            if ((y & (y - 1)) == 0)
            {
                var tzc = BitOperations.TrailingZeroCount(y);
                var hi = tzc > 0 ? x : 0;
                hi >>= -tzc;
                var lo = x << tzc;
                low = lo;
                return hi;
            }
            return BigMulSlow(x, y, out low);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint BigMulSlow(nuint x, nuint y, out nuint low)
        {
            unchecked
            {
                var halfLength = BitOperations.LeadingZeroCount(nuint.MinValue) >>> 1;
                var halfMask = ((nuint)1 << halfLength) - 1;
                nuint a0 = halfMask & x;
                var a1 = x >> halfLength;
                nuint b0 = halfMask & y;
                var b1 = y >> halfLength;
                var z1a = a0 * b1;
                var z1b = a1 * b0;
                var z0 = a0 * b0;
                var z1 = z1a + z1b;
                z1b = z1a > z1 ? halfMask + 1 : 0;
                var z2 = a1 * b1;
                z1a = z1 + (z0 >> halfLength);
                z2 += z1b;
                z1b = z1a >> halfLength;
                z1a <<= halfLength;
                z2 += z1b;
                z0 = (halfMask & z0) | z1a;
                low = z0;
                return z2;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint CalculateRationalMultiplyConstant(nuint value, nuint numerator, nuint denominator)
        {
            if (numerator == denominator)
            {
                return value;
            }
            var tz = BitOperations.TrailingZeroCount(numerator | denominator);
            numerator >>= tz;
            denominator >>= tz;
            if (numerator >= denominator && numerator % denominator == 0)
            {
                return MultiplyCheckedConstant(value, numerator / denominator);
            }
            if (denominator >= numerator && denominator % numerator == 0)
            {
                return value / (denominator / numerator);
            }
            if (denominator >= numerator)
            {
                (var q, var r) = nuint.DivRem(value, denominator);
                r *= numerator;
                r /= denominator;
                q *= numerator;
                q += r;
                return q;
            }
            var high = BigMulConstant(value, numerator, out var low);
            return BigDivConstant(high, low, denominator);
        }

        [DoesNotReturn]
        internal static nuint ThrowOverflowException()
        {
            _ = checked(nuint.MaxValue * nuint.MaxValue);
            throw new OverflowException();
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
            var r0 = olen < length;
            var r1 = i < olen;
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
            var newLength = range - length;
            var y = start <= newLength;
            return length <= range && y;
        }

        /// <summary>
        /// Checks if the sliced range consists of <paramref name="start"/> and <paramref name="length"/> is in between 0 and <paramref name="range"/>.
        /// </summary>
        /// <param name="range">The exclusive upper limit of the range.</param>
        /// <param name="start">The starting point of the sliced range.</param>
        /// <param name="length">The length of the sliced range.</param>
        /// <returns><see langword="true"/> if the sliced range consists of <paramref name="start"/> and <paramref name="length"/> is in between 0 and <paramref name="range"/>, <see langword="false"/> otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool IsRangeInRange(uint range, uint start, uint length)
        {
            if (Unsafe.SizeOf<nuint>() <= sizeof(uint)) return IsRangeInRange((nuint)range, start, length);
            nuint end = (nuint)start + length;
            return end <= range;
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

        #region Bit Manipulation

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint RoundDownToPowerOfTwo(nuint value)
        {
            var y = value > 0 ? (nuint)1: 0;
            return y << ~BitOperations.LeadingZeroCount(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint RoundDownToPowerOfTwo(nuint value, out int exponent)
        {
            var y = value > 0 ? (nuint)1 : 0;
            var m = BitOperations.Log2(value);
            exponent = m;
            return y << m;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static uint CeilLowerBits(uint value, int bits)
        {
            var y = (uint)1 << bits;
            y--;
            value += y;
            return value & ~y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static nuint CeilLowerBits(nuint value, int bits)
        {
            var y = (nuint)1 << bits;
            y--;
            value += y;
            return value & ~y;
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
