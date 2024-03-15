using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers
{
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public interface ISequencePosition<TSelf> : IEquatable<TSelf>
        where TSelf : unmanaged, ISequencePosition<TSelf>
    {
    }
}
