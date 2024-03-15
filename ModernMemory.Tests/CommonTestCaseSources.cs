using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Tests
{
    internal static class CommonTestCaseSources
    {
        private readonly record struct S3(byte V0, byte V1, byte V2);

        private readonly record struct S5(byte V0, byte V1, byte V2, byte V3, byte V4);
        private readonly record struct S6(byte V0, byte V1, byte V2, byte V3, byte V4, byte V5);
        private readonly record struct S7(byte V0, byte V1, byte V2, byte V3, byte V4, byte V5, byte V6);

        internal static IEnumerable<(Type type, object? value)> TypedSpecimen()
        {
            static (Type type, object? value) Specimen<T>(T value) => (typeof(T), value);
            yield return Specimen(sbyte.MaxValue);
            yield return Specimen(short.MaxValue);
            yield return Specimen(new S3(1, 2, 3));
            yield return Specimen(int.MaxValue);
            yield return Specimen(float.Pi);
            yield return Specimen(new S5(1, 2, 3, 4, 5));
            yield return Specimen(new S6(1, 2, 3, 4, 5, 6));
            yield return Specimen(new S7(1, 2, 3, 4, 5, 6, 7));
            yield return Specimen(long.MaxValue);
            yield return Specimen(double.Pi);
            yield return Specimen(Guid.Parse("73169808-e6b9-49d9-8702-0f20a37d8bcb"));
            yield return Specimen("A quick brown fox jumps over the lazy dog.");
        }

        internal static IEnumerable<(int size, nuint start, nuint length)> SliceSlicesCorrectlyTestCaseValues()
        {
            yield return (7, 2, 4);
            yield return (8, 0, 8);
            yield return (8, 8, 0);
        }
    }
}
