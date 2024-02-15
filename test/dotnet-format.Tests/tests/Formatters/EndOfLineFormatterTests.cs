// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Tools.Formatters;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public class EndOfLineFormatterTests : CSharpFormatterTests
    {
        private protected override ICodeFormatter Formatter => new EndOfLineFormatter();

        public EndOfLineFormatterTests(ITestOutputHelper output)
        {
            TestOutputHelper = output;
        }

        [Theory]
        [InlineData("\n", "\n", "lf")]
        [InlineData("\r\n", "\n", "lf")]
        [InlineData("\r", "\n", "lf")]
        [InlineData("\n", "\r\n", "crlf")]
        [InlineData("\r\n", "\r\n", "crlf")]
        [InlineData("\r", "\r\n", "crlf")]
        [InlineData("\n", "\r", "cr")]
        [InlineData("\r\n", "\r", "cr")]
        [InlineData("\r", "\r", "cr")]
        public async Task TestEndOfLine_NoFinalNewline(string codeNewline, string expectedNewline, string endOfLine)
        {
            var testCode = $"class C{codeNewline}{{{codeNewline}}}";

            var expectedCode = $"class C{expectedNewline}{{{expectedNewline}}}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["end_of_line"] = endOfLine,
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig);
        }

        [Theory]
        [InlineData("\n", "\n", "lf")]
        [InlineData("\r\n", "\n", "lf")]
        [InlineData("\r", "\n", "lf")]
        [InlineData("\n", "\r\n", "crlf")]
        [InlineData("\r\n", "\r\n", "crlf")]
        [InlineData("\r", "\r\n", "crlf")]
        [InlineData("\n", "\r", "cr")]
        [InlineData("\r\n", "\r", "cr")]
        [InlineData("\r", "\r", "cr")]
        public async Task TestEndOfLine_WithFinalNewline(string codeNewline, string expectedNewline, string endOfLine)
        {
            var testCode = $"class C{codeNewline}{{{codeNewline}}}{codeNewline}";

            var expectedCode = $"class C{expectedNewline}{{{expectedNewline}}}{expectedNewline}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["end_of_line"] = endOfLine,
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig);
        }

        [Theory]
        [InlineData("\n")]
        [InlineData("\r\n")]
        [InlineData("\r")]
        public async Task TestEndOfLine_AndNoSetting_NoChanges(string codeNewline)
        {
            var testCode = $"class C{codeNewline}{{{codeNewline}}}{codeNewline}";

            var editorConfig = new Dictionary<string, string>();

            await AssertCodeUnchangedAsync(testCode, editorConfig);
        }
    }
}
