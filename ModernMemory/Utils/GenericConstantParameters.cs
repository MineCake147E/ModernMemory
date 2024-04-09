using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Utils
{
    public interface IGenericConstantParameter<T, TSelf>
        where T : IEquatable<T>
        where TSelf : unmanaged, IGenericConstantParameter<T, TSelf>
    {
        public static abstract T Value { get; }
        public static virtual bool ValueMatches(T value) => value.Equals(TSelf.Value);
    }

    public interface IGenericBoolParameter<TSelf> : IGenericConstantParameter<bool, TSelf>
        where TSelf : unmanaged, IGenericBoolParameter<TSelf>
    {
    }

    internal interface IGenericBoolParameter<TSelf, TNegated> : IGenericBoolParameter<TSelf>
        where TSelf : unmanaged, IGenericBoolParameter<TSelf, TNegated>
        where TNegated : unmanaged, IGenericBoolParameter<TNegated, TSelf>
    {
    }
    public readonly struct TypeTrue : IGenericBoolParameter<TypeTrue, TypeFalse>
    {
        public static bool Value => true;
    }

    public readonly struct TypeFalse : IGenericBoolParameter<TypeFalse, TypeTrue>
    {
        public static bool Value => false;
    }
}
