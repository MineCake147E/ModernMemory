using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace ModernMemory.Tests
{
    [TestFixture]
    public class NativeArrayTests
    {
        private static IEnumerable<nuint> LengthValues
        {
            get
            {
                yield return 65536;
                var totalAvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                if (Environment.Is64BitProcess && totalAvailableMemoryBytes > uint.MaxValue)
                {
                    yield return (nuint)(0x2000_0000_0000_0000ul >> BitOperations.LeadingZeroCount((ulong)totalAvailableMemoryBytes));
                }
            }
        }
        private static IEnumerable<bool> BoolValues
        {
            get
            {
                yield return true;
                yield return false;
            }
        }

        private static IEnumerable<byte> AlignmentExponentValues
        {
            get
            {
                for (var i = 0; i < 6; i++)
                {
                    yield return (byte)i;
                }
            }
        }

        private static void NativeArrayConstructsCorrectly<T>(nuint lengthInBytes, byte alignmentExponent) where T : unmanaged
            => Assert.Multiple(() =>
            {
                var realLength = lengthInBytes / (nuint)Unsafe.SizeOf<T>();
                var narr = new NativeArray<T>(realLength, alignmentExponent);
                Assert.That(narr.Length, Is.EqualTo(realLength), "Checking Length");
                var expectedAlignment = (nuint)1 << alignmentExponent;
                Assert.That(narr.RequestedAlignment, Is.EqualTo(expectedAlignment), "Checking RequestedAlignment");
                Assert.That(narr.CurrentAlignment, Is.GreaterThanOrEqualTo(expectedAlignment), "Checking CurrentAlignment");
            });

        private static IEnumerable<TestCaseData> NativeArrayConstructsCorrectlyTestCaseSource
            => LengthValues.SelectMany(len => AlignmentExponentValues.Select(ae => new TestCaseData(len, ae)));

        [NonParallelizable]
        [TestCaseSource(nameof(NativeArrayConstructsCorrectlyTestCaseSource))]
        public void NativeArrayConstructsCorrectlyForInt(nuint lengthInBytes, byte alignmentExponent) => NativeArrayConstructsCorrectly<int>(lengthInBytes, alignmentExponent);

        [NonParallelizable]
        [TestCaseSource(nameof(NativeArrayConstructsCorrectlyTestCaseSource))]
        public void NativeArrayConstructsCorrectlyForGuid(nuint lengthInBytes, byte alignmentExponent) => NativeArrayConstructsCorrectly<Guid>(lengthInBytes, alignmentExponent);
    }
}
