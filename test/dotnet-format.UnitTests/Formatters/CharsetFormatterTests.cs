// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Tools.Formatters;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public class CharsetFormatterTests : CSharpFormatterTests
    {
        private protected override ICodeFormatter Formatter => new CharsetFormatter();

        public CharsetFormatterTests(ITestOutputHelper output)
        {
            TestOutputHelper = output;
        }

        [Theory]
        [InlineData("latin1", "utf-8")]
        [InlineData("latin1", "utf-8-bom")]
        [InlineData("latin1", "utf-16be")]
        [InlineData("latin1", "utf-16le")]
        [InlineData("utf-8", "latin1")]
        [InlineData("utf-8", "utf-8-bom")]
        [InlineData("utf-8", "utf-16be")]
        [InlineData("utf-8", "utf-16le")]
        [InlineData("utf-8-bom", "latin1")]
        [InlineData("utf-8-bom", "utf-8")]
        [InlineData("utf-8-bom", "utf-16be")]
        [InlineData("utf-8-bom", "utf-16le")]
        [InlineData("utf-16be", "latin1")]
        [InlineData("utf-16be", "utf-8")]
        [InlineData("utf-16be", "utf-8-bom")]
        [InlineData("utf-16be", "utf-16le")]
        [InlineData("utf-16le", "latin1")]
        [InlineData("utf-16le", "utf-8")]
        [InlineData("utf-16le", "utf-8-bom")]
        [InlineData("utf-16le", "utf-16be")]
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

            Assert.Equal(expectedEncoding, formattedText.Encoding);
        }

        [Fact]
        public async Task TestCharsetNotSpecified_NoChange()
        {
            // This encoding is not supported by .editorconfig, so if it roundtrips then there was no change.
            var codeEncoding = Encoding.UTF32;

            var testCode = "class 🤵 { }";

            var editorConfig = new Dictionary<string, string>()
            {
            };

            var formattedText = await AssertCodeUnchangedAsync(testCode, editorConfig, codeEncoding);

            Assert.Equal(codeEncoding, formattedText.Encoding);
        }
    }
}
