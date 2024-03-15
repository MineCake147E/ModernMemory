using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    public interface IResultSummary<TSelf, out TErrorDetails>
        where TErrorDetails : unmanaged
        where TSelf : struct, IResultSummary<TSelf, TErrorDetails>
    {
        ResultLevel Level { get; }

        TErrorDetails Details { get; }

        Action? ThrowAction { get; }
    }
}
