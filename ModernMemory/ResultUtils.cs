using System.Reflection.Emit;

namespace ModernMemory
{
    public static partial class ResultUtils
    {
        public static bool IsSuccess<TResult, TDetails>(this TResult result)
            where TDetails : unmanaged
            where TResult : struct, IResultSummary<TResult, TDetails>
            => result.Level.IsSuccess();

        public static bool IsError<TResult, TDetails>(this TResult result)
            where TDetails : unmanaged
            where TResult : struct, IResultSummary<TResult, TDetails>
            => result.Level.IsError();

        public static void ThrowIfThrowActionIsNotNull<TResult, TDetails>(this TResult result)
            where TDetails : unmanaged
            where TResult : struct, IResultSummary<TResult, TDetails>
        {
            if (result.ThrowAction is { } e)
            {
                e();
            }
        }

        public static void ThrowIfNoSuccess<TResult, TDetails>(this TResult result)
            where TDetails : unmanaged
            where TResult : struct, IResultSummary<TResult, TDetails>
        {
            if (!result.Level.IsSuccess())
            {
                result.ThrowIfThrowActionIsNotNull<TResult, TDetails>();
            }
        }

        public static void ThrowIfCatastrophic<TResult, TDetails>(this TResult result)
            where TDetails : unmanaged
            where TResult : struct, IResultSummary<TResult, TDetails>
        {
            if (result.Level is ResultLevel.CatastrophicError)
            {
                result.ThrowIfThrowActionIsNotNull<TResult, TDetails>();
            }
        }
    }
}
