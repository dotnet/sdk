// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Tools.Formatters;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    [TestClass]
    public class CompositeWhitespaceFormatterTests : CSharpFormatterTests
    {
        private protected override ICodeFormatter Formatter => new CompositeWhitespaceFormatter();

        [TestMethod]
        public async Task WhitespaceIsRetainedWithoutFinalNewlineSetting()
        {
            await AssertCodeChangedAsync(
                "class C\n{\n    int x=1;\n}",
                "class C\n{\n    int x = 1;\n}",
                new Dictionary<string, string> { ["end_of_line"] = "lf" });
        }

        [TestMethod]
        [DataRow("true", "\n")]
        [DataRow("false", "")]
        public async Task AppliesWhitespaceNewlineAndCharsetTogether(string insertFinalNewline, string suffix)
        {
            var text = await AssertCodeChangedAsync(
                "class C\r\n{\r\n    int x=1;\r\n}\r\n",
                "class C\n{\n    int x = 1;\n}" + suffix,
                new Dictionary<string, string>
                {
                    ["insert_final_newline"] = insertFinalNewline,
                    ["end_of_line"] = "lf",
                    ["charset"] = "utf-8-bom",
                },
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            Assert.IsNotNull(text.Encoding);
            text.Encoding.GetPreamble().Should().Equal(new byte[] { 0xef, 0xbb, 0xbf });
        }
    }
}
