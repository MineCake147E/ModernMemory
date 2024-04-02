using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
    public interface INativeIndexable<T> : IReadOnlyNativeIndexable<T>
    {
        new T this[nuint index] { get; set; }
        T IReadOnlyNativeIndexable<T>.this[nuint index] => this[index];
    }

    public interface IReadOnlyNativeIndexable<out T> : ICountable<nuint>
    {
        T this[nuint index] { get; }
    }
}
