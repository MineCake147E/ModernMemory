using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Tests
{
    [TestFixture]
    public class NativeSpanTests
    {
        private static IEnumerable<(Type type, object? value)> TypedSpecimen() => CommonTestCaseSources.TypedSpecimen();

        private static IEnumerable<TestCaseData> SpecimenAndSizeTestCaseSource() => TypedSpecimen().Select(a => new TestCaseData(a.value, 127));

        #region Fill and Clear

        [TestCaseSource(nameof(SpecimenAndSizeTestCaseSource))]
        public void FillFillsCorrectly<T>(T value, int size)
        {
            var array = new T[size * 2];
            Span<T> span = array;
            span.Clear();
            var expArray = new T[size * 2];
            Span<T> eSpan = expArray;
            eSpan.Clear();
            eSpan.Slice(size / 2, size).Fill(value);
            var nativeSpan = span.Slice(size / 2, size).AsNativeSpan();
            nativeSpan.Fill(value);
            Assert.That(array, Is.EqualTo(expArray));
        }

        [TestCaseSource(nameof(SpecimenAndSizeTestCaseSource))]
        public void ClearClearsCorrectly<T>(T value, int size)
        {
            var array = new T[size * 2];
            Span<T> span = array;
            span.Fill(value);
            var expArray = new T[size * 2];
            Span<T> eSpan = expArray;
            eSpan.Fill(value);
            eSpan.Slice(size / 2, size).Clear();
            var nativeSpan = span.Slice(size / 2, size).AsNativeSpan();
            nativeSpan.Clear();
            Assert.That(array, Is.EquivalentTo(expArray));
        }
        #endregion

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
        public void CollectionSpreadWorksCorrectly()
        {
            var q = Enumerable.Range(0, 999);
            NativeSpan<int> a = [.. q];
            List<int> b = [.. a];
            Assert.That(b, Is.EquivalentTo(q));
        }

        [Test]
        public void CollectionLiteralWorksCorrectly()
        {
            List<int> q = [0, 1, 2, 3];
            NativeSpan<int> a = [0, 1, 2, 3];   // TODO: Analyzer should detect all-constant cases (better using new([...]))
            List<int> b = [.. a];
            Assert.That(b, Is.EquivalentTo(q));
        }

        [Test]
        public void CollectionLiteralConstructorWorksCorrectly()
        {
            List<int> q = [0, 1, 2, 3];
            NativeSpan<int> a = new([..q]);
            List<int> b = [.. a];
            Assert.That(b, Is.EquivalentTo(q));
        }
    }
}
