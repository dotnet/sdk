using Microsoft.TemplateEngine.Abstractions.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public abstract class TestBase
    {
        protected void RunAndVerify(string originalValue, string expectedValue, IProcessor processor, int bufferSize)
        {
            byte[] valueBytes = Encoding.UTF8.GetBytes(originalValue);
            MemoryStream input = new MemoryStream(valueBytes);
            MemoryStream output = new MemoryStream();
            bool changed = processor.Run(input, output, bufferSize);
            Verify(Encoding.UTF8, output, changed, originalValue, expectedValue);
        }

        protected void Verify(Encoding encoding, Stream output, bool changed, string source, string expected)
        {
            output.Position = 0;
            byte[] resultBytes = new byte[output.Length];
            output.Read(resultBytes, 0, resultBytes.Length);
            string actual = encoding.GetString(resultBytes);
            Assert.Equal(expected, actual);

            bool expectedChange = !string.Equals(expected, source, StringComparison.Ordinal);
            string modifier = expectedChange ? "" : "not ";
            if (expectedChange ^ changed)
            {
                Assert.False(true, $"Expected value to {modifier} be changed");
            }
        }
    }
}
