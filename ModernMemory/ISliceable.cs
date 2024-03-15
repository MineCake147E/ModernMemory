using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
#pragma warning disable S3246 // Generic type parameters should be co/contravariant when possible (false positive)
    public interface ISliceable<TSelf, TIndex>
#pragma warning restore S3246 // Generic type parameters should be co/contravariant when possible
        where TSelf : ISliceable<TSelf, TIndex>
        where TIndex : unmanaged
    {
        TSelf Slice(TIndex start);
        TSelf Slice(TIndex start, TIndex length);
    }
}
