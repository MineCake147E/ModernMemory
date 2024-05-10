using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Tests.Buffers
{
    [TestFixture]
    public class FixedArrayTests
    {
        [Test]
        public void FixedArrayLocalPropagatesValuesCorrectly()
        {
            Unsafe.SkipInit(out FixedArray128<ulong> v);
            Span<ulong> vs = v;
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(vs));
            NativeSpan<ulong> a = v;
            Assert.That(a.ToArray(), Is.EqualTo(vs.ToArray()));
        }

        FixedArray128<ulong> fixedArray;

        [Test]
        public void FixedArrayMemberPropagatesValuesCorrectly()
        {
            Span<ulong> vs = fixedArray;
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(vs));
            NativeSpan<ulong> a = fixedArray;
            Assert.That(a.ToArray(), Is.EqualTo(vs.ToArray()));
        }

        [Test]
        public void FixedArrayMemberRefPropagatesValuesCorrectly()
        {
            ref var v = ref fixedArray;
            Span<ulong> vs = v;
            RandomNumberGenerator.Fill(MemoryMarshal.AsBytes(vs));
            NativeSpan<ulong> a = v;
            Assert.That(a.ToArray(), Is.EqualTo(vs.ToArray()));
        }
    }
}
