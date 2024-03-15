using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Tests
{
    [TestFixture]
    public class MathUtilsTests
    {
        [TestCase(0u, 1u, 0u)]
        [TestCase(1u, 1u, 0u)]
        [TestCase(2u, 1u, 1u)]
        [TestCase(2u, 3u, 0u)]
        [TestCase(2u, 0u, 2u)]
        public void SubtractSaturateCalculatesCorrectlyUIntPtr(uint left, uint right, uint expected)
            => Assert.That(MathUtils.SubtractSaturate(left, right), Is.EqualTo((nuint)expected));

        [TestCase(0u, 1, 0u)]
        [TestCase(1u, 1, 0u)]
        [TestCase(2u, 1, 1u)]
        [TestCase(2u, 3, 0u)]
        [TestCase(2u, 0, 2u)]
        public void SubtractSaturateCalculatesCorrectlyIntPtr(uint left, int right, uint expected)
            => Assert.That(MathUtils.SubtractSaturate(left, right), Is.EqualTo((nuint)expected));

        [TestCase(0u, 1, false)]
        [TestCase(1u, 1, false)]
        [TestCase(2u, 1, false)]
        [TestCase(2u, 3, false)]
        [TestCase(2u, 0, false)]
        [TestCase(2u, -1, true)]
        public void SubtractSaturateThrowsCorrectly(uint left, int right, bool throwsIfDebug)
        {
            nuint result;
            if (throwsIfDebug && ThrowHelper.IsDebugDefined)
            {
                Assert.Throws(typeof(ArgumentOutOfRangeException), () => result = MathUtils.SubtractSaturate(left, right));
            }
            else
            {
                Assert.DoesNotThrow(() => result = MathUtils.SubtractSaturate(left, right));
            }
        }
    }
}
