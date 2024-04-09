using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Utils
{
    public readonly struct CompactPowerOfTwoNumber<TStorage>
        where TStorage : IBinaryNumber<TStorage>, IUnsignedNumber<TStorage>
    {
        readonly TStorage storage;

        public CompactPowerOfTwoNumber(TStorage storage)
        {
            this.storage = storage;
        }

        public static implicit operator nuint(CompactPowerOfTwoNumber<TStorage> value)
        {
            nuint k = 1;
            var exp = (int)uint.CreateTruncating(value.storage);
            k <<= exp;
            return k;
        }
    }
}
