using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    public static class ThrowHelper
    {
        /// <summary>
        /// Throws the specified exception.
        /// </summary>
        /// <typeparam name="T">The type of exception.</typeparam>
        /// <param name="exception">The exception.</param>
        [DoesNotReturn]
        public static void Throw<T>(this T exception) where T : Exception => throw exception;

        /// <summary>
        /// Throws the specified exception.
        /// </summary>
        /// <typeparam name="T">The type of exception.</typeparam>
        /// <param name="exception">The exception.</param>
        [DoesNotReturn]
        public static void Throw<T>(this Lazy<T> exception) where T : Exception => throw exception.Value;

        /// <summary>
        /// Throws a new instance of <see cref="IndexOutOfRangeException"/>.
        /// </summary>
        [DoesNotReturn]
        [DebuggerNonUserCode]
        public static void ThrowIndexOutOfRangeException() => throw new IndexOutOfRangeException();

        [DebuggerHidden]
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        [Conditional("DEBUG")]
        private static void SetIfDebug(ref bool result) => result = true;

        [DebuggerNonUserCode]
        public static bool IsDebugDefined
        {
            [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
            get
            {
                var y = false;
                SetIfDebug(ref y);
                return y;
            }
        }

        /// <summary>
        /// Runs an <paramref name="action"/> only in the DEBUG mode.
        /// </summary>
        /// <param name="action">The <see cref="Action"/> to run if in debug mode.</param>
        [Conditional("DEBUG")]
        public static void RunIfDebug(Action action) => action();
    }
}
