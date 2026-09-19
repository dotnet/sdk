// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Tools.Formatters;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    [TestClass]
    public class CharsetFormatterTests : CSharpFormatterTests
    {
        private protected override ICodeFormatter Formatter => new CharsetFormatter();

        [TestMethod]
        [DataRow("latin1", "utf-8")]
        [DataRow("latin1", "utf-8-bom")]
        [DataRow("latin1", "utf-16be")]
        [DataRow("latin1", "utf-16le")]
        [DataRow("utf-8", "latin1")]
        [DataRow("utf-8", "utf-8-bom")]
        [DataRow("utf-8", "utf-16be")]
        [DataRow("utf-8", "utf-16le")]
        [DataRow("utf-8-bom", "latin1")]
        [DataRow("utf-8-bom", "utf-8")]
        [DataRow("utf-8-bom", "utf-16be")]
        [DataRow("utf-8-bom", "utf-16le")]
        [DataRow("utf-16be", "latin1")]
        [DataRow("utf-16be", "utf-8")]
        [DataRow("utf-16be", "utf-8-bom")]
        [DataRow("utf-16be", "utf-16le")]
        [DataRow("utf-16le", "latin1")]
        [DataRow("utf-16le", "utf-8")]
        [DataRow("utf-16le", "utf-8-bom")]
        [DataRow("utf-16le", "utf-16be")]
        public async Task TestCharsetWrong_CharsetFixed(string codeValue, string expectedValue)
        {
            var codeEncoding = CharsetFormatter.GetCharset(codeValue);
            var expectedEncoding = CharsetFormatter.GetCharset(expectedValue);

            // Use unicode to ensure that "latin1" and "utf8" don't look equivalent.
            var testCode = "class 🤵 { }";

            var editorConfig = new Dictionary<string, string>()
            {

                ["charset"] = expectedValue,
            };

            var formattedText = await AssertCodeUnchangedAsync(testCode, editorConfig, codeEncoding);

            Assert.AreEqual(expectedEncoding, formattedText.Encoding);
        }

        [TestMethod]
        public async Task TestCharsetNotSpecified_NoChange()
        {
            // This encoding is not supported by .editorconfig, so if it roundtrips then there was no change.
            var codeEncoding = Encoding.UTF32;

            var testCode = "class 🤵 { }";

            var editorConfig = new Dictionary<string, string>()
            {
            };

            var formattedText = await AssertCodeUnchangedAsync(testCode, editorConfig, codeEncoding);

            Assert.AreEqual(codeEncoding, formattedText.Encoding);
        }
    }
}
