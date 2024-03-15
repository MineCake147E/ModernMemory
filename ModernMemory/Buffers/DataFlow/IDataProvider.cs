using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModernMemory.Buffers.DataFlow
{
    public interface IDataProvider<T>
    {
        /// <summary>
        /// Gets the amount of data can be read currently from this <see cref="IDataProvider{T}"/>.
        /// </summary>
        /// <param name="count">When this method returns, contains the amount of data can be read currently from this <see cref="IDataProvider{T}"/>, if the <see cref="IDataProvider{T}"/> can suggest it; otherwise, the <see cref="nuint.MaxValue"/>.</param>
        /// <returns><see langword="true"/> if the <see cref="IDataProvider{T}"/> can suggest the amount of data can be read currently from this <see cref="IDataProvider{T}"/>; otherwise, <see langword="false"/>.</returns>
        AvailableElementsResult TryGetAvailableElements(out nuint count);

        /// <summary>
        /// Notifies <see cref="IDataProvider{T}"/> that <paramref name="count"/> amount of data was read.
        /// </summary>
        /// <param name="count">The number of data items read.</param>
        /// <remarks>You must request a new buffer after calling <see cref="AdvanceRead(nuint)"/> to continue reading more data and cannot read from a previously acquired buffer.</remarks>
        void AdvanceRead(nuint count);

        void Prefetch(nuint count);

        /// <summary>
        /// Gets the preferred <see cref="ReadingMethods"/> that this <see cref="IDataProvider{T}"/> provides.
        /// </summary>
        ReadingMethods PreferredReadingMethods => ReadingMethods.Pull;

        IBufferedDataProvider<T>? BufferedDataProvider => this as IBufferedDataProvider<T>;
        ISequenceDataProvider<T>? SequenceDataProvider => this as ISequenceDataProvider<T>;

        #region Methods without auto-Advance

        void PeekExact(NativeSpan<T> destination, nuint offset = 0);
        nuint PeekAtMost(NativeSpan<T> destination, nuint offset = 0);
        bool TryPeekExact(NativeSpan<T> destination, nuint offset = 0);
        void PeekExactTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count, nuint offset = 0) where TBufferWriter : INativeBufferWriter<T>;
        void PeekAtMostTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count = 0, nuint offset = 0) where TBufferWriter : INativeBufferWriter<T>;
        #endregion

        #region Methods with auto-Advance
        void ReadExact(NativeSpan<T> destination)
        {
            PeekExact(destination);
            AdvanceRead(destination.Length);
        }
        nuint ReadAtMost(NativeSpan<T> destination)
        {
            var res = PeekAtMost(destination);
            AdvanceRead(res);
            return res;
        }
        bool TryReadExact(NativeSpan<T> destination)
        {
            var res = TryPeekExact(destination);
            if (res) AdvanceRead(destination.Length);
            return res;
        }

        void WriteExactTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count) where TBufferWriter : INativeBufferWriter<T>
        {
            PeekExactTo(bufferWriter, count);
            AdvanceRead(count);
        }
        void WriteAtMostTo<TBufferWriter>(TBufferWriter bufferWriter, nuint count = 0) where TBufferWriter : INativeBufferWriter<T>;
        #endregion
    }
}
