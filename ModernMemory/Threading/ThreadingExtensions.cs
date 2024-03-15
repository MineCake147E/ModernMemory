using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Threading
{
    public static class ThreadingExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Yield()
        {
            if (X86Base.IsSupported)
            {
                X86Base.Pause();
            }
            else if (ArmBase.IsSupported)
            {
                ArmBase.Yield();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool GetIsFastYieldAvailable() => Environment.ProcessorCount > 0 && (X86Base.IsSupported || ArmBase.IsSupported);
    }
}
