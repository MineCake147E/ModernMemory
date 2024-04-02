using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Tests.NativeMemoryExtensions
{
    [TestFixture]
    public class SwapTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(64)]
        [TestCase(65)]
        [TestCase(2048)]
        [TestCase(2049)]
        public void SwapSwapsStrongBoxesCorrectly(int size)
        {
            var k = new StrongBox<int>[size];
            var g = new StrongBox<int>[k.Length];
            var ks = k.AsSpan();
            for (int i = 0; i < ks.Length; i++)
            {
                ks[i] = new(i);
            }
            var gs = g.AsSpan();
            for (int i = 0; i < gs.Length; i++)
            {
                gs[i] = new(i + size);
            }
            var eK = gs.ToArray();
            var eG = ks.ToArray();
            NativeMemoryUtils.SwapValues(ref MemoryMarshal.GetReference(ks), ref MemoryMarshal.GetReference(gs), (nuint)ks.Length);
            Assert.Multiple(() =>
            {
                Assert.That(k.Select(a => a.Value).ToList(), Is.EqualTo(eK.Select(a => a.Value).ToList()));
                Assert.That(g.Select(a => a.Value).ToList(), Is.EqualTo(eG.Select(a => a.Value).ToList()));
            });
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(64)]
        [TestCase(65)]
        [TestCase(2048)]
        [TestCase(2049)]
        public void SwapSwapsValuesCorrectly(int size)
        {
            var k = new int[size];
            var g = new int[k.Length];
            var ks = k.AsSpan();
            for (int i = 0; i < ks.Length; i++)
            {
                ks[i] = i;
            }
            var gs = g.AsSpan();
            for (int i = 0; i < gs.Length; i++)
            {
                gs[i] = i + size;
            }
            var eK = gs.ToArray();
            var eG = ks.ToArray();
            NativeMemoryUtils.SwapValues(ref MemoryMarshal.GetReference(ks), ref MemoryMarshal.GetReference(gs), (nuint)ks.Length);
            Assert.Multiple(() =>
            {
                Assert.That(k, Is.EqualTo(eK));
                Assert.That(g, Is.EqualTo(eG));
            });
        }
    }
}
