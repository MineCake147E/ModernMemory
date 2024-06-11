using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Tests.Utils
{
    public readonly struct BinaryIntegerSequenceProvider<T> : ISequenceProvider<T>
        where T : IBinaryNumber<T>
    {
        public static void GenerateSequence(NativeSpan<T> destination)
        {
            var value = T.Zero;
            for (nuint i = 0; i < destination.Length; i++)
            {
                destination[i] = value;
                value += T.One;
            }
        }
    }

    public readonly struct CharSequenceProvider : ISequenceProvider<char>
    {
        public static void GenerateSequence(NativeSpan<char> destination)
        {
            var value = char.MinValue;
            for (nuint i = 0; i < destination.Length; i++)
            {
                destination[i] = value;
                value++;
            }
        }
    }
}
