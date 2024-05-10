using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.DataFlow
{
    public interface ISequenceDataProvider<T> : IDataProvider<T>, ISequenceDataReader<T>
    {

    }
}
