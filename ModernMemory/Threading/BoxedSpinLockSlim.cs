using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Threading
{
    public sealed class BoxedSpinLockSlim
    {
        private ValueSpinLockSlim value = default;
        public ref ValueSpinLockSlim Lock => ref value;

        public ValueSpinLockSlim.AcquiredLock Enter() => value.Enter();

        public ValueSpinLockSlim.AcquiredLock TryEnter() => value.TryEnter();

        public ValueSpinLockSlim.AcquiredLock TryEnterBySpin(ulong allowedRetries = 0) => value.TryEnterBySpin(allowedRetries);
    }
}
