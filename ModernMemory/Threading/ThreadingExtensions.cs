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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreFence()
        {
            if (Sse.IsSupported)
            {
                Sse.StoreFence();
                return;
            }
            Interlocked.MemoryBarrier();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LoadFence()
        {
            if (Sse2.IsSupported)
            {
                Sse2.LoadFence();
                return;
            }
            Interlocked.MemoryBarrier();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MemoryFence()
        {
            if (Sse2.IsSupported)
            {
                Sse2.MemoryFence();
                return;
            }
            Interlocked.MemoryBarrier();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        public static bool GetIsFastYieldAvailable() => Environment.ProcessorCount > 1 && (X86Base.IsSupported || ArmBase.IsSupported);
    }
}
