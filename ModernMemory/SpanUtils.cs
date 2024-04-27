using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    /*
     * Transplant from Shamisen, and a little bit of optimization.
     * MIT License
     *
     * Copyright (c) 2022 ちーず(・8・)けーき
     *
     * Permission is hereby granted, free of charge, to any person obtaining a copy
     * of this software and associated documentation files (the "Software"), to deal
     * in the Software without restriction, including without limitation the rights
     * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
     * copies of the Software, and to permit persons to whom the Software is
     * furnished to do so, subject to the following conditions:
     *
     * The above copyright notice and this permission notice shall be included in all
     * copies or substantial portions of the Software.
     *
     * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
     * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
     * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
     * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
     * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
     * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
     * SOFTWARE.
     */
    public static class SpanUtils
    {
        /// <summary>
        /// Slices the specified <paramref name="span"/> to the specified <paramref name="maxLength"/> if the <paramref name="span"/> is longer than the <paramref name="maxLength"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="span">The span.</param>
        /// <param name="maxLength">The maximum length.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static Span<T> SliceWhileIfLongerThan<T>(this Span<T> span, int maxLength)
        {
            var res = span;
            if ((uint)span.Length > (uint)maxLength) res = span.Slice(0, maxLength);
            return res;
        }

        /// <summary>
        /// Slices the specified <paramref name="span"/> to the specified <paramref name="maxLength"/> if the <paramref name="span"/> is longer than the <paramref name="maxLength"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="span">The span.</param>
        /// <param name="maxLength">The maximum length.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static ReadOnlySpan<T> SliceWhileIfLongerThan<T>(this ReadOnlySpan<T> span, int maxLength)
        {
            var res = span;
            if ((uint)span.Length > (uint)maxLength) res = span.Slice(0, maxLength);
            return res;
        }
    }
}
