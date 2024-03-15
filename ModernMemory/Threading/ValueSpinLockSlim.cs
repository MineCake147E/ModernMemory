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
    public struct ValueSpinLockSlim
    {
        internal uint lockField;

        public readonly bool IsHeld
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => lockField > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Exit() => lockField = 0;

        public readonly ref struct AcquiredLock
        {
            private readonly ref ValueSpinLockSlim spinLock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal AcquiredLock(ref ValueSpinLockSlim spinLock)
            {
                this.spinLock = ref spinLock;
            }

            internal static AcquiredLock Empty
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new(ref Unsafe.NullRef<ValueSpinLockSlim>());
            }

            public bool IsValid
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => !Unsafe.IsNullRef(ref spinLock);
            }

            public bool IsHeld
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    var t = this;
                    var res = false;
                    if (!Unsafe.IsNullRef(ref t.spinLock)) res = t.spinLock.IsHeld;
                    return res;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                var t = this;
                if (!Unsafe.IsNullRef(ref t.spinLock)) t.spinLock.Exit();
            }
        }
    }
    public static class ValueSpinLockSlimExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueSpinLockSlim.AcquiredLock Enter(this ref ValueSpinLockSlim spinLock)
        {
            if (ThreadingExtensions.GetIsFastYieldAvailable())
            {
                return EnterFallback(ref spinLock);
            }
            while (true)
            {
                var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
                if (exchanged != 0)
                {
                    do
                    {
                        ThreadingExtensions.Yield();
                    } while (Volatile.Read(ref spinLock.lockField) != 0);
                }
                else
                {
                    return new(ref spinLock);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueSpinLockSlim.AcquiredLock Enter(this StrongBox<ValueSpinLockSlim> spinLock) => spinLock.Value.Enter();

        private static ValueSpinLockSlim.AcquiredLock EnterFallback(ref ValueSpinLockSlim spinLock)
        {
            SpinWait sw = new();
            while (true)
            {
                var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
                if (exchanged != 0)
                {
                    do
                    {
                        sw.SpinOnce();
                    } while (Volatile.Read(ref spinLock.lockField) != 0);
                    sw.Reset();
                }
                else
                {
                    return new(ref spinLock);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueSpinLockSlim.AcquiredLock TryEnter(this ref ValueSpinLockSlim spinLock)
        {
            var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
            return exchanged == 0 ? new(ref spinLock) : ValueSpinLockSlim.AcquiredLock.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueSpinLockSlim.AcquiredLock TryEnter(this StrongBox<ValueSpinLockSlim> spinLock) => spinLock.Value.TryEnter();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueSpinLockSlim.AcquiredLock TryEnterBySpin(this ref ValueSpinLockSlim spinLock, ulong allowedRetries = 0)
        {
            if (ThreadingExtensions.GetIsFastYieldAvailable())
            {
                return spinLock.TryEnterBySpinFallback(allowedRetries);
            }
            do
            {
                var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
                if (exchanged == 0)
                {
                    return new(ref spinLock);
                }
                else if (allowedRetries > 0)
                {
                    do
                    {
                        allowedRetries--;
                        ThreadingExtensions.Yield();
                    } while (allowedRetries > 0 && Volatile.Read(ref spinLock.lockField) != 0);
                }
            } while (allowedRetries > 0);
            return ValueSpinLockSlim.AcquiredLock.Empty;
        }

        private static ValueSpinLockSlim.AcquiredLock TryEnterBySpinFallback(this ref ValueSpinLockSlim spinLock, ulong allowedRetries = 0)
        {
            SpinWait sw = new();
            do
            {
                var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
                if (exchanged == 0)
                {
                    return new(ref spinLock);
                }
                else if (allowedRetries > 0)
                {
                    do
                    {
                        allowedRetries--;
                        sw.SpinOnce();
                    } while (allowedRetries > 0 && Volatile.Read(ref spinLock.lockField) != 0);
                    sw.Reset();
                }
            } while (allowedRetries > 0);
            return ValueSpinLockSlim.AcquiredLock.Empty;
        }
    }
}
