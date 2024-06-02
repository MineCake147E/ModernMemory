// <auto-generated />
// Environment.Version: 8.0.6
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace ModernMemory.Buffers
{
#pragma warning disable S1144 // Unused private types or members should be removed
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0051 // Remove unused private members
    [InlineArray(4)]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct FixedArray4<T> : IFixedGenericInlineArray<T, FixedArray4<T>>
    {
        private T head;
        public int Length => 4;

        public static int Count => 4;
    
        public static Span<T> AsSpan(ref FixedArray4<T> self) => self;

        private string GetDebuggerDisplay()
        {
            ReadOnlySpan<T> a = this;
            return $"{nameof(FixedArray4<T>)}<{typeof(T).Name}>[{Length}] {{ {string.Join(", ", a.ToArray())} }}";
        }
        public override string? ToString() => GetDebuggerDisplay();
    }

    [InlineArray(8)]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct FixedArray8<T> : IFixedGenericInlineArray<T, FixedArray8<T>>
    {
        private T head;
        public int Length => 8;

        public static int Count => 8;
    
        public static Span<T> AsSpan(ref FixedArray8<T> self) => self;

        private string GetDebuggerDisplay()
        {
            ReadOnlySpan<T> a = this;
            return $"{nameof(FixedArray8<T>)}<{typeof(T).Name}>[{Length}] {{ {string.Join(", ", a.ToArray())} }}";
        }
        public override string? ToString() => GetDebuggerDisplay();
    }

    [InlineArray(12)]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct FixedArray12<T> : IFixedGenericInlineArray<T, FixedArray12<T>>
    {
        private T head;
        public int Length => 12;

        public static int Count => 12;
    
        public static Span<T> AsSpan(ref FixedArray12<T> self) => self;

        private string GetDebuggerDisplay()
        {
            ReadOnlySpan<T> a = this;
            return $"{nameof(FixedArray12<T>)}<{typeof(T).Name}>[{Length}] {{ {string.Join(", ", a.ToArray())} }}";
        }
        public override string? ToString() => GetDebuggerDisplay();
    }

    [InlineArray(16)]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct FixedArray16<T> : IFixedGenericInlineArray<T, FixedArray16<T>>
    {
        private T head;
        public int Length => 16;

        public static int Count => 16;
    
        public static Span<T> AsSpan(ref FixedArray16<T> self) => self;

        private string GetDebuggerDisplay()
        {
            ReadOnlySpan<T> a = this;
            return $"{nameof(FixedArray16<T>)}<{typeof(T).Name}>[{Length}] {{ {string.Join(", ", a.ToArray())} }}";
        }
        public override string? ToString() => GetDebuggerDisplay();
    }

    [InlineArray(32)]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct FixedArray32<T> : IFixedGenericInlineArray<T, FixedArray32<T>>
    {
        private T head;
        public int Length => 32;

        public static int Count => 32;
    
        public static Span<T> AsSpan(ref FixedArray32<T> self) => self;

        private string GetDebuggerDisplay()
        {
            ReadOnlySpan<T> a = this;
            return $"{nameof(FixedArray32<T>)}<{typeof(T).Name}>[{Length}] {{ {string.Join(", ", a.ToArray())} }}";
        }
        public override string? ToString() => GetDebuggerDisplay();
    }

    [InlineArray(64)]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct FixedArray64<T> : IFixedGenericInlineArray<T, FixedArray64<T>>
    {
        private T head;
        public int Length => 64;

        public static int Count => 64;
    
        public static Span<T> AsSpan(ref FixedArray64<T> self) => self;

        private string GetDebuggerDisplay()
        {
            ReadOnlySpan<T> a = this;
            return $"{nameof(FixedArray64<T>)}<{typeof(T).Name}>[{Length}] {{ {string.Join(", ", a.ToArray())} }}";
        }
        public override string? ToString() => GetDebuggerDisplay();
    }

    [InlineArray(128)]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct FixedArray128<T> : IFixedGenericInlineArray<T, FixedArray128<T>>
    {
        private T head;
        public int Length => 128;

        public static int Count => 128;
    
        public static Span<T> AsSpan(ref FixedArray128<T> self) => self;

        private string GetDebuggerDisplay()
        {
            ReadOnlySpan<T> a = this;
            return $"{nameof(FixedArray128<T>)}<{typeof(T).Name}>[{Length}] {{ {string.Join(", ", a.ToArray())} }}";
        }
        public override string? ToString() => GetDebuggerDisplay();
    }

    [InlineArray(256)]
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public struct FixedArray256<T> : IFixedGenericInlineArray<T, FixedArray256<T>>
    {
        private T head;
        public int Length => 256;

        public static int Count => 256;
    
        public static Span<T> AsSpan(ref FixedArray256<T> self) => self;

        private string GetDebuggerDisplay()
        {
            ReadOnlySpan<T> a = this;
            return $"{nameof(FixedArray256<T>)}<{typeof(T).Name}>[{Length}] {{ {string.Join(", ", a.ToArray())} }}";
        }
        public override string? ToString() => GetDebuggerDisplay();
    }

#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore IDE0044 // Add readonly modifier
#pragma warning restore S1144 // Unused private types or members should be removed
}
