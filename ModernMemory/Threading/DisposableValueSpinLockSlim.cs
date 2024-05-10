using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Threading
{
    public struct DisposableValueSpinLockSlim : IDisposable
    {
        internal int lockField = 0;

        public DisposableValueSpinLockSlim()
        {
        }

        public readonly bool IsDisposed
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(in lockField) == -1;
        }

        public readonly bool IsHeld
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(in lockField) > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Exit() => Interlocked.CompareExchange(ref lockField, 0, 1);

        public void Dispose() => Interlocked.CompareExchange(ref lockField, -1, 1);

        public ref struct AcquiredLock
        {
            private ref DisposableValueSpinLockSlim spinLock;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal AcquiredLock(ref DisposableValueSpinLockSlim spinLock)
            {
                this.spinLock = ref spinLock;
            }

            internal static AcquiredLock Empty
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new(ref Unsafe.NullRef<DisposableValueSpinLockSlim>());
            }

            public readonly bool IsValid
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => !Unsafe.IsNullRef(in spinLock);
            }

            public readonly bool IsHeld
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
                ref var t = ref spinLock;
                if (!Unsafe.IsNullRef(ref t))
                {
                    spinLock = ref Unsafe.NullRef<DisposableValueSpinLockSlim>();
                    t.Exit();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void DisposeLock()
            {
                ref var t = ref spinLock;
                if (!Unsafe.IsNullRef(ref t))
                {
                    spinLock = ref Unsafe.NullRef<DisposableValueSpinLockSlim>();
                    t.Dispose();
                }
            }
        }
    }

    public static class DisposableValueSpinLockSlimExtensions
    {
        [DoesNotReturn]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static DisposableValueSpinLockSlim.AcquiredLock ThrowDisposed(ref readonly DisposableValueSpinLockSlim spinLock) => throw new ObjectDisposedException(nameof(spinLock));

        /// <exception cref="ObjectDisposedException">The <paramref name="spinLock"/> is disposed.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DisposableValueSpinLockSlim.AcquiredLock Enter(this ref DisposableValueSpinLockSlim spinLock)
        {
            if (!ThreadingExtensions.GetIsFastYieldAvailable())
            {
                return EnterFallback(ref spinLock);
            }
            while (true)
            {
                var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
                if (exchanged == 0)
                {
                    return new(ref spinLock);
                }
                while (exchanged > 0)
                {
                    ThreadingExtensions.Yield();
                    exchanged = Volatile.Read(ref spinLock.lockField);
                }
                if (exchanged < 0)
                {
                    return ThrowDisposed(ref spinLock);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DisposableValueSpinLockSlim.AcquiredLock Enter(this ref DisposableValueSpinLockSlim spinLock, out bool isDisposed)
        {
            if (!ThreadingExtensions.GetIsFastYieldAvailable())
            {
                return EnterFallback(ref spinLock, out isDisposed);
            }
            while (true)
            {
                var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
                if (exchanged == 0)
                {
                    isDisposed = false;
                    return new(ref spinLock);
                }
                while (exchanged > 0)
                {
                    ThreadingExtensions.Yield();
                    exchanged = Volatile.Read(ref spinLock.lockField);
                }
                if (exchanged < 0)
                {
                    isDisposed = true;
                    return DisposableValueSpinLockSlim.AcquiredLock.Empty;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DisposableValueSpinLockSlim.AcquiredLock Enter(this StrongBox<DisposableValueSpinLockSlim> spinLock) => spinLock.Value.Enter();

        private static DisposableValueSpinLockSlim.AcquiredLock EnterFallback(ref DisposableValueSpinLockSlim spinLock)
        {
            SpinWait sw = new();
            while (true)
            {
                var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
                if (exchanged != 0)
                {
                    while (exchanged > 0)
                    {
                        sw.SpinOnce();
                        exchanged = Volatile.Read(ref spinLock.lockField);
                    }
                    if (exchanged < 0)
                    {
                        return ThrowDisposed(ref spinLock);
                    }
                    sw.Reset();
                }
                else
                {
                    return new(ref spinLock);
                }
            }
        }

        private static DisposableValueSpinLockSlim.AcquiredLock EnterFallback(ref DisposableValueSpinLockSlim spinLock, out bool isDisposed)
        {
            SpinWait sw = new();
            while (true)
            {
                var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
                if (exchanged != 0)
                {
                    while (exchanged > 0)
                    {
                        sw.SpinOnce();
                        exchanged = Volatile.Read(ref spinLock.lockField);
                    }
                    if (exchanged < 0)
                    {
                        isDisposed = true;
                        return DisposableValueSpinLockSlim.AcquiredLock.Empty;
                    }
                    sw.Reset();
                }
                else
                {
                    isDisposed = false;
                    return new(ref spinLock);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DisposableValueSpinLockSlim.AcquiredLock TryEnter(this ref DisposableValueSpinLockSlim spinLock)
        {
            var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
            return exchanged switch
            {
                > 0 => DisposableValueSpinLockSlim.AcquiredLock.Empty,
                0 => new(ref spinLock),
                _ => ThrowDisposed(ref spinLock)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DisposableValueSpinLockSlim.AcquiredLock TryEnter(this ref DisposableValueSpinLockSlim spinLock, out bool isDisposed)
        {
            var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
            isDisposed = exchanged < 0;
            return exchanged == 0 ? new(ref spinLock) : DisposableValueSpinLockSlim.AcquiredLock.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DisposableValueSpinLockSlim.AcquiredLock TryEnter(this StrongBox<DisposableValueSpinLockSlim> spinLock) => spinLock.Value.TryEnter();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DisposableValueSpinLockSlim.AcquiredLock TryEnter(this StrongBox<DisposableValueSpinLockSlim> spinLock, out bool isDisposed) => spinLock.Value.TryEnter(out isDisposed);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DisposableValueSpinLockSlim.AcquiredLock TryEnterBySpin(this ref DisposableValueSpinLockSlim spinLock, ulong allowedRetries = 0)
        {
            if (ThreadingExtensions.GetIsFastYieldAvailable())
            {
                return spinLock.TryEnterBySpinFallback(allowedRetries);
            }
            var remainingRetries = allowedRetries;
            do
            {
                var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
                if (exchanged == 0)
                {
                    return new(ref spinLock);
                }
                while (exchanged > 0 && --remainingRetries < allowedRetries)
                {
                    ThreadingExtensions.Yield();
                    exchanged = Volatile.Read(ref spinLock.lockField);
                }
                if (exchanged < 0)
                {
                    return ThrowDisposed(ref spinLock);
                }
            } while (--remainingRetries < allowedRetries);
            return DisposableValueSpinLockSlim.AcquiredLock.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DisposableValueSpinLockSlim.AcquiredLock TryEnterBySpin(this ref DisposableValueSpinLockSlim spinLock, out bool isDisposed, ulong allowedRetries = 0)
        {
            if (ThreadingExtensions.GetIsFastYieldAvailable())
            {
                return spinLock.TryEnterBySpinFallback(out isDisposed, allowedRetries);
            }
            var remainingRetries = allowedRetries;
            int exchanged;
            do
            {
                exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
                if (exchanged <= 0)
                {
                    break;
                }
                while (exchanged > 0 && --remainingRetries < allowedRetries)
                {
                    ThreadingExtensions.Yield();
                    exchanged = Volatile.Read(ref spinLock.lockField);
                }
                if (exchanged < 0)
                {
                    break;
                }
            } while (--remainingRetries < allowedRetries);
            isDisposed = exchanged < 0;
            return exchanged switch
            {
                0 => new(ref spinLock),
                _ => DisposableValueSpinLockSlim.AcquiredLock.Empty,
            };
        }

        private static DisposableValueSpinLockSlim.AcquiredLock TryEnterBySpinFallback(this ref DisposableValueSpinLockSlim spinLock, ulong allowedRetries = 0)
        {
            SpinWait sw = new();
            var remainingRetries = allowedRetries;
            do
            {
                var exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
                if (exchanged == 0)
                {
                    return new(ref spinLock);
                }
                do
                {
                    sw.SpinOnce();
                } while (--remainingRetries < allowedRetries && Volatile.Read(ref spinLock.lockField) > 0);
                if (exchanged < 0)
                {
                    return ThrowDisposed(ref spinLock);
                }
                sw.Reset();
            } while (--remainingRetries < allowedRetries);
            return DisposableValueSpinLockSlim.AcquiredLock.Empty;
        }

        private static DisposableValueSpinLockSlim.AcquiredLock TryEnterBySpinFallback(this ref DisposableValueSpinLockSlim spinLock, out bool isDisposed, ulong allowedRetries = 0)
        {
            SpinWait sw = new();
            var remainingRetries = allowedRetries;
            int exchanged;
            do
            {
                exchanged = Interlocked.CompareExchange(ref spinLock.lockField, 1, 0);
                if (exchanged <= 0)
                {
                    break;
                }
                while (exchanged > 0 && --remainingRetries < allowedRetries)
                {
                    sw.SpinOnce();
                    exchanged = Volatile.Read(ref spinLock.lockField);
                }
                sw.Reset();
                if (exchanged < 0)
                {
                    break;
                }
            } while (--remainingRetries < allowedRetries);
            isDisposed = exchanged < 0;
            return exchanged switch
            {
                0 => new(ref spinLock),
                _ => DisposableValueSpinLockSlim.AcquiredLock.Empty,
            };
        }
    }
}
