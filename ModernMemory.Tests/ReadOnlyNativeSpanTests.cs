using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Tests
{
    [TestFixture]
    public class ReadOnlyNativeSpanTests
    {
        private static IEnumerable<(Type type, object? value)> TypedSpecimen() => CommonTestCaseSources.TypedSpecimen();

        private static IEnumerable<TestCaseData> SpecimenAndSizeTestCaseSource() => TypedSpecimen().Select(a => new TestCaseData(a.value, 127));

        [TestCaseSource(nameof(SpecimenAndSizeTestCaseSource))]
        public void TryCopyToCopiesCorrectly<T>(T value, int size)
            => Assert.Multiple(() =>
        {
            var array = new T[size * 2];
            Span<T> span = array;
            span.Clear();
            var expArray = new T[size * 2];
            NativeSpan<T> eSpan = expArray;
            eSpan.Clear();
            var sSpan = eSpan.Slice((nuint)size / 2, (nuint)size);
            sSpan.Fill(value);
            var nativeSpan = span.Slice(size / 2, size).AsNativeSpan();
            Assert.That(((ReadOnlyNativeSpan<T>)sSpan).TryCopyTo(nativeSpan), Is.True);
            Assert.That(array, Is.EquivalentTo(expArray));
        });

        #region Slice

        private static IEnumerable<TestCaseData> SliceSlicesCorrectlyTestCaseSource()
    => TypedSpecimen().SelectMany(a => CommonTestCaseSources.SliceSlicesCorrectlyTestCaseValues()
    .Select(b => new TestCaseData(a.value, b.size, b.start, b.length)));

        [TestCaseSource(nameof(SliceSlicesCorrectlyTestCaseSource))]
        public void SliceSlicesCorrectly<T>(T value, int size, nuint start, nuint length)
        {
            var array = new T[size];
            array.AsSpan().Fill(value);
            var ns = array.AsNativeSpan();
            var act = ns.Slice(start, length);
            ref var a = ref NativeMemoryUtils.GetReference(ns);
            ref var b = ref NativeMemoryUtils.GetReference(act);
            var offset = (nuint)Unsafe.ByteOffset(ref a, ref b);
            var actualLength = act.Length;
            Assert.Multiple(() =>
            {
                Assert.That(offset, Is.EqualTo(start * (nuint)Unsafe.SizeOf<T>()));
                Assert.That(actualLength, Is.EqualTo(length));
            });
        }
        #endregion

        [Test]
        public void CorrectlyRecognizedAsCollection()
        {
            var q = Enumerable.Range(0, 999);
            ReadOnlyNativeSpan<int> a = [.. q];
            List<int> b = [.. a];
            Assert.That(b, Is.EquivalentTo(q));
        }
    }
}
