using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers;

namespace ModernMemory.Tests
{
    internal static class TestUtils
    {
        public static ParallelQuery<(T First, T Second)> ZipAdjacent<T>(this OrderedParallelQuery<T> values)
        {
            var en = values.SkipWhile(a => false).Aggregate(default((ParallelQuery<(T First, T Second)>? zipped, List<(T First, T Second)>? chunk, (T first, T last)? edge)), (z, item) =>
            {
                if (z.edge.HasValue)
                {
                    var newItem = (First: z.edge.Value.last, Srcond: item);
                    var list = z.chunk;
                    list?.Add(newItem);
                    list ??= new([newItem]);
                    return (z.zipped, list, (z.edge.Value.first, item));
                }
                else
                {
                    return (z.zipped, z.chunk, (item, item));
                }
            }, combineAccumulatorsFunc: (c0, c1) =>
            {
                var e0 = c0.edge;
                var e1 = c1.edge;
                ((T First, T Second)? newItem, (T first, T last)? newEdge) merged = (e0, e1) switch
                {
                    ({ } v0, { } v1) => ((v0.last, v1.first), (v0.first, v1.last)),
                    ({ } v0, null) => (null, v0),
                    _ => (null, e1),
                };
                var z0 = c0.zipped;
                var z1 = c1.zipped;
                var l0 = c0.chunk;
                var l1 = c1.chunk;
                l0 = l0.AddOrCreateIfNotNull(merged.newItem);
                var merging = (l0, z1) switch
                {
                    (null, null) => null,
                    ({ } lv0, null) => lv0.AsParallel(),
                    (null, { } zv1) => zv1,
                    _ => l0.AsParallel().Concat(z1)
                };
                z0 = merging is not null ? z0?.Concat(merging) ?? merging : z0;
                return (z0, l1, merged.newEdge);
            }, a => a.chunk is not null ? a.zipped?.Concat(a.chunk.AsParallel()) ?? a.chunk.AsParallel() : null);
            return en ?? ParallelEnumerable.Empty<(T First, T Second)>();
        }

        public static bool AdjacentElementAnyEquals<T>(this ParallelQuery<T> values, Func<T, T, bool> predicate)
        {
            var en = values.SkipWhile(a => false).Aggregate(default((bool result, (T first, T last)? edge)), (z, item) => z.edge.HasValue ? ((bool result, (T first, T last)? edge))(z.result || predicate(z.edge.Value.last, item), (z.edge.Value.first, item)) : ((bool result, (T first, T last)? edge))(z.result, (item, item))
            , combineAccumulatorsFunc: (c0, c1) =>
            {
                var e0 = c0.edge;
                var e1 = c1.edge;
                ((T First, T Second)? newItem, (T first, T last)? newEdge) merged = (e0, e1) switch
                {
                    ({ } v0, { } v1) => ((v0.last, v1.first), (v0.first, v1.last)),
                    ({ } v0, null) => (null, v0),
                    _ => (null, e1),
                };
                var z0 = c0.result;
                var z1 = c1.result;
                return (z0 || z1 || merged.newItem.HasValue && predicate(merged.newItem.Value.First, merged.newItem.Value.Second), merged.newEdge);
            }, a => a.result || a.edge.HasValue && predicate(a.edge.Value.first, a.edge.Value.last));
            return en;
        }

        private static List<T>? AddOrCreateIfNotNull<T>(this List<T>? list, T? elementToAdd) where T : struct
        {
            if (!elementToAdd.HasValue) return list;
            list?.Add(elementToAdd.Value);
            list ??= new([elementToAdd.Value]);
            return list;
        }

        public static T Slice<T>(this T sliceable, SliceData slice) where T : ISliceable<T, nuint>
            => !slice.SliceByLength ? sliceable.Slice(slice.Start) : sliceable.Slice(slice.Start, slice.Length);

        public static ReadOnlySequenceSlim<T> Slice<T>(this ReadOnlySequenceSlim<T> sliceable, SliceData slice)
            => !slice.SliceByLength ? sliceable.Slice(slice.Start) : sliceable.Slice(slice.Start, slice.Length);
    }
}
