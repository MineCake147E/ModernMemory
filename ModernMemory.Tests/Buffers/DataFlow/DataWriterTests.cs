using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using ModernMemory.Buffers.DataFlow;

namespace ModernMemory.Tests.Buffers.DataFlow
{
    [TestFixture]
    public class DataWriterTests
    {
        [Test]
        public void WritesCorrectly()
        {
            var bw = new ArrayBufferWriter<byte>();
            Span<byte> data = new byte[512];
            RandomNumberGenerator.Fill(data);
            var dw = DataWriter<byte>.CreateFrom(ref bw);
            dw.WriteAtMost(data);
            dw.Flush();
            Assert.That(bw.WrittenSpan.ToArray(), Is.EqualTo(data.ToArray()));
            dw.Dispose();
        }
    }
}
