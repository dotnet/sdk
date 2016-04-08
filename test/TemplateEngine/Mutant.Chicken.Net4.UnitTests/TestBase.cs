using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Mutant.Chicken.Net4.UnitTests
{
    [ExcludeFromCodeCoverage]
    public abstract class TestBase
    {
        protected void Verify(Encoding encoding, Stream output, bool changed, string source, string expected)
        {
            output.Position = 0;
            byte[] resultBytes = new byte[output.Length];
            output.Read(resultBytes, 0, resultBytes.Length);
            string actual = encoding.GetString(resultBytes);
            AssertEx.AreEqual(expected, actual);

            bool expectedChange = !string.Equals(expected, source, StringComparison.Ordinal);
            string modifier = expectedChange ? "" : "not ";
            Assert.AreEqual(expectedChange, changed, $"Expected value to {modifier} be changed");
        }
    }
}
