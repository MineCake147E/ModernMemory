using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Utils
{
    public interface ICopyable<T, TTo> : INativeSpanCopyable<T>
    {
        bool TryCopyTo(TTo destination);

        void CopyTo(TTo destination);
    }
    public interface INativeSpanCopyable<T>
    {
        bool TryCopyTo(NativeSpan<T> destination);

        void CopyTo(NativeSpan<T> destination);
    }
}
