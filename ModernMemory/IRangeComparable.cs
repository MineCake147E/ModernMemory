using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory
{
    public interface IRangeComparable<in TIndex>
    {
        int CompareTo(TIndex index);
    }
}
