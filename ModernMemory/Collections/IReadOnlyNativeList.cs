﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
    public interface IReadOnlyNativeList<T> : IReadOnlyNativeCollection<T>
    {
        T this[nuint index] { get; }
    }
}
