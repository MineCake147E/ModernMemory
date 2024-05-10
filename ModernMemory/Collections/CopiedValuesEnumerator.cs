﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Collections
{
    public sealed class CopiedValuesEnumerator<T> : IEnumerator<T>
    {
        ArrayOwner<T>? owner;
        ReadOnlyNativeMemory<T>.Enumerator enumerator;

        public CopiedValuesEnumerator(ReadOnlyNativeSpan<T> values)
        {
            var o = owner = new(values.Length);
            var om = o.NativeMemory.Slice(0, values.Length);
            values.CopyAtMostTo(om.Span);
            enumerator = om.GetMemoryEnumerator();
        }

        public T Current => enumerator.Current;
        object IEnumerator.Current => Current!;

        public bool MoveNext() => enumerator.MoveNext();
        public void Reset() => enumerator.Reset();

        private void Dispose(bool disposing)
        {
            if (Interlocked.Exchange(ref owner, null) is { } o)
            {
                if (disposing && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                {
                    o.Clear();
                }
                enumerator.Dispose();
                enumerator = default;
                o.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
