using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Threading
{
    public struct SpinLockedContainer<T>
    {
        private ValueSpinLockSlim spinLock;
        private T value;
        public T Value { get; set; }
    }
}
