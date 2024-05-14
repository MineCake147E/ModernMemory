using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Threading;

namespace ModernMemory.Tests
{
    [TestFixture]
    public class SpinLockSlimTests
    {
        [Test]
        public void SpinLockCorrectlyEnters()
        {
            ValueSpinLockSlim spinLock = new();
            using var q = spinLock.Enter();
            var handleValid = q.IsValid;
            var handleHeld = q.IsHolding;
            var valueLockHeld = spinLock.IsHeld;
            Assert.Multiple(() =>
            {
                Assert.That(handleValid);
                Assert.That(handleHeld);
                Assert.That(valueLockHeld);
            });
        }

        [Test]
        public void SpinLockCorrectlyEntersAndExits()
        {
            ValueSpinLockSlim spinLock = new();
            var q = spinLock.Enter();
            var handleValidBeforeExit = q.IsValid;
            var handleHeldBeforeExit = q.IsHolding;
            var valueLockHeldBeforeExit = spinLock.IsHeld;
            q.Dispose();
            var handleValidAfterExit = q.IsValid;
            var handleHeldAfterExit = q.IsHolding;
            var valueLockHeldAfterExit = spinLock.IsHeld;
            Assert.Multiple(() =>
            {
                Assert.That(handleValidBeforeExit);
                Assert.That(handleHeldBeforeExit);
                Assert.That(valueLockHeldBeforeExit);
                Assert.That(handleValidAfterExit);
                Assert.That(!handleHeldAfterExit);
                Assert.That(!valueLockHeldAfterExit);
            });
        }

        [Test]
        public void SpinLockCorrectlyEntersAndPreventsOtherEnter()
        {
            ManualResetEventSlim mres = new(false);
            ManualResetEventSlim mres2 = new(false);
            StrongBox<ValueSpinLockSlim> boxedLock = new(new ValueSpinLockSlim());
            var sw = new Stopwatch();
            var q = Task.Run(() =>
            {
                var l = boxedLock.Enter();
                mres.Set();
                mres2.Wait();
                var e0 = sw.Elapsed;
                l.Dispose();
                var e1 = sw.Elapsed;
                Console.WriteLine($"[Task] e0: {e0}");
                Console.WriteLine($"[Task] e1: {e1}");
            });
            sw.Start();
            Assert.Multiple(() =>
            {
                mres.Wait();
                Assert.That(boxedLock.Value.IsHeld);
                var tryEnterHandle = boxedLock.TryEnter();
                Assert.That(!tryEnterHandle.IsValid);
                Assert.That(!tryEnterHandle.IsHolding);
                var e0 = sw.Elapsed;
                mres2.Set();
                var e1 = sw.Elapsed;
                using var l = boxedLock.Enter();
                var e2 = sw.Elapsed;
                Assert.That(l.IsHolding);
                Console.WriteLine($"[Main] e0: {e0}");
                Console.WriteLine($"[Main] e1: {e1}");
                Console.WriteLine($"[Main] e2: {e2}");
            });
            q.Wait();
        }
    }
}
