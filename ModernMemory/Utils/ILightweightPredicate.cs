using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Utils
{
    public interface ILightweightPredicate<in T>
    {
        bool Predicate(T value);
    }

    public readonly struct DelegatedPredicate<T> : ILightweightPredicate<T>
    {
        private readonly Predicate<T> predicate;

        public DelegatedPredicate(Predicate<T> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            this.predicate = predicate;
        }

        public bool Predicate(T value) => predicate(value);
    }
}
