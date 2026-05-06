// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Microsoft.CodeAnalysis.Tools.Formatters;

namespace Microsoft.CodeAnalysis.Tools.Tests.Formatters
{
    public class FinalNewlineFormatterTests : CSharpFormatterTests
    {
        private protected override ICodeFormatter Formatter => new FinalNewlineFormatter();

        public FinalNewlineFormatterTests(ITestOutputHelper output)
        {
            TestOutputHelper = output;
        }

        [Fact]
        public async Task WhenFinalNewlineUnspecified_AndFinalNewlineMissing_NoChange()
        {
            var code = @"
class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["end_of_line"] = "crlf",
            };

            await AssertCodeUnchangedAsync(code, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewlineUnspecified_AndFinalNewlineExits_NoChange()
        {
            var code = @"
class C
{
}
";

            var editorConfig = new Dictionary<string, string>()
            {
                ["end_of_line"] = "crlf",
            };

            await AssertCodeUnchangedAsync(code, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewlineRequired_AndEndOfLineIsLineFeed_LineFeedAdded()
        {
            var testCode = "class C\n{\n}";

            var expectedCode = "class C\n{\n}\n";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "true",
                ["end_of_line"] = "lf",
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewlineRequired_AndEndOfLineIsCarriageReturnLineFeed_CarriageReturnLineFeedAdded()
        {
            var testCode = "class C\r\n{\r\n}";

            var expectedCode = "class C\r\n{\r\n}\r\n";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "true",
                ["end_of_line"] = "crlf",
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewlineRequired_AndEndOfLineIsCarriageReturn_CarriageReturnAdded()
        {
            var testCode = "class C\r{\r}";

            var expectedCode = "class C\r{\r}\r";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "true",
                ["end_of_line"] = "cr",
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig);
        }
        [Fact]
        public async Task WhenFinalNewlineRequired_AndFinalNewlineExits_NoChange()
        {
            var code = @"
class C
{
}
";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "true",
                ["end_of_line"] = "crlf",
            };

            await AssertCodeUnchangedAsync(code, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewlineUnwanted_AndFinalNewlineExists_CarriageReturnLineFeedRemoved()
        {
            var testCode = "class C\r\n{\r\n}\r\n\r\n\r\n";

            var expectedCode = "class C\r\n{\r\n}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "false",
                ["end_of_line"] = "crlf",
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewlineUnwanted_AndFinalNewlineExists_LineFeedRemoved()
        {
            var testCode = "class C\n{\n}\n\n\n";

            var expectedCode = "class C\n{\n}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "false",
                ["end_of_line"] = "crlf",
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewlineUnwanted_AndFinalNewlineExists_CarriageReturnRemoved()
        {
            var testCode = "class C\r{\r}\r\r\r";

            var expectedCode = "class C\r{\r}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "false",
                ["end_of_line"] = "crlf",
            };

            await AssertCodeChangedAsync(testCode, expectedCode, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewlineUnwanted_AndFinalNewlineMissing_NoChange()
        {
            var code = @"
class C
{
}";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "false",
                ["end_of_line"] = "crlf",
            };

            await AssertCodeUnchangedAsync(code, editorConfig);
        }

        [Fact]
        public async Task WhenFinalNewlineUnwanted_AndFileIsEmpty_NoChange()
        {
            var code = @"";

            var editorConfig = new Dictionary<string, string>()
            {
                ["insert_final_newline"] = "false",
                ["end_of_line"] = "crlf",
            };

            await AssertCodeUnchangedAsync(code, editorConfig);
        }
    }
}
