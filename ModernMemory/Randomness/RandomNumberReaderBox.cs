using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Randomness
{
    public sealed class RandomNumberReaderBox<TRandomNumberReader>(TRandomNumberReader reader) : IRandomNumberReader where TRandomNumberReader : struct, IRandomNumberReader
    {
        private TRandomNumberReader reader = reader;

        public ref TRandomNumberReader Reader => ref reader;

        public void ReadBytes(NativeSpan<byte> destination) => Reader.ReadBytes(destination);
        public byte ReadByte() => Reader.ReadByte();
        public ulong GenerateBitsUInt64(int bits) => Reader.GenerateBitsUInt64(bits);
        public uint GenerateBitsUInt32(int bits) => Reader.GenerateBitsUInt32(bits);
        public ulong GenerateRange(ulong maxExclusive) => Reader.GenerateRange(maxExclusive);
        public ulong GenerateRange(ulong minInclusive, ulong maxExclusive) => Reader.GenerateRange(minInclusive, maxExclusive);
        public uint GenerateRange(uint maxExclusive) => Reader.GenerateRange(maxExclusive);
        public uint GenerateRange(uint minInclusive, uint maxExclusive) => Reader.GenerateRange(minInclusive, maxExclusive);
        public ushort GenerateRange(ushort maxExclusive) => Reader.GenerateRange(maxExclusive);
        public ushort GenerateRange(ushort minInclusive, ushort maxExclusive) => Reader.GenerateRange(minInclusive, maxExclusive);
        public byte GenerateRange(byte maxExclusive) => Reader.GenerateRange(maxExclusive);
        public byte GenerateRange(byte minInclusive, byte maxExclusive) => Reader.GenerateRange(minInclusive, maxExclusive);
        public void Shuffle<T>(NativeSpan<T> values, ReadOnlyNativeSpan<T> source) => Reader.Shuffle(values, source);
        public void Shuffle<T>(NativeSpan<T> values) => Reader.Shuffle(values);
        public bool TryShuffle<T>(NativeSpan<T> values, ReadOnlyNativeSpan<T> source) => Reader.TryShuffle(values, source);
    }
}
