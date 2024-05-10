using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.DataFlow
{
    public readonly struct AvailableElementsResult
    {
        private readonly sbyte internalValue;

        private AvailableElementsResult(sbyte internalValue)
        {
            this.internalValue = internalValue;
        }

        public AvailableElementsResult(ValueKind valueKind) : this((sbyte)valueKind) { }

        public AvailableElementsResult(EmptyReason emptyReason) : this((sbyte)(0x80 | (uint)emptyReason)) { }

        public static AvailableElementsResult EmptyWith(EmptyReason emptyReason, out nuint count)
        {
            count = 0;
            return new(emptyReason);
        }

        public static AvailableElementsResult ValueWith(nuint value, out nuint count)
        {
            count = value;
            return Value;
        }

        public static AvailableElementsResult ValueWith(ValueKind kind, out nuint count)
        {
            count = nuint.MaxValue;
            return new(kind);
        }

        public EmptyReason EmptyReason
        {
            get
            {
                var s = (uint)(int)internalValue;
                return (EmptyReason)(byte)(s & s >> 25);
            }
        }

        public ValueKind ValueKind
        {
            get
            {
                var s = (uint)(int)internalValue;
                return (ValueKind)(byte)(s & ~s >> 25);
            }
        }

        public bool HasElements => internalValue >= 0;

        public bool IsEmpty => internalValue < 0;

        public bool IsCountValid => internalValue <= 0;

        public static AvailableElementsResult Value => new(ValueKind.Countable);

        public static AvailableElementsResult CountableOutOfRange => new(ValueKind.CountableOutOfRange);

        public static AvailableElementsResult Uncountable => new(ValueKind.Uncountable);

        public static AvailableElementsResult Infinite => new(ValueKind.Infinite);

        public static AvailableElementsResult WaitingForSource => new(EmptyReason.WaitingForSource);

        public static AvailableElementsResult Canceled => new(EmptyReason.Canceled);

        public static AvailableElementsResult SectionComplete => new(EmptyReason.SectionComplete);

        public static AvailableElementsResult StreamComplete => new(EmptyReason.StreamComplete);
    }

    [Flags]
    public enum EmptyReason : byte
    {
        None = 0,
        WaitingForSource = 1,
        Canceled = 2,
        SectionComplete = 0x40,
        StreamComplete = 0x80
    }

    public enum ValueKind : byte
    {
        Countable = 0,
        CountableOutOfRange,
        Uncountable,
        Infinite,
    }
}
