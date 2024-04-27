using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Sorting;

namespace ModernMemory.Tests.Sorting
{
    public interface ISequencePermutationProvider<TParameter>
    {
        static abstract void Permute<T>(Span<T> destination, TParameter parameter);
    }

    public readonly struct IdentityPermutationProvider<TParameter> : ISequencePermutationProvider<TParameter>
    {
        public static void Permute<T>(Span<T> destination, TParameter parameter) {}
    }

    public readonly struct ReversePermutationProvider<TParameter> : ISequencePermutationProvider<TParameter>
    {
        public static void Permute<T>(Span<T> destination, TParameter parameter) => destination.Reverse();
    }

    public readonly struct RotatedPermutationProvider : ISequencePermutationProvider<int>
    {
        public static void Permute<T>(Span<T> destination, int parameter)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)parameter, (uint)destination.Length);
            NativeMemoryUtils.Rotate(ref MemoryMarshal.GetReference(destination), (nuint)parameter, (nuint)(destination.Length - parameter));
        }
    }

    public readonly struct RandomPermutationProvider : ISequencePermutationProvider<int>
    {
        public static void Permute<T>(Span<T> destination, int parameter) => RandomNumberGenerator.Shuffle(destination);
    }
}
