using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections.Core
{
    public interface ICollectionBackend<T>
    {
        NativeSpan<T> Span { get; }

    }
}
