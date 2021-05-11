// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public abstract class TestBase
    {
        protected static void RunAndVerify(string originalValue, string expectedValue, IProcessor processor, int bufferSize, bool? changeOverride = null, bool emitBOM = false)
        {
            byte[] valueBytes = Encoding.UTF8.GetBytes(originalValue);
            using MemoryStream input = new MemoryStream();
            if (emitBOM)
            {
                byte[] preamble = new UTF8Encoding(true).GetPreamble();
                input.Write(preamble, 0, preamble.Length);
            }
            input.Write(valueBytes, 0, valueBytes.Length);
            input.Position = 0;
            using MemoryStream output = new MemoryStream();
            bool changed = processor.Run(input, output, bufferSize);
            Verify(new UTF8Encoding(emitBOM), output, changed, originalValue, expectedValue, changeOverride, emitBOM);
        }

        protected static void Verify(Encoding encoding, Stream output, bool changed, string source, string expected, bool? changeOverride = null, bool checkBOM = false)
        {
            output.Position = 0;

            if (checkBOM)
            {
                byte[] preamble = encoding.GetPreamble();
                if (preamble.Length > 0)
                {
                    byte[] readPreamble = new byte[preamble.Length];
                    Assert.Equal(readPreamble.Length, output.Read(readPreamble, 0, readPreamble.Length));
                    Assert.Equal(preamble, readPreamble);
                }
            }

            byte[] resultBytes = new byte[output.Length - output.Position];
            output.Read(resultBytes, 0, resultBytes.Length);
            string actual = encoding.GetString(resultBytes);
            Assert.Equal(expected, actual);

            bool expectedChange = changeOverride ?? !string.Equals(expected, source, StringComparison.Ordinal);
            string modifier = expectedChange ? "" : "not ";
            if (expectedChange ^ changed)
            {
                Assert.False(true, $"Expected value to {modifier} be changed");
            }
        }
    }
}
