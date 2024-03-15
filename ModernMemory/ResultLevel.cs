using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    [Flags]
    public enum ResultLevel
    {
        None = 0,
        Success = 1,
        RecoverableError = 1 << 1,

        CatastrophicError = None,
        FullSuccess = Success,
        PartialSuccess = Success | RecoverableError
    }

    public static partial class ResultUtils
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool IsSuccess(this ResultLevel level) => (level & ResultLevel.Success) != 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool IsNotSuccess(this ResultLevel level) => !level.IsSuccess();

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool IsError(this ResultLevel level) => level != ResultLevel.FullSuccess;
    }
}
